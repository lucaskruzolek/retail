using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

public class RetailDbContextTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Ctx_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();
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
    public async Task Articulo_MultiplesCodigosNulos_DebePermitirInsercionPorIndiceFiltrado()
    {
        // Arrange
        var categoria = new Categoria { NombreCategoria = "Artesanías" };
        var marca = new Marca { NombreMarca = "Taller Local" };
        await _context.Categorias.AddAsync(categoria);
        await _context.Marcas.AddAsync(marca);
        await _context.SaveChangesAsync();

        var artesania1 = new Articulo
        {
            CodigoBarras = null,
            Descripcion = "Vasija de Cerámica 1",
            IdCategoria = categoria.Id,
            IdMarca = marca.Id,
            CostoReposicion = 1000m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 1500m,
            StockActual = 5,
            StockMinimo = 1,
            EsServicio = false
        };

        var artesania2 = new Articulo
        {
            CodigoBarras = null,
            Descripcion = "Vasija de Cerámica 2",
            IdCategoria = categoria.Id,
            IdMarca = marca.Id,
            CostoReposicion = 1200m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 1800m,
            StockActual = 3,
            StockMinimo = 1,
            EsServicio = false
        };

        // Act
        await _context.Articulos.AddRangeAsync(artesania1, artesania2);
        var act = async () => await _context.SaveChangesAsync();

        // Assert - Filtered Unique Index no debe rechazar múltiples NULL
        await act.Should().NotThrowAsync();

        var articulosGuardados = await _context.Articulos
            .Where(a => a.CodigoBarras == null)
            .ToListAsync();

        articulosGuardados.Should().HaveCount(2);
    }

    [Fact]
    public async Task Articulo_CodigosDuplicadosNoNulos_DebeLanzarExcepcionDeUnicidad()
    {
        // Arrange
        var categoria = new Categoria { NombreCategoria = "Librería" };
        var marca = new Marca { NombreMarca = "Bic" };
        await _context.Categorias.AddAsync(categoria);
        await _context.Marcas.AddAsync(marca);
        await _context.SaveChangesAsync();

        const string codigoDuplicado = "7799999999999";

        var articulo1 = new Articulo
        {
            CodigoBarras = codigoDuplicado,
            Descripcion = "Bolígrafo Azul Original",
            IdCategoria = categoria.Id,
            IdMarca = marca.Id,
            CostoReposicion = 200m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 300m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        await _context.Articulos.AddAsync(articulo1);
        await _context.SaveChangesAsync();

        var articulo2 = new Articulo
        {
            CodigoBarras = codigoDuplicado,
            Descripcion = "Bolígrafo Azul Duplicado",
            IdCategoria = categoria.Id,
            IdMarca = marca.Id,
            CostoReposicion = 200m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 300m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        await _context.Articulos.AddAsync(articulo2);

        // Act & Assert
        var act = async () => await _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DbContext_SoftDelete_DebeOcultarRegistrosAutomaticamente()
    {
        // Arrange
        var categoria = new Categoria { NombreCategoria = "Mochilas" };
        await _context.Categorias.AddAsync(categoria);
        await _context.SaveChangesAsync();

        var categoriaId = categoria.Id;

        // Act - Eliminar la categoría (el DbContext debe interceptar y aplicar Soft Delete)
        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();

        // Assert 1: La consulta normal (con Global Query Filter) no debe devolver el registro
        var categoriaVisible = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId);
        categoriaVisible.Should().BeNull();

        // Assert 2: La consulta con IgnoreQueryFilters() debe devolver el registro marcado como eliminado
        var categoriaOculta = await _context.Categorias
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == categoriaId);

        categoriaOculta.Should().NotBeNull();
        categoriaOculta!.IsDeleted.Should().BeTrue();
        categoriaOculta.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RepositoryYUnitOfWork_TransaccionExitosa_DebePersistirDatos()
    {
        // Arrange
        var categoria = new Categoria { NombreCategoria = "Oficina" };
        var marca = new Marca { NombreMarca = "Pelikan" };
        await _context.Categorias.AddAsync(categoria);
        await _context.Marcas.AddAsync(marca);
        await _context.SaveChangesAsync();

        var repo = new Repository<Articulo>(_context);
        var uow = new UnitOfWork(_context);

        var nuevoArticulo = new Articulo
        {
            CodigoBarras = "7798888888888",
            Descripcion = "Marcador Permanente Negro",
            IdCategoria = categoria.Id,
            IdMarca = marca.Id,
            CostoReposicion = 500m,
            PorcentajeGanancia = 60m,
            PrecioVenta = 800m,
            StockActual = 50,
            StockMinimo = 10,
            EsServicio = false
        };

        // Act
        await uow.BeginTransactionAsync();
        await repo.AddAsync(nuevoArticulo);
        await uow.CommitTransactionAsync();

        // Assert
        var obtenido = await repo.GetByIdAsync(nuevoArticulo.Id);
        obtenido.Should().NotBeNull();
        obtenido!.Descripcion.Should().Be("Marcador Permanente Negro");

        // Act 2: Soft delete con repositorio
        await repo.DeleteAsync(obtenido);
        await uow.SaveChangesAsync();

        var trasBorrado = await repo.GetByIdAsync(nuevoArticulo.Id);
        trasBorrado.Should().BeNull();
    }
}
