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

    [Fact]
    public async Task UsuarioRepository_BusquedaInsensibleAMayusculas_DebeTraducirseASqlCorrectamente()
    {
        // Arrange
        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.NombreRol == "Cajero");
        if (rol == null)
        {
            rol = new Rol { NombreRol = "Cajero" };
            await _context.Roles.AddAsync(rol);
            await _context.SaveChangesAsync();
        }

        var repo = new Repository<Usuario>(_context);
        var uow = new UnitOfWork(_context);

        var usuario = new Usuario
        {
            NombreUsuario = "operador.test",
            NombreCompleto = "Operador de Prueba",
            PasswordHash = "hash123",
            IdRol = rol.Id
        };

        await repo.AddAsync(usuario);
        await uow.SaveChangesAsync();

        // Act: Búsqueda con expresión LINQ traducible a SQL
        var username = "OPERADOR.TEST";
        var encontrados = await repo.FindAsync(
            u => u.NombreUsuario == username,
            includeDeleted: true);

        // Assert
        encontrados.Should().NotBeEmpty();
        encontrados.Should().ContainSingle(u => u.NombreUsuario == "operador.test");
    }

    [Fact]
    public async Task Usuario_NombreUsuarioSoftDeleted_PermiteNuevoUsuarioConMismoLoginEIdentidadDistinta()
    {
        // Arrange
        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.NombreRol == "Cajero");
        if (rol == null)
        {
            rol = new Rol { NombreRol = "Cajero" };
            await _context.Roles.AddAsync(rol);
            await _context.SaveChangesAsync();
        }

        var usuario1 = new Usuario
        {
            NombreUsuario = "lucas.softdelete",
            NombreCompleto = "Lucas Anterior",
            PasswordHash = "hash1",
            IdRol = rol.Id
        };
        await _context.Usuarios.AddAsync(usuario1);
        await _context.SaveChangesAsync();

        // Soft delete del primer usuario
        _context.Usuarios.Remove(usuario1);
        await _context.SaveChangesAsync();

        // Act: Insertar un segundo usuario independiente con el mismo nombre de usuario
        var usuario2 = new Usuario
        {
            NombreUsuario = "lucas.softdelete",
            NombreCompleto = "Lucas Nuevo",
            PasswordHash = "hash2",
            IdRol = rol.Id
        };
        await _context.Usuarios.AddAsync(usuario2);
        await _context.SaveChangesAsync();

        // Assert
        usuario1.Id.Should().BeGreaterThan(0);
        usuario2.Id.Should().BeGreaterThan(usuario1.Id);
        usuario2.NombreCompleto.Should().Be("Lucas Nuevo");

        // Verificación con IgnoreQueryFilters: ambos registros existen en SQL con claves primarias independientes
        var todos = await _context.Usuarios.IgnoreQueryFilters().Where(u => u.NombreUsuario == "lucas.softdelete").ToListAsync();
        todos.Should().HaveCount(2);
        todos.Should().ContainSingle(u => u.Id == usuario1.Id && u.DeletedAt != null);
        todos.Should().ContainSingle(u => u.Id == usuario2.Id && u.DeletedAt == null);
    }

    [Fact]
    public async Task Usuario_DosUsuariosActivosConMismoNombre_DebeLanzarExcepcionPorIndiceFiltrado()
    {
        // Arrange
        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.NombreRol == "Cajero");
        if (rol == null)
        {
            rol = new Rol { NombreRol = "Cajero" };
            await _context.Roles.AddAsync(rol);
            await _context.SaveChangesAsync();
        }

        var usuario1 = new Usuario
        {
            NombreUsuario = "operador.duplicado",
            NombreCompleto = "Operador Uno",
            PasswordHash = "hash1",
            IdRol = rol.Id
        };
        await _context.Usuarios.AddAsync(usuario1);
        await _context.SaveChangesAsync();

        var usuario2 = new Usuario
        {
            NombreUsuario = "operador.duplicado",
            NombreCompleto = "Operador Dos",
            PasswordHash = "hash2",
            IdRol = rol.Id
        };
        await _context.Usuarios.AddAsync(usuario2);

        // Act
        var act = async () => await _context.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Articulo_CodigoBarrasSoftDeleted_PermiteNuevoArticuloConMismoCodigo()
    {
        // Arrange
        var categoria = new Categoria { NombreCategoria = "Librería General" };
        var marca = new Marca { NombreMarca = "Faber" };
        await _context.Categorias.AddAsync(categoria);
        await _context.Marcas.AddAsync(marca);
        await _context.SaveChangesAsync();

        const string codigoCompartido = "7791234567890";

        var articulo1 = new Articulo
        {
            CodigoBarras = codigoCompartido,
            Descripcion = "Resaltador Amarillo Original",
            IdCategoria = categoria.Id,
            IdMarca = marca.Id,
            CostoReposicion = 300m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 450m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        await _context.Articulos.AddAsync(articulo1);
        await _context.SaveChangesAsync();

        // Soft delete del primer artículo
        _context.Articulos.Remove(articulo1);
        await _context.SaveChangesAsync();

        // Act: Insertar un nuevo artículo activo con el mismo código de barras
        var articulo2 = new Articulo
        {
            CodigoBarras = codigoCompartido,
            Descripcion = "Resaltador Amarillo Nuevo Lote",
            IdCategoria = categoria.Id,
            IdMarca = marca.Id,
            CostoReposicion = 350m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 525m,
            StockActual = 25,
            StockMinimo = 5,
            EsServicio = false
        };

        await _context.Articulos.AddAsync(articulo2);
        var act = async () => await _context.SaveChangesAsync();

        // Assert
        await act.Should().NotThrowAsync();
        articulo1.Id.Should().BeGreaterThan(0);
        articulo2.Id.Should().BeGreaterThan(articulo1.Id);

        // Con IgnoreQueryFilters() ambos registros coexisten en la base de datos
        var articulosEnDb = await _context.Articulos
            .IgnoreQueryFilters()
            .Where(a => a.CodigoBarras == codigoCompartido)
            .ToListAsync();

        articulosEnDb.Should().HaveCount(2);
        articulosEnDb.Should().ContainSingle(a => a.Id == articulo1.Id && a.DeletedAt != null);
        articulosEnDb.Should().ContainSingle(a => a.Id == articulo2.Id && a.DeletedAt == null);
    }

    [Fact]
    public async Task Articulo_CategoriaYMarcaNulas_PermiteInsercionYPersistenciaCorrecta()
    {
        // Arrange
        var articuloSinRubroNiMarca = new Articulo
        {
            CodigoBarras = "7790000000001",
            Descripcion = "Producto Sin Categoría Ni Marca",
            IdCategoria = null,
            IdMarca = null,
            CostoReposicion = 250m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 350m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        // Act
        await _context.Articulos.AddAsync(articuloSinRubroNiMarca);
        await _context.SaveChangesAsync();

        // Assert
        var recuperado = await _context.Articulos
            .Include(a => a.Categoria)
            .Include(a => a.Marca)
            .FirstOrDefaultAsync(a => a.Id == articuloSinRubroNiMarca.Id);

        recuperado.Should().NotBeNull();
        recuperado!.IdCategoria.Should().BeNull();
        recuperado.Categoria.Should().BeNull();
        recuperado.IdMarca.Should().BeNull();
        recuperado.Marca.Should().BeNull();
    }
}
