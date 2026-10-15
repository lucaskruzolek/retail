using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Exceptions;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Bloqueo de lectores y escritor contra SQL Server Express real: el <c>sp_getapplock</c> vive en <c>master</c>, así que no hace
/// falta que la base exista; el nombre de la base solo define el recurso, y por eso cada prueba usa el suyo.
/// </summary>
public class BloqueoRestauracionIntegracionTests : IDisposable
{
    // El motor muestra en sys.dm_tran_locks solo los primeros 32 caracteres del nombre de un bloqueo de aplicación
    // ("Retail.Restauracion." ocupa 20): lo que distingue esta base va primero para poder reconocer su sesión.
    private readonly string _nombreBase = $"{Guid.NewGuid():N}_RetailDb_Test_Bloqueo";
    private readonly RetailDbContext _context;
    private readonly SqlServerBloqueoRestauracion _sut;

    public BloqueoRestauracionIntegracionTests()
    {
        var cadena = ServidorDePruebas.CadenaDeConexion(_nombreBase);
        _context = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(cadena).Options);

        // Espera corta del compartido: las pruebas que lo rechazan no deben tardar los 10 segundos de producción.
        _sut = new SqlServerBloqueoRestauracion(_context, TimeSpan.FromMilliseconds(300));
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AdquirirExclusivoAsync_ConOtroExclusivoTomado_LanzaRestauracionFallida()
    {
        // Arrange
        await using var primero = await _sut.AdquirirExclusivoAsync();

        // Act
        var act = () => _sut.AdquirirExclusivoAsync();

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*en curso*");
    }

    [Fact]
    public async Task AdquirirCompartidoAsync_ConExclusivoTomado_LanzaBackupFallido()
    {
        // Arrange: una restauración en curso.
        await using var restauracion = await _sut.AdquirirExclusivoAsync();

        // Act
        var act = () => _sut.AdquirirCompartidoAsync();

        // Assert
        await act.Should().ThrowAsync<BackupFallidoException>().WithMessage("*restauración*");
    }

    [Fact]
    public async Task AdquirirExclusivoAsync_ConUnCompartidoTomado_LanzaRestauracionFallida()
    {
        // Arrange: una copia en curso impide restaurar.
        await using var copia = await _sut.AdquirirCompartidoAsync();

        // Act
        var act = () => _sut.AdquirirExclusivoAsync();

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>();
    }

    [Fact]
    public async Task IntentarAdquirirExclusivoAsync_SinNadieMas_DevuelveElBloqueo()
    {
        // Act
        await using var exclusivo = await _sut.IntentarAdquirirExclusivoAsync();

        // Assert
        exclusivo.Should().NotBeNull();
    }

    [Fact]
    public async Task IntentarAdquirirExclusivoAsync_ConOtroExclusivoTomado_DevuelveNulo()
    {
        // Arrange
        await using var restauracion = await _sut.AdquirirExclusivoAsync();

        // Act
        var intento = await _sut.IntentarAdquirirExclusivoAsync();

        // Assert
        intento.Should().BeNull();
    }

    [Fact]
    public async Task IntentarAdquirirExclusivoAsync_ConUnCompartidoTomado_DevuelveNulo()
    {
        // Arrange: una copia real en curso.
        await using var copia = await _sut.AdquirirCompartidoAsync();

        // Act
        var intento = await _sut.IntentarAdquirirExclusivoAsync();

        // Assert
        intento.Should().BeNull();
    }

    [Fact]
    public async Task IntentarAdquirirExclusivoAsync_LiberadoElCompartido_DevuelveElBloqueo()
    {
        // Arrange: la copia terminó (o su proceso murió y el motor soltó el bloqueo).
        var copia = await _sut.AdquirirCompartidoAsync();
        await copia.DisposeAsync();

        // Act
        await using var exclusivo = await _sut.IntentarAdquirirExclusivoAsync();

        // Assert
        exclusivo.Should().NotBeNull();
    }

    [Fact]
    public async Task AdquirirCompartidoAsync_DosALaVez_AmbosSeConceden()
    {
        // Act
        await using var primero = await _sut.AdquirirCompartidoAsync();
        await using var segundo = await _sut.AdquirirCompartidoAsync();

        // Assert
        primero.Should().NotBeNull();
        segundo.Should().NotBeNull();
    }

    [Fact]
    public async Task AdquirirCompartidoAsync_LiberadoElExclusivo_SeConcede()
    {
        // Arrange
        var restauracion = await _sut.AdquirirExclusivoAsync();
        await restauracion.DisposeAsync();

        // Act
        await using var copia = await _sut.AdquirirCompartidoAsync();

        // Assert
        copia.Should().NotBeNull();
    }

