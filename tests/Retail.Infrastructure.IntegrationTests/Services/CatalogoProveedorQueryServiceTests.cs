using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Proveedores;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

public class CatalogoProveedorQueryServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_CatQ_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private CatalogoProveedorQueryService _sut = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        _sut = new CatalogoProveedorQueryService(_context);
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

    /// <summary>
    /// SQL Server admite como máximo 2.100 parámetros por consulta. Este test verifica que buscar más códigos
    /// que ese límite funciona: EF Core 8 envía la lista como un único parámetro JSON (OPENJSON).
    /// Protege ante un cambio de versión de EF Core que modifique esa traducción (handoff, sección 6.2).
    /// </summary>
    [Fact]
    public async Task ObtenerMapaPorCodigosProveedorAsync_ConMasDe2100Codigos_RetornaCoincidenciasSinExcepcion()
    {
        // Arrange: 2.500 ítems persistidos para un proveedor
        const int existentes = 2500;
        var proveedor = Proveedor.Crear("Distribuidora Sur", "30-12345678-1");
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        _context.CatalogosProveedores.AddRange(Enumerable.Range(1, existentes).Select(i => new CatalogoProveedor
        {
            IdProveedor = proveedor.Id,
            CodigoProveedor = $"COD-{i:D5}",
            DescripcionProveedor = $"Producto {i}",
            CostoReposicion = 100m
        }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // 3.000 códigos buscados: los 2.500 existentes más 500 que no existen
        var codigosBuscados = Enumerable.Range(1, 3000).Select(i => $"COD-{i:D5}").ToList();

        // Act
        var mapa = await _sut.ObtenerMapaPorCodigosProveedorAsync(proveedor.Id, codigosBuscados);

        // Assert
        mapa.Should().HaveCount(existentes);
        mapa.Should().ContainKey("COD-00001").And.ContainKey($"COD-{existentes:D5}");
        mapa.Should().NotContainKey("COD-02501");
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_TerminoSinAcentos_EncuentraLaDescripcionAcentuadaYRespetaLaEnie()
    {
        // Arrange: descripcion_proveedor usa Modern_Spanish_CI_AI
        var proveedor = Proveedor.Crear("Distribuidora Norte", "30-87654321-0");
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        _context.CatalogosProveedores.AddRange(
            new CatalogoProveedor { IdProveedor = proveedor.Id, CodigoProveedor = "A1", DescripcionProveedor = "Compás escolar metálico", CostoReposicion = 100m },
            new CatalogoProveedor { IdProveedor = proveedor.Id, CodigoProveedor = "A2", DescripcionProveedor = "Agenda año 2027", CostoReposicion = 100m });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var sinAcentos = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id, TerminoBusqueda = "COMPAS ESCOLAR" });
        var conEnie = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id, TerminoBusqueda = "año" });
        var sinEnie = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id, TerminoBusqueda = "ano" });

        // Assert
        sinAcentos.Items.Should().ContainSingle(i => i.CodigoProveedor == "A1");
        conEnie.Items.Should().ContainSingle(i => i.CodigoProveedor == "A2");
        sinEnie.Items.Should().BeEmpty("en castellano la ñ es una letra distinta de la n");
    }
}
