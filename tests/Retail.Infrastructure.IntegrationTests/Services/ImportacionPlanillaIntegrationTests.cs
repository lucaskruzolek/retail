using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Services;
using Retail.Application.Validators.Proveedores;
using Retail.Domain.Entities;
using Retail.Infrastructure.ExternalServices.Excel;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;
using Xunit.Abstractions;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Pruebas de punta a punta del importador: ProveedorService armado con las piezas reales (repositorios,
/// Unit of Work, query service y parser) contra LocalDB. Cubren H-14 y la medición de H-15 del handoff.
/// </summary>
public class ImportacionPlanillaIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Import_{Guid.NewGuid():N}";
    private readonly string _rutaPlanilla = Path.Combine(Path.GetTempPath(), $"planilla_{Guid.NewGuid():N}.xlsx");
    private readonly ITestOutputHelper _salida;
    private RetailDbContext _context = null!;
    private ProveedorService _sut = null!;

    public ImportacionPlanillaIntegrationTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        _sut = new ProveedorService(
            new Repository<Proveedor>(_context),
            new CatalogoProveedorQueryService(_context),
            new Repository<Articulo>(_context),
            new UnitOfWork(_context),
            new ExcelCatalogParser(),
            new CrearProveedorValidator(),
            new ActualizarProveedorValidator(),
            new MapeoColumnasValidator(),
            new IncorporarCatalogoArticulosValidator());
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        if (File.Exists(_rutaPlanilla))
        {
            File.Delete(_rutaPlanilla);
        }

        GC.SuppressFinalize(this);
    }

    private async Task<Proveedor> CrearProveedorAsync(string razonSocial, string cuit)
    {
        var proveedor = new Proveedor { RazonSocial = razonSocial, Cuit = cuit };
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();
        return proveedor;
    }

    /// <summary>
    /// Crea <paramref name="cantidad"/> renglones de catálogo para el proveedor, cada uno con un artículo
    /// de la tienda vinculado (costo 100, ganancia 50 %, precio 150).
    /// </summary>
    private async Task CrearCatalogoConArticulosAsync(Proveedor proveedor, string prefijo, int cantidad)
    {
        var catalogos = Enumerable.Range(1, cantidad).Select(i => new CatalogoProveedor
        {
            IdProveedor = proveedor.Id,
            CodigoProveedor = $"{prefijo}-{i:D5}",
            DescripcionProveedor = $"Producto {prefijo} {i}",
            CostoReposicion = 100m
        }).ToList();
        _context.CatalogosProveedores.AddRange(catalogos);
        await _context.SaveChangesAsync();

        _context.Articulos.AddRange(catalogos.Select(c => new Articulo
        {
            Descripcion = c.DescripcionProveedor,
            IdCatalogoProveedor = c.Id,
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m,
            StockActual = 10,
            StockMinimo = 2
        }));
        await _context.SaveChangesAsync();
    }

    private async Task EscribirPlanillaAsync(IEnumerable<(string Codigo, decimal Precio)> filas)
    {
        var hoja = filas.Select(f => new Dictionary<string, object?>
        {
            ["CODIGO"] = f.Codigo,
            ["DESCRIPCION"] = $"Descripción {f.Codigo}",
            ["PRECIO"] = f.Precio
        });
        await MiniExcel.SaveAsAsync(_rutaPlanilla, hoja);
    }

    private async Task<ResultadoImportacionDto> ImportarAsync(int idProveedor)
    {
        var mapeo = new MapeoColumnasDto
        {
            IdProveedor = idProveedor,
            ColumnaCodigo = "CODIGO",
            ColumnaDescripcion = "DESCRIPCION",
            ColumnaPrecioCosto = "PRECIO"
        };

        await using var stream = File.OpenRead(_rutaPlanilla);
        return await _sut.ImportarPlanillaProveedorAsync(stream, mapeo);
    }

    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConArticulosDeOtroProveedor_SoloCargaLosDelProveedorImportado()
    {
        // Arrange: los dos proveedores tienen un renglón "X-00001" con su artículo vinculado
        var proveedorA = await CrearProveedorAsync("Distribuidora Sur", "30-11111111-1");
        var proveedorB = await CrearProveedorAsync("Papelera Norte", "30-22222222-2");
        await CrearCatalogoConArticulosAsync(proveedorA, "X", 1);
        await CrearCatalogoConArticulosAsync(proveedorB, "X", 1);
        _context.ChangeTracker.Clear();

        await EscribirPlanillaAsync(new[] { ("X-00001", 200m) });

        // Act
        var resultado = await ImportarAsync(proveedorA.Id);

        // Assert: el ChangeTracker muestra qué artículos trajo la consulta Q2 (H-14)
        var articulosCargados = _context.ChangeTracker.Entries<Articulo>().Select(e => e.Entity).ToList();
        int idCatalogoA = await _context.CatalogosProveedores.AsNoTracking()
            .Where(c => c.IdProveedor == proveedorA.Id)
            .Select(c => c.Id)
            .SingleAsync();
        articulosCargados.Should().ContainSingle()
            .Which.IdCatalogoProveedor.Should().Be(idCatalogoA);

        resultado.PreciosActualizados.Should().Be(1);
        var precios = await _context.Articulos.AsNoTracking()
            .OrderBy(a => a.Id)
            .Select(a => a.PrecioVenta)
            .ToListAsync();
        precios.Should().Equal(300m, 150m);
    }

    /// <summary>
    /// RF-21: la consulta de la regla 9 (JOIN por ArticuloOrigen.CatalogoProveedor) se traduce a SQL y trae solo
    /// las presentaciones del proveedor importado; el costo nuevo se propaga a ellas y cuenta como precio actualizado.
    /// </summary>
    [Fact]
    public async Task ImportarPlanillaProveedorAsync_ConPresentacionDerivada_PropagaCostoSoloALasDelProveedor()
    {
        // Arrange: cada proveedor tiene un renglón "P-00001" con su pack vinculado y una presentación x10
        var proveedorA = await CrearProveedorAsync("Distribuidora Sur", "30-11111111-1");
        var proveedorB = await CrearProveedorAsync("Papelera Norte", "30-22222222-2");
        await CrearCatalogoConArticulosAsync(proveedorA, "P", 1);
        await CrearCatalogoConArticulosAsync(proveedorB, "P", 1);
        var derivadoA = await CrearPresentacionAsync(proveedorA);
        var derivadoB = await CrearPresentacionAsync(proveedorB);
        _context.ChangeTracker.Clear();

        await EscribirPlanillaAsync(new[] { ("P-00001", 200m) });

        // Act
        var resultado = await ImportarAsync(proveedorA.Id);
        _context.ChangeTracker.Clear();

        // Assert
        resultado.PreciosActualizados.Should().Be(2, "cambian el pack y su presentación");
        var leidoA = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == derivadoA.Id);
        var leidoB = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == derivadoB.Id);
        leidoA.CostoReposicion.Should().Be(20m);
        leidoA.PrecioVenta.Should().Be(40m);
        leidoB.CostoReposicion.Should().Be(10m, "la presentación de otro proveedor no se toca");
    }

    /// <summary>
    /// Crea una presentación x10 (ganancia 100 %) del artículo vinculado al catálogo del proveedor.
    /// </summary>
    private async Task<Articulo> CrearPresentacionAsync(Proveedor proveedor)
    {
        var origen = await _context.Articulos
            .SingleAsync(a => a.CatalogoProveedor != null && a.CatalogoProveedor.IdProveedor == proveedor.Id);
        var presentacion = new Articulo { Descripcion = $"{origen.Descripcion} (unidad)", PorcentajeGanancia = 100m };
        presentacion.DefinirComoPresentacionDe(origen, 10);
        _context.Articulos.Add(presentacion);
        await _context.SaveChangesAsync();
        return presentacion;
    }

    /// <summary>
    /// Medición de H-15: importa 5.000 filas (2.500 existentes con artículo vinculado y 2.500 nuevas) con
    /// 2.000 artículos de otro proveedor como ruido. Se afirma la memoria retenida (estable, ≈ 13 MB medidos)
    /// contra RNF-03. El tiempo solo se informa en la salida: depende de LocalDB y del disco (se midieron
    /// entre 3,9 s y 28,5 s sin cambios de código), y RNF-02 exige no bloquear la UI, no un tiempo fijo.
    /// El criterio de &lt; 3 s del roadmap aplica al parseo y lo cubre ExcelCatalogParserTests.
    /// </summary>
    [Fact]
    public async Task ImportarPlanillaProveedorAsync_Con5000Filas_RetieneMemoriaDentroDeRnf03()
    {
        // Arrange
        const int existentes = 2500;
        const int nuevas = 2500;
        var proveedor = await CrearProveedorAsync("Distribuidora Sur", "30-11111111-1");
        var otroProveedor = await CrearProveedorAsync("Papelera Norte", "30-22222222-2");
        await CrearCatalogoConArticulosAsync(proveedor, "A", existentes);
        await CrearCatalogoConArticulosAsync(otroProveedor, "B", 2000);
        _context.ChangeTracker.Clear();

        await EscribirPlanillaAsync(Enumerable.Range(1, existentes + nuevas)
            .Select(i => ($"A-{i:D5}", 120m)));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long memoriaInicial = GC.GetTotalMemory(forceFullCollection: true);
        var cronometro = Stopwatch.StartNew();

        // Act
        var resultado = await ImportarAsync(proveedor.Id);

        // Assert
        cronometro.Stop();
        // Memoria retenida al terminar: el ChangeTracker sigue vivo con todas las entidades de la importación
        long memoriaFinal = GC.GetTotalMemory(forceFullCollection: true);
        double incrementoMb = (memoriaFinal - memoriaInicial) / (1024.0 * 1024.0);
        int entidadesSeguidas = _context.ChangeTracker.Entries().Count();
        int articulosSeguidos = _context.ChangeTracker.Entries<Articulo>().Count();

        _salida.WriteLine($"Tiempo total (parseo + consultas + recorrido + SaveChanges): {cronometro.ElapsedMilliseconds} ms");
        _salida.WriteLine($"Incremento de memoria administrada retenida: {incrementoMb:N1} MB");
        _salida.WriteLine($"Entidades en el ChangeTracker al finalizar: {entidadesSeguidas} (artículos: {articulosSeguidos})");

        resultado.NuevosRegistros.Should().Be(nuevas);
        resultado.PreciosActualizados.Should().Be(existentes);
        resultado.FilasConError.Should().Be(0);
        articulosSeguidos.Should().Be(existentes, "solo se cargan los artículos del proveedor importado, no los 2.000 del otro (H-14)");
        incrementoMb.Should().BeLessThan(100, "la importación debe dejar margen holgado dentro de los 300 MB de RNF-03");
    }
}
