using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Configuración comercial global (D-38) contra SQL Server Express: la migración siembra la fila única, el servicio la
/// cambia y la persiste, y el CHECK rechaza una política que el Dominio no conoce.
/// </summary>
public class ConfiguracionComercialPersistenciaTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_ConfigCom_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = CrearContexto();
        await _context.Database.CrearDesdePlantillaAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    private RetailDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        return new RetailDbContext(options);
    }

    [Fact]
    public async Task Migracion_SiembraLaFilaUnicaConEnteroMasCercano()
    {
        // Act
        var filas = await _context.ConfiguracionesComerciales.AsNoTracking().ToListAsync();

        // Assert
        filas.Should().ContainSingle();
        filas[0].Id.Should().Be(ConfiguracionComercial.IdUnico);
        filas[0].RedondeoPrecio.Should().Be(RedondeoPrecioEnum.EnteroMasCercano);
    }

    [Fact]
    public async Task CambiarRedondeoPrecioAsync_PorUnGerente_LoPersisteYOtroContextoLoLee()
    {
        // Arrange
        var gerente = Usuario.Crear("gerente.config", "Ana", "López", "hash", RolUsuarioEnum.Gerente, "ana@libreria.com");
        _context.Usuarios.Add(gerente);
        await _context.SaveChangesAsync();

        var servicio = new ConfiguracionComercialService(
            new Repository<ConfiguracionComercial>(_context),
            new Repository<Usuario>(_context),
            new UnitOfWork(_context));

        // Act
        await servicio.CambiarRedondeoPrecioAsync(gerente.Id, RedondeoPrecioEnum.MultiploDe10Superior);

        // Assert: otra terminal (otro contexto) ve la política nueva
        await using var otraTerminal = CrearContexto();
        var servicioDeOtraTerminal = new ConfiguracionComercialService(
            new Repository<ConfiguracionComercial>(otraTerminal),
            new Repository<Usuario>(otraTerminal),
            new UnitOfWork(otraTerminal));
        var leida = await servicioDeOtraTerminal.ObtenerRedondeoPrecioAsync();
        leida.Should().Be(RedondeoPrecioEnum.MultiploDe10Superior);
    }

    [Fact]
    public async Task RedondeoPrecio_ValorDesconocido_LoRechazaElCheck()
    {
        // Act: un script externo que salta el Dominio
        var accion = () => _context.Database.ExecuteSqlRawAsync(
            "UPDATE CONFIGURACION_COMERCIAL SET redondeo_precio = 'MultiploDe3' WHERE id_configuracion_comercial = 1");

        // Assert
        (await accion.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>())
            .Which.Message.Should().Contain("CK_CONFIGURACION_COMERCIAL_RedondeoPrecio");
    }
}
