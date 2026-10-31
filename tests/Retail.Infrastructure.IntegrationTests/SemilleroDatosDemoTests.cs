using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express los datos de <c>Retail.Cli datos-demo</c>: proveedor, rubros con sus categorías y marcas, sin
/// usuarios ni artículos, e idempotente elemento por elemento.
/// </summary>
public class SemilleroDatosDemoTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Demo_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
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
    public async Task CargarAsync_BaseRecienMigrada_CargaProveedorRubrosCategoriasYMarcasSinUsuariosNiArticulos()
    {
        // Act
        var agregados = await SemilleroDatosDemo.CargarAsync(_context);

        // Assert
        agregados.Should().Be(
            SemilleroDatosDemo.RubrosConCategorias.Count
            + SemilleroDatosDemo.Categorias.Count()
            + SemilleroDatosDemo.Marcas.Count
            + 1);
        (await _context.Rubros.Select(r => r.NombreRubro).ToListAsync())
            .Should().BeEquivalentTo(SemilleroDatosDemo.RubrosConCategorias.Select(r => r.Rubro));
        (await _context.Categorias.Select(c => c.NombreCategoria).ToListAsync())
            .Should().BeEquivalentTo(SemilleroDatosDemo.Categorias);

        // Cada categoría cuelga del rubro que le corresponde (D-25).
        var rubroPorCategoria = await _context.Categorias
            .Join(_context.Rubros, c => c.IdRubro, r => r.Id, (c, r) => new { c.NombreCategoria, r.NombreRubro })
            .ToDictionaryAsync(x => x.NombreCategoria, x => x.NombreRubro);
        rubroPorCategoria.Should().BeEquivalentTo(
            SemilleroDatosDemo.RubrosConCategorias
                .SelectMany(r => r.Categorias.Select(c => new KeyValuePair<string, string>(c, r.Rubro))));
        (await _context.Marcas.Select(m => m.NombreMarca).ToListAsync())
            .Should().BeEquivalentTo(SemilleroDatosDemo.Marcas);

        var proveedor = await _context.Proveedores.SingleAsync();
        proveedor.Cuit.Should().Be(SemilleroDatosDemo.CuitProveedorDemo); // forma canónica, normalizada por el agregado
        proveedor.Telefono.Should().Be("01145550000");

        // Los usuarios los crea "inicializar"; los artículos, la importación de la planilla del proveedor.
        (await _context.Usuarios.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await _context.Articulos.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CargarAsync_EjecucionRepetidaConDatosDelUsuario_SoloAgregaLoQueFaltaSinDuplicar()
    {
        // Arrange: el usuario ya cargó uno de los rubros y una de las categorías (con otras mayúsculas), y una propia.
        var rubro = Rubro.Crear("LIBRERÍA");
        await _context.Rubros.AddAsync(rubro);
        await _context.SaveChangesAsync();
        await _context.Categorias.AddRangeAsync(
            Categoria.Crear("ESCRITURA", rubro.Id),
            Categoria.Crear("Papelería Fina", rubro.Id));
        await _context.SaveChangesAsync();

        // Act
        await SemilleroDatosDemo.CargarAsync(_context);
        var segundaEjecucion = await SemilleroDatosDemo.CargarAsync(_context);

        // Assert
        segundaEjecucion.Should().Be(0);
        (await _context.Rubros.CountAsync()).Should().Be(SemilleroDatosDemo.RubrosConCategorias.Count);
        (await _context.Categorias.CountAsync()).Should().Be(SemilleroDatosDemo.Categorias.Count() + 1);
        (await _context.Proveedores.CountAsync()).Should().Be(1);
    }
}
