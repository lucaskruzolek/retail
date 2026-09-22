using System.Diagnostics;
using FluentAssertions;
using MiniExcelLibs;
using Retail.Application.DTOs.Proveedores;
using Retail.Infrastructure.ExternalServices.Excel;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

public class ExcelCatalogParserTests : IDisposable
{
    private readonly string _tempFilePath;

    public ExcelCatalogParserTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_catalog_{Guid.NewGuid():N}.xlsx");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            try
            {
                File.Delete(_tempFilePath);
            }
            catch
            {
                // Ignorar si el archivo está retenido temporalmente por el SO
            }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ParsearCatalogoAsync_Con5000Filas_ProcesaEnMenosDe3SegundosYConConsumoBajoDeMemoria()
    {
        // Arrange: Generar archivo Excel sintético con 5,000 registros
        const int totalFilas = 5000;
        var filas = new List<Dictionary<string, object?>>(totalFilas);

        for (var i = 1; i <= totalFilas; i++)
        {
            filas.Add(new Dictionary<string, object?>
            {
                { "CODIGO", $"ART-{i:D5}" },
                { "DETALLE", $"Artículo de Prueba {i}" },
                { "PRECIO_COSTO", 150.75m + i },
                { "EAN13", $"779{i:D10}" }
            });
        }

        await MiniExcel.SaveAsAsync(_tempFilePath, filas);

        var parser = new ExcelCatalogParser();
        var mapeo = new MapeoColumnasDto
        {
            IdProveedor = 1,
            ColumnaCodigo = "CODIGO",
            ColumnaDescripcion = "DETALLE",
            ColumnaPrecioCosto = "PRECIO_COSTO",
            ColumnaCodigoBarras = "EAN13"
        };

        var reportesProgreso = 0;
        var progress = new Progress<int>(_ => reportesProgreso++);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memoriaInicial = GC.GetTotalMemory(forceFullCollection: true);

        var stopwatch = Stopwatch.StartNew();

        // Act: Streaming con consumo diferido
        IReadOnlyList<ItemCatalogoImportadoDto> itemsLeidos;
        await using (var stream = File.OpenRead(_tempFilePath))
        {
            itemsLeidos = await parser.ParsearCatalogoAsync(stream, mapeo, progress, CancellationToken.None);
        }

        stopwatch.Stop();
        var memoriaFinal = GC.GetTotalMemory(forceFullCollection: false);
        var deltaMemoriaMb = (memoriaFinal - memoriaInicial) / (1024.0 * 1024.0);

        // Assert (RNF-02: < 3s para 5,000 filas; RNF-03: consumo eficiente de RAM)
        itemsLeidos.Should().HaveCount(totalFilas);
        itemsLeidos[0].CodigoProveedor.Should().Be("ART-00001");
        itemsLeidos[0].Descripcion.Should().Be("Artículo de Prueba 1");
        itemsLeidos[0].PrecioCosto.Should().Be(151.75m);
        itemsLeidos[0].CodigoBarras.Should().Be("7790000000001");

        itemsLeidos[totalFilas - 1].CodigoProveedor.Should().Be($"ART-{totalFilas:D5}");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "el procesamiento streaming con MiniExcel debe ser sub-3s");
        deltaMemoriaMb.Should().BeLessThan(50, "el streaming diferido no debe acumular decenas de megabytes en memoria");
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConColumnasInexistentes_RetornaVacioOSaltaFilasSinErroresFatales()
    {
        // Arrange
        var filas = new[]
        {
            new { SKU = "A1", TITLE = "Producto 1", COST = 100m }
        };
        await MiniExcel.SaveAsAsync(_tempFilePath, filas);

        var parser = new ExcelCatalogParser();
        var mapeo = new MapeoColumnasDto
        {
            IdProveedor = 1,
            ColumnaCodigo = "CODIGO_INEXISTENTE",
            ColumnaDescripcion = "DESCRIPCION_INEXISTENTE",
            ColumnaPrecioCosto = "PRECIO_INEXISTENTE"
        };

        IReadOnlyList<ItemCatalogoImportadoDto> items;

        // Act
        await using (var stream = File.OpenRead(_tempFilePath))
        {
            items = await parser.ParsearCatalogoAsync(stream, mapeo, null, CancellationToken.None);
        }

        // Assert
        items.Should().BeEmpty();
    }
}
