using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica los índices que permiten paginar el catálogo de proveedores y de artículos ordenados por descripción
/// sin ordenar en memoria (Ley 8, RNF-02): existen en el modelo y en la base migrada.
/// </summary>
public class IndicesPaginacionTests : IAsyncLifetime, IDisposable
{
    private const string FiltroBorradoLogico = "[deleted_at] IS NULL";

    private readonly string _dbName = $"RetailDb_Test_IdxPag_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(options);
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

    [Fact]
    public void Modelo_CatalogoProveedor_TieneIndiceNoUnicoPorProveedorYDescripcionConFiltroDeBorrado()
    {
        var indice = _context.Model.FindEntityType(typeof(CatalogoProveedor))!
            .GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(CatalogoProveedor.IdProveedor), nameof(CatalogoProveedor.DescripcionProveedor) }));

        indice.IsUnique.Should().BeFalse();
        indice.GetFilter().Should().Be(FiltroBorradoLogico);
    }

    [Fact]
    public void Modelo_Articulo_TieneIndiceNoUnicoPorDescripcionConFiltroDeBorrado()
    {
        var indice = _context.Model.FindEntityType(typeof(Articulo))!
            .GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Articulo.Descripcion) }));

        indice.IsUnique.Should().BeFalse();
        indice.GetFilter().Should().Be(FiltroBorradoLogico);
    }

    [Fact]
    public async Task BaseMigrada_TieneLosDosIndicesDePaginacionFiltradosYNoUnicos()
    {
        var indices = await _context.Database.SqlQueryRaw<IndiceFisico>(
            """
            SELECT i.name AS Nombre, i.is_unique AS EsUnico, i.filter_definition AS Filtro
            FROM sys.indexes i
            WHERE i.name IN ('IX_CATALOGOS_PROVEEDORES_id_proveedor_descripcion_proveedor', 'IX_ARTICULOS_descripcion')
            """).ToListAsync();

        indices.Should().HaveCount(2);
        indices.Should().OnlyContain(i => !i.EsUnico);
        indices.Should().OnlyContain(i => i.Filtro != null && i.Filtro.Contains("deleted_at"));
    }

    public sealed class IndiceFisico
    {
        public string Nombre { get; set; } = string.Empty;

        public bool EsUnico { get; set; }

        public string? Filtro { get; set; }
    }
}