    [Fact]
    public async Task AdquirirCompartidoAsync_ConExclusivoTomado_ElMensajeNoAcusaSoloALaRestauracion()
    {
        // Arrange: el exclusivo puede ser el de otra copia que está cerrando huérfanos (B-0), no una restauración.
        await using var exclusivo = await _sut.AdquirirExclusivoAsync();

        // Act
        var act = () => _sut.AdquirirCompartidoAsync();

        // Assert
        await act.Should().ThrowAsync<BackupFallidoException>()
            .WithMessage("*restauración de la base u otra copia iniciándose*Intente de nuevo en unos segundos*");
    }

    [Fact]
    public async Task AdquirirCompartidoAsync_ElExclusivoSeLiberaDentroDeLaEspera_SeConcede()
    {
        // Arrange: la ventana de B-0 de otra copia dura milisegundos; el compartido espera en lugar de fallar.
        var conEspera = new SqlServerBloqueoRestauracion(_context, TimeSpan.FromSeconds(10));
        var exclusivo = await _sut.AdquirirExclusivoAsync();

        // Act
        var pendiente = conEspera.AdquirirCompartidoAsync();
        await Task.Delay(500);
        var terminoSinEsperar = pendiente.IsCompleted;
        await exclusivo.DisposeAsync();
        await using var copia = await pendiente;

        // Assert
        terminoSinEsperar.Should().BeFalse("debe esperar al exclusivo y no fallar en el acto");
        copia.Should().NotBeNull();
    }

    [Fact]
    public async Task IntentarAdquirirExclusivoAsync_ElProcesoDeLaCopiaMuere_ElMotorSueltaElCompartidoYSeObtiene()
    {
        // Arrange: B-0 se apoya en esto. Una copia sostiene el compartido; "morir" es perder su sesión en el motor (KILL),
        // que es lo que le pasa a una copia cuando su proceso termina de golpe o se corta la PC.
        var copia = await _sut.AdquirirCompartidoAsync();
        var antesDeMorir = await _sut.IntentarAdquirirExclusivoAsync();

        var sesion = await BuscarSesionQueSostieneElBloqueoAsync();

        // Act
        await using (var master = new SqlConnection(ServidorDePruebas.CadenaDeConexion("master")))
        {
            await master.OpenAsync();
            await using var matar = master.CreateCommand();
            matar.CommandText = $"KILL {sesion};";
            await matar.ExecuteNonQueryAsync();
        }

        // La sesión muerta tarda unos instantes en retirarse del motor: se espera hasta 10 segundos.
        IAsyncDisposable? despuesDeMorir = null;
        for (var intento = 0; intento < 40 && despuesDeMorir is null; intento++)
        {
            despuesDeMorir = await _sut.IntentarAdquirirExclusivoAsync();
            if (despuesDeMorir is null)
            {
                await Task.Delay(250);
            }
        }

        // Assert
        antesDeMorir.Should().BeNull("con la copia viva, el exclusivo no está disponible");
        despuesDeMorir.Should().NotBeNull("muerta la sesión de la copia, el motor soltó su compartido sin que nadie lo libere");

        if (despuesDeMorir is not null)
        {
            await despuesDeMorir.DisposeAsync();
        }

        // Liberar la copia ya muerta no debe lanzar.
        var liberar = async () => await copia.DisposeAsync();
        await liberar.Should().NotThrowAsync();
    }

    private async Task<int> BuscarSesionQueSostieneElBloqueoAsync()
    {
        await using var master = new SqlConnection(ServidorDePruebas.CadenaDeConexion("master"));
        await master.OpenAsync();
        await using var consulta = master.CreateCommand();
        consulta.CommandText =
            "SELECT TOP (1) request_session_id FROM sys.dm_tran_locks " +
            "WHERE resource_type = 'APPLICATION' AND resource_description LIKE @patron;";
        consulta.Parameters.AddWithValue("@patron", $"%Retail.Restauracion.{_nombreBase[..12]}%");

        var sesion = await consulta.ExecuteScalarAsync();
        sesion.Should().NotBeNull("la copia debe figurar con un bloqueo de aplicación en el motor");
        return Convert.ToInt32(sesion, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task DisposeAsync_LlamadoDosVeces_NoLanza()
    {
        // Arrange
        var bloqueo = await _sut.AdquirirExclusivoAsync();
        await bloqueo.DisposeAsync();

        // Act
        var act = async () => await bloqueo.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AdquirirExclusivoAsync_BasesDistintas_NoSeBloqueanEntreSi()
    {
        // Arrange: otra base (otro recurso) en el mismo servidor.
        using var otroContexto = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion($"RetailDb_Test_Bloqueo_{Guid.NewGuid():N}")).Options);
        var otro = new SqlServerBloqueoRestauracion(otroContexto);
        await using var primero = await _sut.AdquirirExclusivoAsync();

        // Act
        await using var segundo = await otro.AdquirirExclusivoAsync();

        // Assert
        primero.Should().NotBeNull();
        segundo.Should().NotBeNull();
    }
}
