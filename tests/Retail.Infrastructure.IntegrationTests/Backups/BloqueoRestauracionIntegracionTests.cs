using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Exceptions;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Bloqueo de lectores y escritor contra LocalDB real: el <c>sp_getapplock</c> vive en <c>master</c>, así que no hace
/// falta que la base exista; el nombre de la base solo define el recurso, y por eso cada prueba usa el suyo.
/// </summary>
public class BloqueoRestauracionIntegracionTests : IDisposable
{
    private readonly RetailDbContext _context;
    private readonly SqlServerBloqueoRestauracion _sut;

    public BloqueoRestauracionIntegracionTests()
    {
        var cadena = $"Server=(localdb)\\mssqllocaldb;Database=RetailDb_Test_Bloqueo_{Guid.NewGuid():N};Trusted_Connection=True";
        _context = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(cadena).Options);
        _sut = new SqlServerBloqueoRestauracion(_context);
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
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database=RetailDb_Test_Bloqueo_{Guid.NewGuid():N};Trusted_Connection=True").Options);
        var otro = new SqlServerBloqueoRestauracion(otroContexto);
        await using var primero = await _sut.AdquirirExclusivoAsync();

        // Act
        await using var segundo = await otro.AdquirirExclusivoAsync();

        // Assert
        primero.Should().NotBeNull();
        segundo.Should().NotBeNull();
    }
}
