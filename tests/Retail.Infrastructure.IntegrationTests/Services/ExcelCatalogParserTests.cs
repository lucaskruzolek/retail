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
    private readonly string _tempCsvPath;

    public ExcelCatalogParserTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_catalog_{Guid.NewGuid():N}.xlsx");
        _tempCsvPath = Path.ChangeExtension(_tempFilePath, ".csv");
    }

    public void Dispose()
    {
        foreach (var ruta in new[] { _tempFilePath, _tempCsvPath })
        {
            if (File.Exists(ruta))
            {
                try
                {
                    File.Delete(ruta);
                }
                catch
                {
                    // Ignorar si el archivo está retenido temporalmente por el SO
                }
            }
        }

        GC.SuppressFinalize(this);
    }

    private static MapeoColumnasDto Mapeo(string extension = ".xlsx", int filaEncabezado = 1, string? columnaCodigoBarras = null)
    {
        return new MapeoColumnasDto
        {
            IdProveedor = 1,
            ColumnaCodigo = "CODIGO",
            ColumnaDescripcion = "DETALLE",
            ColumnaPrecioCosto = "PRECIO",
            ColumnaCodigoBarras = columnaCodigoBarras,
            FilaEncabezado = filaEncabezado,
            ExtensionArchivo = extension
        };
    }

    private static async Task<ResultadoParseoCatalogoDto> ParsearAsync(string ruta, MapeoColumnasDto mapeo)
    {
        var parser = new ExcelCatalogParser();
        await using var stream = File.OpenRead(ruta);
        return await parser.ParsearCatalogoAsync(stream, mapeo, null, CancellationToken.None);
    }

    private async Task<ResultadoParseoCatalogoDto> ParsearCsvAsync(string contenido, MapeoColumnasDto? mapeo = null)
    {
        await File.WriteAllTextAsync(_tempCsvPath, contenido);
        return await ParsearAsync(_tempCsvPath, mapeo ?? Mapeo(".csv"));
    }

    /// <summary>
    /// Escribe una hoja XLSX sin encabezado automático: cada arreglo es una fila, tal como se vería en Excel.
    /// Una fila con todos sus valores nulos representa una fila en blanco.
    /// </summary>
    private async Task EscribirXlsxAsync(params object?[][] filas)
    {
        var hoja = filas
            .Select(fila => fila
                .Select((valor, indice) => (Columna: ((char)('A' + indice)).ToString(), Valor: valor))
                .ToDictionary(celda => celda.Columna, celda => celda.Valor))
            .ToList();

        await MiniExcel.SaveAsAsync(_tempFilePath, hoja, printHeader: false);
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
        ResultadoParseoCatalogoDto resultado;
        await using (var stream = File.OpenRead(_tempFilePath))
        {
            resultado = await parser.ParsearCatalogoAsync(stream, mapeo, progress, CancellationToken.None);
        }

        stopwatch.Stop();
        var memoriaFinal = GC.GetTotalMemory(forceFullCollection: false);
        var deltaMemoriaMb = (memoriaFinal - memoriaInicial) / (1024.0 * 1024.0);

        // Assert (RNF-02: < 3s para 5,000 filas; RNF-03: consumo eficiente de RAM)
        var itemsLeidos = resultado.Items;
        itemsLeidos.Should().HaveCount(totalFilas);
        resultado.FilasDescartadas.Should().BeEmpty();
        itemsLeidos[0].NumeroFila.Should().Be(2, "la fila 1 es el encabezado");
        itemsLeidos[0].CodigoProveedor.Should().Be("ART-00001");
        itemsLeidos[0].Descripcion.Should().Be("Artículo de Prueba 1");
        itemsLeidos[0].PrecioCosto.Should().Be(151.75m);
        itemsLeidos[0].CodigoBarras.Should().Be("7790000000001");

        itemsLeidos[totalFilas - 1].CodigoProveedor.Should().Be($"ART-{totalFilas:D5}");
        itemsLeidos[totalFilas - 1].NumeroFila.Should().Be(totalFilas + 1);

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "el procesamiento streaming con MiniExcel debe ser sub-3s");
        deltaMemoriaMb.Should().BeLessThan(50, "el streaming diferido no debe acumular decenas de megabytes en memoria");
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConColumnaObligatoriaInexistente_LanzaInvalidDataExceptionConColumnasEncontradas()
    {
        // Arrange
        var filas = new[]
        {
            new { SKU = "A1", TITLE = "Producto 1", COST = 100m }
        };
        await MiniExcel.SaveAsAsync(_tempFilePath, filas);

        // Act
        var accion = () => ParsearAsync(_tempFilePath, Mapeo());

        // Assert
        await accion.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*'CODIGO'*SKU, TITLE, COST*");
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConCsvYPreciosEnTextoEsAr_LeeElArchivoYInterpretaLosImportes()
    {
        // Act
        var resultado = await ParsearCsvAsync(
            "CODIGO,DETALLE,PRECIO\n" +
            "A1,Libro uno,\"1.500\"\n" +
            "A2,Libro dos,\"1,50\"\n" +
            "A3,Libro tres,\"1.234,56\"\n");

        // Assert
        resultado.Items.Select(i => i.PrecioCosto).Should().Equal(1500m, 1.5m, 1234.56m);
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConFilaEncabezadoEnFila3_IgnoraLasFilasDeTituloYNumeraComoExcel()
    {
        // Arrange: título en la fila 1, fila 2 en blanco, encabezado en la fila 3
        await EscribirXlsxAsync(
            new object?[] { "DISTRIBUIDORA SUR - Lista octubre", null, null },
            new object?[] { null, null, null },
            new object?[] { "CODIGO", "DETALLE", "PRECIO" },
            new object?[] { "BIC-AZ", "Birome azul", 5000m },
            new object?[] { "SOB-100", "Sobre manila", 3000m });

        // Act
        var resultado = await ParsearAsync(_tempFilePath, Mapeo(filaEncabezado: 3));

        // Assert
        resultado.Items.Select(i => (i.NumeroFila, i.CodigoProveedor))
            .Should().Equal((4, "BIC-AZ"), (5, "SOB-100"));
        resultado.FilasDescartadas.Should().BeEmpty();
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConFilaEnBlancoIntermediaEnXlsx_ConservaElNumeroDeFilaReal()
    {
        // Arrange
        await EscribirXlsxAsync(
            new object?[] { "CODIGO", "DETALLE", "PRECIO" },
            new object?[] { "A1", "Libro uno", 100m },
            new object?[] { null, null, null },
            new object?[] { "A2", "Libro dos", 200m });

        // Act
        var resultado = await ParsearAsync(_tempFilePath, Mapeo());

        // Assert
        resultado.Items.Select(i => i.NumeroFila).Should().Equal(2, 4);
        resultado.FilasDescartadas.Should().BeEmpty("una fila completamente vacía no es un error");
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConFilaEnBlancoIntermediaEnCsv_ConservaElNumeroDeFilaReal()
    {
        // Act
        var resultado = await ParsearCsvAsync(
            "CODIGO,DETALLE,PRECIO\n" +
            "A1,Libro uno,100\n" +
            ",,\n" +
            "A2,Libro dos,200\n");

        // Assert
        resultado.Items.Select(i => i.NumeroFila).Should().Equal(2, 4);
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConDatosInvalidos_ReportaMotivoYNumeroDeFila()
    {
        // Act
        var resultado = await ParsearCsvAsync(
            "CODIGO,DETALLE,PRECIO\n" +
            ",Sin código,100\n" +
            "A2,,100\n" +
            "A3,Sin precio,\n" +
            "A4,Precio ilegible,abc\n" +
            "A5,Precio negativo,-5\n" +
            "A6,Válido,10\n");

        // Assert
        resultado.Items.Should().ContainSingle().Which.CodigoProveedor.Should().Be("A6");
        resultado.FilasDescartadas.Should().HaveCount(5);
        resultado.FilasDescartadas[0].Should().StartWith("Fila 2:").And.Contain("código");
        resultado.FilasDescartadas[1].Should().StartWith("Fila 3:").And.Contain("descripción");
        resultado.FilasDescartadas[2].Should().StartWith("Fila 4:").And.Contain("vacío");
        resultado.FilasDescartadas[3].Should().StartWith("Fila 5:").And.Contain("'abc'");
        resultado.FilasDescartadas[4].Should().StartWith("Fila 6:").And.Contain("negativo");
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConEncabezadosConAcentosMayusculasYEspacios_ResuelveLasColumnas()
    {
        // Arrange: la planilla usa "Código", "Detallé" y " precio " pero el mapeo dice CODIGO, DETALLE y PRECIO
        var mapeo = Mapeo(".csv");

        // Act
        var resultado = await ParsearCsvAsync(
            "Código,Detallé, precio \n" +
            "A1,Libro uno,100\n",
            mapeo);

        // Assert
        resultado.Items.Should().ContainSingle().Which.CodigoProveedor.Should().Be("A1");
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConColumnaCodigoBarrasInexistente_ImportaSinCodigoDeBarras()
    {
        // Act: el mapeo pide EAN, pero la planilla no tiene esa columna (es opcional)
        var resultado = await ParsearCsvAsync(
            "CODIGO,DETALLE,PRECIO\n" +
            "A1,Libro uno,100\n",
            Mapeo(".csv", columnaCodigoBarras: "EAN"));

        // Assert
        resultado.Items.Should().ContainSingle().Which.CodigoBarras.Should().BeNull();
    }

    [Fact]
    public async Task ParsearCatalogoAsync_ConFilaEncabezadoPosteriorAlFinDeLaPlanilla_LanzaInvalidDataException()
    {
        // Act
        var accion = () => ParsearCsvAsync(
            "CODIGO,DETALLE,PRECIO\n" +
            "A1,Libro uno,100\n",
            Mapeo(".csv", filaEncabezado: 10));

        // Assert
        await accion.Should().ThrowAsync<InvalidDataException>().WithMessage("*fila de encabezado 10*");
    }
}
