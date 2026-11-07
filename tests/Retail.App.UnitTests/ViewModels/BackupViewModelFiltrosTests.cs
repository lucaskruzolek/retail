using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Backups;
using Retail.Application.DTOs.Backups;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>Filtros de columna de las grillas de Copias y Restauraciones (se aplican sobre las listas ya cargadas).</summary>
public class BackupViewModelFiltrosTests : IDisposable
{
    private const int IdActor = 7;

    private readonly IBackupService _backupService = Substitute.For<IBackupService>();
    private readonly IRestauracionService _restauracionService = Substitute.For<IRestauracionService>();
    private readonly BackupViewModel _sut;

    public BackupViewModelFiltrosTests()
    {
        var session = Substitute.For<ICurrentUserSession>();
        session.IdUsuario.Returns(IdActor);
        session.EsGerente.Returns(true);

        _backupService.ListarAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new List<RegistroBackupDto>
        {
            Copia(TipoBackupEnum.Manual, EstadoBackupEnum.Verificado, "gerente", new DateTime(2026, 10, 30, 15, 0, 0)),
            Copia(TipoBackupEnum.Automatico, EstadoBackupEnum.Verificado, "Sistema", new DateTime(2026, 10, 29, 3, 0, 0)),
            Copia(TipoBackupEnum.Automatico, EstadoBackupEnum.Fallido, "Sistema", new DateTime(2026, 9, 12, 3, 0, 0))
        });
        _restauracionService.ObtenerUltimaRestauracionAsync(IdActor, Arg.Any<CancellationToken>()).Returns((RegistroRestauracionDto?)null);
        _restauracionService.ListarRestauracionesAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new List<RegistroRestauracionDto>
        {
            Restauracion(OrigenRestauracionEnum.Aplicacion, "pfernandez", new DateTime(2026, 10, 27, 11, 0, 0)),
            Restauracion(OrigenRestauracionEnum.Consola, null, new DateTime(2026, 8, 2, 11, 0, 0))
        });

        _sut = new BackupViewModel(
            _backupService,
            _restauracionService,
            Substitute.For<IBackupDialogService>(),
            session,
            Substitute.For<IReinicioAplicacion>());
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    private static RegistroBackupDto Copia(TipoBackupEnum tipo, EstadoBackupEnum estado, string operador, DateTime fechaUtc) => new()
    {
        IdRegistroBackup = 1,
        Referencia = Guid.NewGuid(),
        Tipo = tipo,
        Estado = estado,
        Operador = operador,
        FechaInicio = fechaUtc,
        RutaArchivo = @"C:\RetailData\Backups\Retail.bak",
        TamanoBytes = 2048,
        EsRestaurable = estado == EstadoBackupEnum.Verificado
    };

    private static RegistroRestauracionDto Restauracion(OrigenRestauracionEnum origen, string? operador, DateTime fechaUtc) => new()
    {
        Referencia = Guid.NewGuid(),
        Fecha = fechaUtc,
        Origen = origen,
        NombreUsuario = "gerente",
        NombreOperador = operador,
        ReferenciaCopia = Guid.NewGuid(),
        RutaArchivo = @"C:\RetailData\Backups\vieja.bak",
        TuvoCopiaPrevia = true,
        BaseAnterior = "Retail_anterior"
    };

    [Fact]
    public async Task FiltroCopiaTipo_ConAutomatico_MuestraSoloLasAutomaticas()
    {
        await _sut.CargarAsync();

        _sut.FiltroCopiaTipo.AplicarIds([(int)TipoBackupEnum.Automatico]);

        _sut.Copias.Should().HaveCount(2).And.OnlyContain(c => c.Tipo == TipoBackupEnum.Automatico);
        _sut.HayFiltrosActivos.Should().BeTrue();
    }

    [Fact]
    public async Task FiltroCopiaEstadoYOperador_Combinados_SeCombinanConY()
    {
        await _sut.CargarAsync();

        _sut.FiltroCopiaEstado.AplicarIds([(int)EstadoBackupEnum.Verificado]);
        var sistema = _sut.FiltroCopiaOperador.Filtro.Grupos.SelectMany(g => g.Opciones).Single(o => o.Nombre == "Sistema");
        _sut.FiltroCopiaOperador.Filtro.AplicarIds([sistema.Id]);

        _sut.Copias.Should().ContainSingle().Which.Operador.Should().Be("Sistema");
    }

    [Fact]
    public async Task FiltroCopiaFecha_ConRango_MuestraSoloLasDeEsosDias()
    {
        await _sut.CargarAsync();
        var fechaLocal = _sut.Copias.Single(c => c.Tipo == TipoBackupEnum.Manual).FechaLocal.Date;

        _sut.FiltroCopiaFecha.BorradorDesde = fechaLocal;
        _sut.FiltroCopiaFecha.BorradorHasta = fechaLocal;
        _sut.FiltroCopiaFecha.AplicarCommand.Execute(null);

        _sut.Copias.Should().ContainSingle().Which.Tipo.Should().Be(TipoBackupEnum.Manual);
    }

    [Fact]
    public async Task LimpiarFiltros_ConFiltrosEnCopias_RestauraLaListaCompletaYSeDeshabilita()
    {
        await _sut.CargarAsync();
        _sut.FiltroCopiaTipo.AplicarIds([(int)TipoBackupEnum.Manual]);
        _sut.Copias.Should().HaveCount(1);

        _sut.LimpiarFiltrosCommand.Execute(null);

        _sut.Copias.Should().HaveCount(3);
        _sut.HayFiltrosActivos.Should().BeFalse();
        _sut.LimpiarFiltrosCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task FiltroRestauracionOrigen_ConConsola_MuestraSoloEsa()
    {
        await _sut.CargarAsync();

        _sut.FiltroRestauracionOrigen.AplicarIds([(int)OrigenRestauracionEnum.Consola]);

        _sut.Restauraciones.Should().ContainSingle().Which.Dto.Origen.Should().Be(OrigenRestauracionEnum.Consola);
    }

    [Fact]
    public async Task FiltroRestauracionEjecutadaPor_ConUnOperador_MuestraSoloSusRestauraciones()
    {
        await _sut.CargarAsync();
        var opcion = _sut.FiltroRestauracionEjecutadaPor.Filtro.Grupos.SelectMany(g => g.Opciones).Single(o => o.Nombre == "pfernandez");

        _sut.FiltroRestauracionEjecutadaPor.Filtro.AplicarIds([opcion.Id]);

        _sut.Restauraciones.Should().ContainSingle().Which.EjecutadaPor.Should().Be("pfernandez");
    }

    [Fact]
    public async Task Restauraciones_ConFiltrosQueNoDejanNinguna_AvisaQueNingunaCoincide()
    {
        await _sut.CargarAsync();

        _sut.FiltroRestauracionOrigen.AplicarIds([(int)OrigenRestauracionEnum.Consola]);
        _sut.FiltroRestauracionFecha.BorradorDesde = new DateTime(2030, 1, 1);
        _sut.FiltroRestauracionFecha.AplicarCommand.Execute(null);

        _sut.Restauraciones.Should().BeEmpty();
        _sut.NoHayRestauraciones.Should().BeTrue();
        _sut.MensajeSinRestauraciones.Should().Be("Ninguna restauración coincide con los filtros");
    }

    [Fact]
    public async Task Copias_SiLaSeleccionQuedaFueraDelFiltro_SeQuitaLaSeleccion()
    {
        await _sut.CargarAsync();
        _sut.CopiaSeleccionada = _sut.Copias.Single(c => c.Tipo == TipoBackupEnum.Manual);

        _sut.FiltroCopiaTipo.AplicarIds([(int)TipoBackupEnum.Automatico]);

        _sut.CopiaSeleccionada.Should().BeNull("Restaurar no debe actuar sobre una fila que ya no se ve");
    }
}
