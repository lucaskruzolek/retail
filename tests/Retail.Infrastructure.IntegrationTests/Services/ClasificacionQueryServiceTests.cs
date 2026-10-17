using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;
using Xunit.Abstractions;

namespace Retail.Infrastructure.IntegrationTests.Services;

public class ClasificacionQueryServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_ClasQ_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private readonly ITestOutputHelper _salida;
    private ClasificacionQueryService _sut = null!;

    public ClasificacionQueryServiceTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        _sut = new ClasificacionQueryService(_context);
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

    private async Task<Rubro> CrearRubroAsync(string nombre)
    {
        var rubro = Rubro.Crear(nombre);
        _context.Rubros.Add(rubro);
        await _context.SaveChangesAsync();
        return rubro;
    }

    private async Task<Categoria> CrearCategoriaAsync(string nombre, int idRubro, bool deBaja = false)
    {
        var categoria = Categoria.Crear(nombre, idRubro);
        if (deBaja)
        {
            categoria.MarkAsDeleted();
        }

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        return categoria;
    }

    private async Task<Marca> CrearMarcaAsync(string nombre, bool deBaja = false)
    {
        var marca = Marca.Crear(nombre);
        if (deBaja)
        {
            marca.MarkAsDeleted();
        }

        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();
        return marca;
    }

    private async Task CrearArticuloAsync(string descripcion, int? idCategoria, int? idMarca, bool deBaja = false)
    {
        var articulo = new Articulo
        {
            Descripcion = descripcion,
            IdCategoria = idCategoria,
            IdMarca = idMarca,
            CostoReposicion = 100,
            PorcentajeGanancia = 50,
            PrecioVenta = 150,
            StockActual = 1,
            StockMinimo = 0,
            EsServicio = false
        };
        if (deBaja)
        {
            articulo.MarkAsDeleted();
        }

        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task ListarCategoriasAsync_ConBajasYVariasCategorias_ExcluyeBajasYOrdenaPorNombreConRubro()
    {
        var rubro = await CrearRubroAsync("Librería");
        await CrearCategoriaAsync("Lápices", rubro.Id);
        await CrearCategoriaAsync("Cuadernos", rubro.Id);
        await CrearCategoriaAsync("Obsoleta", rubro.Id, deBaja: true);

        var resultado = await _sut.ListarCategoriasAsync(null);

        resultado.Select(c => c.NombreCategoria).Should().Equal("Cuadernos", "Lápices");
        resultado.Should().OnlyContain(c => c.NombreRubro == "Librería" && c.IdRubro == rubro.Id);
    }

    [Fact]
    public async Task ListarCategoriasAsync_ConArticulosActivosYDeBaja_CantidadEnUsoCuentaAmbos()
    {
        var rubro = await CrearRubroAsync("Librería");
        var cuadernos = await CrearCategoriaAsync("Cuadernos", rubro.Id);
        var lapices = await CrearCategoriaAsync("Lápices", rubro.Id);
        await CrearArticuloAsync("Cuaderno A", cuadernos.Id, null);
        await CrearArticuloAsync("Cuaderno B", cuadernos.Id, null, deBaja: true);

        var resultado = await _sut.ListarCategoriasAsync(null);

        resultado.Single(c => c.IdCategoria == cuadernos.Id).CantidadEnUso.Should().Be(2);
        resultado.Single(c => c.IdCategoria == lapices.Id).CantidadEnUso.Should().Be(0);
    }

    [Theory]
    [InlineData("lapic")]
    [InlineData("LAPI")]
    [InlineData("  lápi  ")]
    public async Task ListarCategoriasAsync_ConFiltroSinAcentosNiMayusculas_EncuentraLapices(string filtro)
    {
        var rubro = await CrearRubroAsync("Librería");
        await CrearCategoriaAsync("Lápices", rubro.Id);
        await CrearCategoriaAsync("Cuadernos", rubro.Id);

        var resultado = await _sut.ListarCategoriasAsync(filtro);

        resultado.Select(c => c.NombreCategoria).Should().Equal("Lápices");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListarCategoriasAsync_ConFiltroEnBlanco_DevuelveTodas(string? filtro)
    {
        var rubro = await CrearRubroAsync("Librería");
        await CrearCategoriaAsync("Lápices", rubro.Id);
        await CrearCategoriaAsync("Cuadernos", rubro.Id);

        var resultado = await _sut.ListarCategoriasAsync(filtro);

        resultado.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListarCategoriasAsync_ConFiltro_SeTraduceAUnaSolaConsultaSql()
    {
        // Se reproduce la composición del servicio para inspeccionar el SQL generado: una única sentencia SELECT
        // con LIKE, ORDER BY y las dos subconsultas correlacionadas (rubro y conteo de artículos).
        var sql = _context.Categorias
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .Where(c => EF.Functions.Like(c.NombreCategoria, "%lapiz%"))
            .OrderBy(c => c.NombreCategoria)
            .Select(c => new
            {
                c.Id,
                NombreRubro = _context.Rubros
                    .Where(r => r.Id == c.IdRubro && r.DeletedAt == null)
                    .Select(r => r.NombreRubro)
                    .FirstOrDefault(),
                CantidadEnUso = _context.Articulos.Count(a => a.IdCategoria == c.Id)
            })
            .ToQueryString();

        _salida.WriteLine(sql);
        sql.Should().Contain("LIKE").And.Contain("COUNT(*)").And.Contain("ORDER BY");
        sql.Should().NotContain("[IsDeleted]");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ListarRubrosAsync_ConCategoriasDeBaja_CantidadEnUsoCuentaSoloActivas()
    {
        var libreria = await CrearRubroAsync("Librería");
        var tecnologia = await CrearRubroAsync("Tecnología");
        await CrearCategoriaAsync("Lápices", libreria.Id);
        await CrearCategoriaAsync("Cuadernos", libreria.Id);
        await CrearCategoriaAsync("Obsoleta", libreria.Id, deBaja: true);

        var resultado = await _sut.ListarRubrosAsync(null);

        resultado.Select(r => r.NombreRubro).Should().Equal("Librería", "Tecnología");
        resultado.Single(r => r.IdRubro == libreria.Id).CantidadEnUso.Should().Be(2);
        resultado.Single(r => r.IdRubro == tecnologia.Id).CantidadEnUso.Should().Be(0);
    }

    [Fact]
    public async Task ListarRubrosAsync_ConFiltroYRubroDeBaja_FiltraYExcluyeBajas()
    {
        await CrearRubroAsync("Librería");
        var baja = Rubro.Crear("Libros viejos");
        baja.MarkAsDeleted();
        _context.Rubros.Add(baja);
        await _context.SaveChangesAsync();

        var resultado = await _sut.ListarRubrosAsync("LIBRERIA");

        resultado.Select(r => r.NombreRubro).Should().Equal("Librería");
        (await _sut.ListarRubrosAsync("libros")).Should().BeEmpty();
    }

    [Fact]
    public async Task ListarMarcasAsync_ConArticulosDeBajaYMarcasDeBaja_CuentaBajasYExcluyeMarcasInactivas()
    {
        var faber = await CrearMarcaAsync("Faber-Castell");
        var bic = await CrearMarcaAsync("Bic");
        await CrearMarcaAsync("Vieja", deBaja: true);
        await CrearArticuloAsync("Lápiz 1", null, faber.Id);
        await CrearArticuloAsync("Lápiz 2", null, faber.Id, deBaja: true);

        var resultado = await _sut.ListarMarcasAsync(null);

        resultado.Select(m => m.NombreMarca).Should().Equal("Bic", "Faber-Castell");
        resultado.Single(m => m.IdMarca == faber.Id).CantidadEnUso.Should().Be(2);
        resultado.Single(m => m.IdMarca == bic.Id).CantidadEnUso.Should().Be(0);
    }

    [Fact]
    public async Task ListarMarcasAsync_ConFiltro_IgnoraAcentosYMayusculas()
    {
        await CrearMarcaAsync("Editorial Atlántida");
        await CrearMarcaAsync("Bic");

        var resultado = await _sut.ListarMarcasAsync("ATLANTIDA");

        resultado.Select(m => m.NombreMarca).Should().Equal("Editorial Atlántida");
    }

    [Fact]
    public async Task ContarArticulosPorCategoriaAsync_ConArticuloDeBaja_LoIncluye()
    {
        var rubro = await CrearRubroAsync("Librería");
        var categoria = await CrearCategoriaAsync("Cuadernos", rubro.Id);
        await CrearArticuloAsync("A", categoria.Id, null);
        await CrearArticuloAsync("B", categoria.Id, null, deBaja: true);

        (await _sut.ContarArticulosPorCategoriaAsync(categoria.Id)).Should().Be(2);
    }

    [Fact]
    public async Task ContarArticulosPorMarcaAsync_ConArticuloDeBaja_LoIncluye()
    {
        var marca = await CrearMarcaAsync("Bic");
        await CrearArticuloAsync("A", null, marca.Id);
        await CrearArticuloAsync("B", null, marca.Id, deBaja: true);

        (await _sut.ContarArticulosPorMarcaAsync(marca.Id)).Should().Be(2);
    }

    [Fact]
    public async Task ContarCategoriasActivasPorRubroAsync_ConCategoriaDeBaja_LaExcluye()
    {
        var rubro = await CrearRubroAsync("Librería");
        await CrearCategoriaAsync("Cuadernos", rubro.Id);
        await CrearCategoriaAsync("Obsoleta", rubro.Id, deBaja: true);

        (await _sut.ContarCategoriasActivasPorRubroAsync(rubro.Id)).Should().Be(1);
    }
}
