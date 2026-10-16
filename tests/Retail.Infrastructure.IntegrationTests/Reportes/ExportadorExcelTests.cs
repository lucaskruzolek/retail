using System.Globalization;
using FluentAssertions;
using MiniExcelLibs;
using Retail.Application.DTOs.Reportes.Exportacion;
using Retail.Infrastructure.Reportes;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Reportes;

public class ExportadorExcelTests
{
    private static MemoryStream Exportar()
    {
        var flujo = new MemoryStream();
        ExportadorExcel.Exportar(DocumentoReportePrueba.Completo(), flujo);
        flujo.Position = 0;
        return flujo;
    }

    private static List<IDictionary<string, object?>> Leer(MemoryStream flujo, string hoja)
    {
        flujo.Position = 0;
        return MiniExcel.Query(flujo, useHeaderRow: true, sheetName: hoja)
            .Cast<IDictionary<string, object?>>()
            .ToList();
    }

    [Fact]
    public void Exportar_DocumentoCompleto_HojaReporteTieneTituloUsuarioYFiltros()
    {
        using var flujo = Exportar();

        var filas = Leer(flujo, "Reporte").ToDictionary(f => (string)f["Campo"]!, f => f["Valor"]);

        filas["Reporte"].Should().Be("Reporte de ventas");
        filas["Generado por"].Should().Be("Gerente Pérez");
        filas["Generado el"].Should().Be("15/10/2026 14:30");
        filas["Desde"].Should().Be("01/10/2026");
        filas["Hasta"].Should().Be("15/10/2026");
    }

    [Fact]
    public void Exportar_TitulosLargosYRepetidos_HojasConNombresSaneadosYUnicos()
    {
        using var flujo = Exportar();

        flujo.Position = 0;
        var hojas = MiniExcel.GetSheetNames(flujo);

        hojas.Should().HaveCount(4);
        hojas.Should().OnlyHaveUniqueItems();
        hojas.Should().OnlyContain(h => h.Length <= 31);
        hojas.Should().Contain(["Reporte", "Resumen", "Resumen (2)"]);
        hojas.Should().Contain(DocumentoReportePrueba.TituloTablaLarga[..31].TrimEnd());
    }

    [Theory]
    [InlineData("A/B:C*D?E[F]G\\H", "ABCDEFGH")]
    [InlineData("///", "Tabla")]
    public void ObtenerNombreHojaUnico_CaracteresProhibidos_LosElimina(string titulo, string esperado)
    {
        ExportadorExcel.ObtenerNombreHojaUnico(titulo, new HashSet<string>()).Should().Be(esperado);
    }

    [Fact]
    public void Exportar_CeldasNumericas_SeLeenComoNumeroYNoComoTexto()
    {
        using var flujo = Exportar();

        var primera = Leer(flujo, "Resumen")[0];

        primera["Artículo"].Should().Be("Cuaderno año 2024 ñandú");
        primera["Unidades"].Should().NotBeOfType<string>();
        Convert.ToDecimal(primera["Unidades"], CultureInfo.InvariantCulture).Should().Be(12m);
        primera["Importe"].Should().NotBeOfType<string>();
        Convert.ToDecimal(primera["Importe"], CultureInfo.InvariantCulture).Should().Be(1234.5m);
        primera["Margen"].Should().NotBeOfType<string>();
        Convert.ToDecimal(primera["Margen"], CultureInfo.InvariantCulture).Should().Be(25.5m);
        primera["Última venta"].Should().BeOfType<DateTime>().Which.Should().Be(new DateTime(2026, 10, 1));
    }

    [Fact]
    public void Exportar_CeldasNull_QuedanVacias()
    {
        using var flujo = Exportar();

        var ultima = Leer(flujo, "Resumen")[2];

        ultima["Unidades"].Should().BeNull();
        ultima["Importe"].Should().BeNull();
        ultima["Margen"].Should().BeNull();
        ultima["Última venta"].Should().BeNull();
    }

    [Fact]
    public void Exportar_TablaSinFilas_NoFallaYConservaLaHoja()
    {
        var documento = DocumentoReportePrueba.Completo() with
        {
            Tablas = [new TablaReporte("Vacía", [new ColumnaReporte("Artículo", TipoColumnaEnum.Texto)], [])]
        };
        using var flujo = new MemoryStream();

        ExportadorExcel.Exportar(documento, flujo);

        flujo.Position = 0;
        MiniExcel.GetSheetNames(flujo).Should().Contain("Vacía");
        flujo.Position = 0;
        var encabezado = MiniExcel.Query(flujo, useHeaderRow: false, sheetName: "Vacía").Cast<IDictionary<string, object?>>().ToList();
        encabezado.Should().ContainSingle();
        encabezado[0]["A"].Should().Be("Artículo");
    }

    [Fact]
    public void Exportar_EncabezadosRepetidos_LlevanSufijo()
    {
        var documento = DocumentoReportePrueba.Completo() with
        {
            Tablas =
            [
                new TablaReporte(
                    "Dup",
                    [new ColumnaReporte("Total", TipoColumnaEnum.Moneda), new ColumnaReporte("Total", TipoColumnaEnum.Entero)],
                    [new object?[] { 1.5m, 2 }])
            ]
        };
        using var flujo = new MemoryStream();

        ExportadorExcel.Exportar(documento, flujo);

        var fila = Leer(flujo, "Dup")[0];
        fila.Keys.Should().Equal("Total", "Total (2)");
    }

    [Fact]
    public void Exportar_Hojas_NoDeclaranColumnasConIndiceCero()
    {
        using var flujo = Exportar();
        using var zip = new System.IO.Compression.ZipArchive(flujo);

        foreach (var entrada in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var lector = new StreamReader(entrada.Open());
            lector.ReadToEnd().Should().NotContain("<x:col min=\"0\"");
        }
    }
}
