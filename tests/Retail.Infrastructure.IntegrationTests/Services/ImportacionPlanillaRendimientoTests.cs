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
/// Medición del importador con una planilla de 30.000 filas, el tamaño del catálogo de un distribuidor grande (análisis
/// de rendimiento de la clasificación, paso C0). Recorre tres escenarios en orden: el alta inicial, una reimportación
/// idéntica y la actualización semanal típica (cambios al azar en precios y descripciones, ítems nuevos y 3.000
/// artículos vinculados). Informa el
/// tiempo de cada uno, los lotes enviados y las compilaciones de SQL; como en las otras mediciones, el tiempo no se
/// afirma (depende de SQL Server Express y del disco) y sí los conteos del resultado.
/// </summary>
public class ImportacionPlanillaRendimientoTests : IAsyncLifetime, IDisposable
{
    private const int CantidadDeFilas = 30_000;
    private const int ArticulosVinculados = 3_000;

    private readonly string _dbName = $"RetailDb_Test_ImportRend_{Guid.NewGuid():N}";
    private readonly string _rutaPlanilla = Path.Combine(Path.GetTempPath(), $"planilla_rend_{Guid.NewGuid():N}.xlsx");
    private readonly ITestOutputHelper _salida;
    private readonly MedidorDeComandosSql _comandos = new();
    private readonly MedidorDeGuardado _guardado = new();
    private DbContextOptions<RetailDbContext> _options = null!;
    private RetailDbContext _context = null!;

    public ImportacionPlanillaRendimientoTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(_options);
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
        if (File.Exists(_rutaPlanilla))
        {
            File.Delete(_rutaPlanilla);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    [Trait("Category", "Rendimiento")]
    public async Task ImportarPlanillaProveedorAsync_30000Filas_InformaElTiempoDeCadaEscenario()
    {
        // Arrange
        var proveedor = Proveedor.Crear("Distribuidora Grande", "30-11111111-8");
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await EscribirPlanillaAsync(PlanillaBase());
        var cronometro = Stopwatch.StartNew();
        await using (var stream = File.OpenRead(_rutaPlanilla))
        {
            var parseo = await new ExcelCatalogParser().ParsearCatalogoAsync(stream, Mapeo(proveedor.Id));
            _salida.WriteLine($"0. Parseo aislado de la planilla: {cronometro.ElapsedMilliseconds:N0} ms ({parseo.Items.Count} filas)");
        }

        // Act y Assert, escenario por escenario
        var alta = await ImportarYMedirAsync("1. Alta inicial", proveedor.Id);
        alta.NuevosRegistros.Should().Be(CantidadDeFilas);

        await VincularArticulosAsync(proveedor.Id);
        var identica = await ImportarYMedirAsync("2. Reimportación idéntica", proveedor.Id);
        identica.NuevosRegistros.Should().Be(0);
        identica.PreciosActualizados.Should().Be(0, "ningún costo cambió");

        var (semanal, preciosDeVinculados) = PlanillaSemanal();
        await EscribirPlanillaAsync(semanal);
        var resultado = await ImportarYMedirAsync("3. Reimportación semanal (10 % precios, 2 % descripciones, 1 % nuevos)", proveedor.Id);
        resultado.NuevosRegistros.Should().Be(CantidadDeFilas / 100);
        resultado.PreciosActualizados.Should().Be(preciosDeVinculados);
        resultado.FilasConError.Should().Be(0);
    }

    private sealed record FilaPlanilla(string Codigo, string Descripcion, decimal Precio, int Numero);

    private static List<FilaPlanilla> PlanillaBase()
    {
        return Enumerable.Range(1, CantidadDeFilas)
            .Select(i => new FilaPlanilla($"G-{i:D6}", $"PRODUCTO {i} MODELO {i % 97}", 100m, i))
            .ToList();
    }

    /// <summary>
    /// La actualización semanal típica, con los cambios repartidos al azar (semilla fija, reproducible) y no con un
    /// patrón regular: un patrón periódico deja pocas combinaciones de filas por lote y escondería la variedad de formas
    /// de los UPDATE que se quiere medir. 10 % de los precios sube, 2 % de las descripciones cambia y se agrega 1 % de
    /// ítems nuevos. Devuelve también cuántos artículos vinculados (los de las primeras filas) cambian de costo.
    /// </summary>
    private static (List<FilaPlanilla> Filas, int PreciosDeVinculados) PlanillaSemanal()
    {
        var aleatorio = new Random(42);
        var filas = new List<FilaPlanilla>(CantidadDeFilas + (CantidadDeFilas / 100));
        int preciosDeVinculados = 0;
        foreach (var fila in PlanillaBase())
        {
            bool cambiaPrecio = aleatorio.NextDouble() < 0.10;
            bool cambiaDescripcion = aleatorio.NextDouble() < 0.02;
            if (cambiaPrecio && fila.Numero <= ArticulosVinculados)
            {
                preciosDeVinculados++;
            }

            filas.Add(fila with
            {
                Precio = cambiaPrecio ? 110m : fila.Precio,
                Descripcion = cambiaDescripcion ? $"{fila.Descripcion} NUEVO" : fila.Descripcion
            });
        }

        // Los nuevos se intercalan al azar entre las filas existentes, como en una lista real ordenada por rubro.
        for (var i = 1; i <= CantidadDeFilas / 100; i++)
        {
            int numero = CantidadDeFilas + i;
            filas.Insert(aleatorio.Next(filas.Count), new FilaPlanilla($"G-{numero:D6}", $"PRODUCTO {numero} MODELO {numero % 97}", 100m, numero));
        }

        return (filas, preciosDeVinculados);
    }

    /// <summary>
    /// Importa la planilla con un contexto nuevo, como una pantalla recién abierta, con los medidores enganchados, e
    /// informa el tiempo total, el del guardado, los lotes y las compilaciones.
    /// </summary>
    private async Task<ResultadoImportacionDto> ImportarYMedirAsync(string escenario, int idProveedor)
    {
        await using var medido = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>(_options)
            .AddInterceptors(_comandos, _guardado)
            .Options);
        var servicio = new ProveedorService(
            new Repository<Proveedor>(medido),
            new CatalogoProveedorQueryService(medido),
            new Repository<Articulo>(medido),
            new UnitOfWork(medido),
            new ExcelCatalogParser(),
            new CrearProveedorValidator(),
            new ActualizarProveedorValidator(),
            new MapeoColumnasValidator(),
            new IncorporarCatalogoArticulosValidator(),
            new ConsultaCatalogoProveedorValidator());

        _comandos.Reiniciar();
        _guardado.Reiniciar();
        long compilacionesAntes = await EstadisticasSqlServer.CompilacionesAsync(_context);

        var cronometro = Stopwatch.StartNew();
        ResultadoImportacionDto resultado;
        await using (var stream = File.OpenRead(_rutaPlanilla))
        {
            resultado = await servicio.ImportarPlanillaProveedorAsync(stream, Mapeo(idProveedor));
        }

        cronometro.Stop();

        long compilaciones = await EstadisticasSqlServer.CompilacionesAsync(_context) - compilacionesAntes;
        var primerUpdate = _comandos.TextosDeEscritura.FirstOrDefault(t => t.Contains("UPDATE", StringComparison.Ordinal));
        _salida.WriteLine($"{escenario}: {cronometro.ElapsedMilliseconds:N0} ms " +
            $"({resultado.NuevosRegistros} nuevos, {resultado.PreciosActualizados} precios de artículos actualizados)");
        _salida.WriteLine($"   SaveChanges: {_guardado.Duracion.TotalMilliseconds:N0} ms; comandos de escritura: " +
            $"{_comandos.DuracionEscrituras.TotalMilliseconds:N0} ms en {_comandos.Escrituras} lotes " +
            $"({_comandos.TextosDeEscritura.Distinct().Count()} textos distintos); compilaciones: {compilaciones}");
        _salida.WriteLine($"   Lecturas: {_comandos.Lecturas} consultas, {_comandos.DuracionLecturas.TotalMilliseconds:N0} ms de ejecución; " +
            $"entidades en el Change Tracker: {medido.ChangeTracker.Entries().Count()}");
        if (primerUpdate is not null)
        {
            _salida.WriteLine($"   Primer UPDATE: {PrimeraSentencia(primerUpdate)}");
        }

        return resultado;
    }

