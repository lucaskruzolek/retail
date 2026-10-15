using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
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
}
