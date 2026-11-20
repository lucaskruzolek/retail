using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Sesión única por usuario y aviso de mantenimiento (Track 6.7, Paquete 4) contra SQL Server Express. Son
/// deterministas: sin hilos ni esperas; el paso del tiempo se simula envejeciendo <c>ultimo_latido</c> con un UPDATE
/// directo, y los vencimientos los decide siempre el reloj del servidor.
/// </summary>
public class SesionesActivasTests : PedidosPruebaBase
{
    private SesionesActivasSqlServer _sesiones = null!;
    private int _idAna;
    private int _idBeto;
    private int _terminal1;
    private int _terminal2;

    public SesionesActivasTests()
        : base("Sesiones")
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var ana = Usuario.Crear("ana.cajera", "Ana", "Suarez", "hash", RolUsuarioEnum.Cajero, null);
        var beto = Usuario.Crear("beto.cajero", "Beto", "Rios", "hash", RolUsuarioEnum.Cajero, null);
        Contexto.Usuarios.AddRange(ana, beto);
        await Contexto.SaveChangesAsync();
        _idAna = ana.Id;
        _idBeto = beto.Id;

        _terminal1 = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 1");
        _terminal2 = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 2");
        _sesiones = new SesionesActivasSqlServer(Contexto);
    }

    [Fact]
    public async Task IniciarAsync_UsuarioSinSesion_LaInsertaYQuedaListada()
    {
        // Act
        var resultado = await _sesiones.IniciarAsync(_idAna, _terminal1);

        // Assert
        resultado.Iniciada.Should().BeTrue();
        resultado.TomoSesionVencida.Should().BeFalse();
        var lista = await _sesiones.ListarAsync();
        var fila = lista.Should().ContainSingle().Subject;
        fila.IdUsuario.Should().Be(_idAna);
        fila.NombreUsuario.Should().Be("Ana Suarez");
        fila.IdTerminal.Should().Be(_terminal1);
        fila.NombreTerminal.Should().Be("Mostrador 1");
        fila.Vencida.Should().BeFalse();
    }

    [Fact]
    public async Task IniciarAsync_MismoUsuarioEnOtraTerminalMientrasEstaViva_RechazaYDiceDondeEsta()
    {
        // Arrange
        await _sesiones.IniciarAsync(_idAna, _terminal1);

        // Act
        var resultado = await _sesiones.IniciarAsync(_idAna, _terminal2);

        // Assert
        resultado.Iniciada.Should().BeFalse();
        resultado.TerminalOcupada.Should().Be("Mostrador 1");
        resultado.InicioOtraSesionUtc.Should().NotBeNull();
        resultado.InicioOtraSesionUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        resultado.InicioOtraSesionUtc.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        (await _sesiones.ListarAsync()).Should().ContainSingle(s => s.IdTerminal == _terminal1);
    }

    [Fact]
    public async Task IniciarAsync_SesionVencidaEnOtraTerminal_LaTomaYElLatidoDeLaVieja_YaNoEsVigente()
    {
        // Arrange: la sesión de Ana en la terminal 1 dejó de latir hace 4 minutos (vence a los 3).
        await _sesiones.IniciarAsync(_idAna, _terminal1);
        await EnvejecerAsync(_idAna, minutos: 4);

        // Act
        var resultado = await _sesiones.IniciarAsync(_idAna, _terminal2);
        var latidoDeLaVieja = await _sesiones.RenovarAsync(_idAna, _terminal1);
        var latidoDeLaNueva = await _sesiones.RenovarAsync(_idAna, _terminal2);

        // Assert
        resultado.Iniciada.Should().BeTrue();
        resultado.TomoSesionVencida.Should().BeTrue();
        latidoDeLaVieja.SesionVigente.Should().BeFalse();
        latidoDeLaNueva.SesionVigente.Should().BeTrue();
        (await _sesiones.ListarAsync()).Should().ContainSingle(s => s.IdTerminal == _terminal2);
    }

    [Fact]
    public async Task IniciarAsync_OtroUsuarioEnLaMismaTerminal_QuitaLaFilaHuerfanaDeEsaTerminal()
    {
        // Arrange: Ana quedó "conectada" en la terminal 1 (la PC se colgó sin cerrar sesión).
        await _sesiones.IniciarAsync(_idAna, _terminal1);

        // Act: Beto entra desde esa misma PC.
        var resultado = await _sesiones.IniciarAsync(_idBeto, _terminal1);

        // Assert
        resultado.Iniciada.Should().BeTrue();
        var lista = await _sesiones.ListarAsync();
        lista.Should().ContainSingle().Which.IdUsuario.Should().Be(_idBeto);
    }

    [Fact]
    public async Task IniciarAsync_RechazadaPorSesionViva_NoDejaEfectosEnLaFilaHuerfanaDeLaTerminal()
    {
        // Arrange: Beto figura en la terminal 1 y Ana está viva en la 2.
        await _sesiones.IniciarAsync(_idBeto, _terminal1);
        await _sesiones.IniciarAsync(_idAna, _terminal2);

        // Act: Ana intenta entrar por la terminal 1: el borrado de la fila de Beto se deshace junto con el rechazo.
        var resultado = await _sesiones.IniciarAsync(_idAna, _terminal1);

        // Assert
        resultado.Iniciada.Should().BeFalse();
        (await _sesiones.ListarAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task IniciarAsync_MismoUsuarioEnLaMismaTerminal_RefrescaLaSesion()
    {
        // Arrange: una sesión de esta misma terminal, ya vencida (cierre brusco de la aplicación).
        await _sesiones.IniciarAsync(_idAna, _terminal1);
        await EnvejecerAsync(_idAna, minutos: 4);

        // Act
        var resultado = await _sesiones.IniciarAsync(_idAna, _terminal1);

        // Assert
        resultado.Iniciada.Should().BeTrue();
        resultado.TomoSesionVencida.Should().BeFalse();
        var fila = (await _sesiones.ListarAsync()).Should().ContainSingle().Subject;
        fila.Vencida.Should().BeFalse();
    }

    [Fact]
    public async Task RenovarAsync_SesionPropia_LaMantieneVivaYDevuelveLaHoraDelServidor()
    {
        // Arrange: el último latido tiene 2 minutos (todavía vigente); sin renovar, con 2 más pasaría de los 3 de vencimiento.
        await _sesiones.IniciarAsync(_idAna, _terminal1);
        await EnvejecerAsync(_idAna, minutos: 2);

        // Act: el latido lo deja en "ahora"; pasan otros 2 minutos y sigue vigente.
        var latido = await _sesiones.RenovarAsync(_idAna, _terminal1);
        await EnvejecerAsync(_idAna, minutos: 2);

        // Assert
        latido.SesionVigente.Should().BeTrue();
        latido.AhoraServidorUtc.Kind.Should().Be(DateTimeKind.Utc);
        latido.AhoraServidorUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        latido.MantenimientoProgramadoParaUtc.Should().BeNull();
        (await _sesiones.ListarAsync()).Single().Vencida.Should().BeFalse();
    }

    [Fact]
    public async Task RenovarAsync_SinSesion_NoEsVigente()
    {
        // Act
        var latido = await _sesiones.RenovarAsync(_idAna, _terminal1);

        // Assert
        latido.SesionVigente.Should().BeFalse();
    }

    [Fact]
    public async Task CerrarAsync_BorraSoloLaFilaExactaDeEsaTerminal()
    {
        // Arrange
        await _sesiones.IniciarAsync(_idAna, _terminal1);
        await _sesiones.IniciarAsync(_idBeto, _terminal2);

        // Act: Ana cierra "desde la terminal 2" (no es suya: otra terminal tomó su sesión) y luego desde la suya.
        await _sesiones.CerrarAsync(_idAna, _terminal2);
        var trasCierreAjeno = await _sesiones.ListarAsync();
        await _sesiones.CerrarAsync(_idAna, _terminal1);
        var trasCierrePropio = await _sesiones.ListarAsync();

        // Assert
        trasCierreAjeno.Should().HaveCount(2);
        trasCierrePropio.Should().ContainSingle().Which.IdUsuario.Should().Be(_idBeto);
    }

    [Fact]
    public async Task ForzarCierreAsync_BorraLaSesion_YElLatidoSiguienteNoEsVigente()
    {
        // Arrange
        await _sesiones.IniciarAsync(_idAna, _terminal1);

        // Act
        var habia = await _sesiones.ForzarCierreAsync(_idAna);
        var latido = await _sesiones.RenovarAsync(_idAna, _terminal1);
        var habiaOtraVez = await _sesiones.ForzarCierreAsync(_idAna);

        // Assert
        habia.Should().BeTrue();
        latido.SesionVigente.Should().BeFalse();
        habiaOtraVez.Should().BeFalse();
    }

    [Fact]
    public async Task ProgramarMantenimientoAsync_ElLatidoDevuelveLaHoraYElMotivo_YCancelarLoLimpia()
    {
        // Arrange
        await _sesiones.IniciarAsync(_idAna, _terminal1);

        // Act
        await _sesiones.ProgramarMantenimientoAsync(15, "Restaurar la base", IdUsuario);
        var conAviso = await _sesiones.RenovarAsync(_idAna, _terminal1);
        var estadoConAviso = await _sesiones.ObtenerMantenimientoAsync();
        await _sesiones.CancelarMantenimientoAsync();
        var sinAviso = await _sesiones.RenovarAsync(_idAna, _terminal1);
        var estadoSinAviso = await _sesiones.ObtenerMantenimientoAsync();

        // Assert
        conAviso.MantenimientoProgramadoParaUtc.Should().NotBeNull();
        conAviso.MantenimientoProgramadoParaUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        (conAviso.MantenimientoProgramadoParaUtc.Value - conAviso.AhoraServidorUtc)
            .Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(30));
        conAviso.MotivoMantenimiento.Should().Be("Restaurar la base");
        estadoConAviso.HayMantenimiento.Should().BeTrue();
        estadoConAviso.Motivo.Should().Be("Restaurar la base");

        sinAviso.MantenimientoProgramadoParaUtc.Should().BeNull();
        sinAviso.MotivoMantenimiento.Should().BeNull();
        estadoSinAviso.HayMantenimiento.Should().BeFalse();
    }

    [Fact]
    public async Task ContarSesionesVigentesExceptoAsync_IgnoraVencidasYLaTerminalExcluida()
    {
        // Arrange: Ana vigente en la terminal 1, Beto vencido en la terminal 2, Cora vigente en la terminal 3.
        var cora = Usuario.Crear("cora.cajera", "Cora", "Paz", "hash", RolUsuarioEnum.Cajero, null);
        Contexto.Usuarios.Add(cora);
        await Contexto.SaveChangesAsync();
        var terminal3 = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 3");
        await _sesiones.IniciarAsync(_idAna, _terminal1);
        await _sesiones.IniciarAsync(_idBeto, _terminal2);
        await _sesiones.IniciarAsync(cora.Id, terminal3);
        await EnvejecerAsync(_idBeto, minutos: 4);

        // Act
        var excluyendoLaTerminal1 = await _sesiones.ContarSesionesVigentesExceptoAsync(_terminal1);
        var excluyendoLaTerminal2 = await _sesiones.ContarSesionesVigentesExceptoAsync(_terminal2);

        // Assert: sin contar la 1, queda Cora (Beto está vencido); sin contar la 2, quedan Ana y Cora.
        excluyendoLaTerminal1.Should().Be(1);
        excluyendoLaTerminal2.Should().Be(2);
    }

    [Fact]
    public async Task Migracion_DejaUnaUnicaFilaDeMantenimiento_YElCheckRechazaUnaSegunda()
    {
        // Act
        var filas = await Contexto.Database.SqlQueryRaw<int>("SELECT id AS Value FROM MANTENIMIENTO_PROGRAMADO").ToListAsync();
        var segunda = () => Contexto.Database.ExecuteSqlRawAsync("INSERT INTO MANTENIMIENTO_PROGRAMADO (id) VALUES (2)");

        // Assert
        filas.Should().ContainSingle().Which.Should().Be(1);
        (await segunda.Should().ThrowAsync<Exception>())
            .Which.GetBaseException().Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("CK_MANTENIMIENTO_PROGRAMADO_FilaUnica");
    }

    /// <summary>
    /// Simula el paso del tiempo escribiendo un último latido anterior (la hora la sigue poniendo el servidor).
    /// </summary>
    private async Task EnvejecerAsync(int idUsuario, int minutos)
    {
        await Contexto.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE SESIONES_ACTIVAS SET ultimo_latido = DATEADD(minute, {-minutos}, SYSUTCDATETIME()) WHERE id_usuario = {idUsuario}");
    }
}
