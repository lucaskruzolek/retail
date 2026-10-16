using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using QuestPDF.Fluent;
using Retail.Application.DTOs.Reportes.Exportacion;
using Retail.Infrastructure.Reportes;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Reportes;

public class ExportadorPdfTests
{
    private static byte[] Generar(DocumentoReporte documento)
    {
        using var flujo = new MemoryStream();
        ExportadorPdf.Exportar(documento, flujo);
        return flujo.ToArray();
    }

    /// <summary>
    /// Cuenta los objetos de página del PDF buscando <c>/Type /Page</c> que no sea <c>/Pages</c> (el árbol de páginas).
    /// Si QuestPDF pasara a comprimir los diccionarios en flujos de objetos, el test de una sola página fallaría
    /// (contaría 0) en lugar de dar un falso positivo.
    /// </summary>
    private static int ContarPaginas(byte[] pdf)
    {
        var texto = Encoding.Latin1.GetString(pdf);
        return Regex.Count(texto, @"/Type\s*/Page(?!s)");
    }

    [Fact]
    public void Exportar_ConfiguracionDeFuentes_NoDependeDelArchivoDeLatoJuntoAlEjecutable()
    {
        // Arrange + Act: el constructor estático del exportador fija la configuración global de QuestPDF.
        Generar(DocumentoReportePrueba.SinGraficosNiFiltros());

        // Assert (R-12): Segoe UI del sistema y sin detener el PDF por la familia de respaldo (Lato) faltante. La prueba
        // de punta a punta, con el ejecutable único publicado y sin QuestPDF.Fonts.Lato.br, está en el plan (R-5, R-12).
        ExportadorPdf.FuenteDelDocumento.Should().Be("Segoe UI");
        QuestPDF.Settings.UseSystemFonts.Should().BeTrue();
        QuestPDF.Settings.ThrowOnMissingFontFamilies.Should().BeFalse();
        QuestPDF.Settings.ThrowOnMissingTextGlyphs.Should().BeTrue("un carácter que ninguna fuente puede dibujar debe seguir deteniendo el PDF");
    }

    [Fact]
    public void Exportar_DocumentoCompleto_GeneraPdfValido()
    {
        var pdf = Generar(DocumentoReportePrueba.Completo());

        pdf.Should().NotBeEmpty();
        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        ContarPaginas(pdf).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Exportar_TablaDe200Filas_OcupaMasDeUnaPagina()
    {
        var pdf = Generar(DocumentoReportePrueba.ConFilas(200));

        ContarPaginas(pdf).Should().BeGreaterThan(1);
    }

    [Fact]
    public void Exportar_TablaChica_OcupaUnaSolaPagina()
    {
        ContarPaginas(Generar(DocumentoReportePrueba.SinGraficosNiFiltros())).Should().Be(1);
    }

    [Fact]
    public void Exportar_SinGraficosNiFiltros_GeneraPdf()
    {
        var pdf = Generar(DocumentoReportePrueba.SinGraficosNiFiltros());

        Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void Formatear_MonedaEnteroYPorcentaje_UsaSeparadoresArgentinos()
    {
        ExportadorPdf.Formatear(TipoColumnaEnum.Moneda, 1234.5m).Should().Be("$ 1.234,50");
        ExportadorPdf.Formatear(TipoColumnaEnum.Entero, 1234567).Should().Be("1.234.567");
        ExportadorPdf.Formatear(TipoColumnaEnum.Porcentaje, 25.5m).Should().Be("25,50 %");
    }

    [Fact]
    public void Formatear_NullYFecha_UsaGuionYFormatoDiaMes()
    {
        ExportadorPdf.Formatear(TipoColumnaEnum.Moneda, null).Should().Be("—");
        ExportadorPdf.Formatear(TipoColumnaEnum.Fecha, new DateOnly(2026, 10, 1)).Should().Be("01/10/2026");
    }

    [Fact]
    public void CrearDocumento_ConUnaTablaDeNueveColumnas_SaleApaisado()
    {
        // Arrange: el desglose por medio de pago de Ventas por período (D-34) no entra en A4 vertical con importes de 9 cifras
        var columnas = Enumerable.Range(1, 9).Select(i => new ColumnaReporte($"Col {i}", TipoColumnaEnum.Moneda)).ToArray();
        var fila = Enumerable.Repeat((object?)987_654_321.55m, 9).ToArray();
        var documento = new DocumentoReporte(
            "Ventas por período", [], new DateTime(2026, 10, 15), "Gerente", [new TablaReporte("Ventas", columnas, [fila])], []);

        // Act
        var imagenes = ExportadorPdf.CrearDocumento(documento).GenerateImages().ToList();

        // Assert: la primera página es más ancha que alta
        using var bitmap = SkiaSharp.SKBitmap.Decode(imagenes[0]);
        bitmap.Width.Should().BeGreaterThan(bitmap.Height);
    }

    [Fact]
    public void CrearDocumento_ConTablasDeCincoColumnas_SigueVertical()
    {
        // Act
        var imagenes = ExportadorPdf.CrearDocumento(DocumentoReportePrueba.Completo()).GenerateImages().ToList();

        // Assert
        using var bitmap = SkiaSharp.SKBitmap.Decode(imagenes[0]);
        bitmap.Height.Should().BeGreaterThan(bitmap.Width);
    }
}