    private static string PrimeraSentencia(string lote)
    {
        int inicio = lote.IndexOf("UPDATE", StringComparison.Ordinal);
        int fin = lote.IndexOf("OUTPUT", inicio, StringComparison.Ordinal);
        return lote[inicio..(fin < 0 ? lote.Length : fin)].ReplaceLineEndings(" ").Trim();
    }

    private static MapeoColumnasDto Mapeo(int idProveedor)
    {
        return new MapeoColumnasDto
        {
            IdProveedor = idProveedor,
            ColumnaCodigo = "CODIGO",
            ColumnaDescripcion = "DESCRIPCION",
            ColumnaPrecioCosto = "PRECIO",
            ColumnaRubro = "RUBRO",
            ColumnaCategoria = "CATEGORIA",
            ColumnaMarca = "MARCA"
        };
    }

    /// <summary>
    /// Escribe la planilla con los textos de clasificación del proveedor repartidos entre pocos valores, como en una lista
    /// real (5 rubros, 20 categorías, 50 marcas), derivados del número de fila.
    /// </summary>
    private async Task EscribirPlanillaAsync(IEnumerable<FilaPlanilla> filas)
    {
        var hoja = filas.Select(f => new Dictionary<string, object?>
        {
            ["CODIGO"] = f.Codigo,
            ["DESCRIPCION"] = f.Descripcion,
            ["PRECIO"] = f.Precio,
            ["RUBRO"] = $"RUBRO {f.Numero % 5}",
            ["CATEGORIA"] = $"CATEGORIA {f.Numero % 20}",
            ["MARCA"] = $"MARCA {f.Numero % 50}"
        });

        if (File.Exists(_rutaPlanilla))
        {
            File.Delete(_rutaPlanilla);
        }

        await MiniExcel.SaveAsAsync(_rutaPlanilla, hoja);
    }

    /// <summary>Vincula un artículo de la tienda (costo 100) a cada uno de los primeros ítems del catálogo.</summary>
    private async Task VincularArticulosAsync(int idProveedor)
    {
        var catalogos = await _context.CatalogosProveedores
            .Where(c => c.IdProveedor == idProveedor)
            // Por la clave primaria, sin Sort: los códigos se insertaron en orden. Ordenar por código pedía un memory
            // grant de 30 MB que SQL Server Express no otorga cuando el equipo tiene poca memoria libre.
            .OrderBy(c => c.Id)
            .Take(ArticulosVinculados)
            .ToListAsync();

        foreach (var catalogo in catalogos)
        {
            var articulo = new Articulo
            {
                Descripcion = catalogo.DescripcionProveedor,
                PorcentajeGanancia = 50m,
                StockActual = 10,
                StockMinimo = 2
            };
            catalogo.AsignarArticulo(articulo);
            articulo.MarcarFuentePrincipal(catalogo);
            articulo.ActualizarCostoYRecalcularPrecio(100m);
            _context.Articulos.Add(articulo);
        }

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }
}
