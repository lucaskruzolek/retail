using Retail.Application.DTOs.Reportes.Exportacion;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Reportes;

/// <summary>Documentos de ejemplo para probar los exportadores sin base de datos.</summary>
internal static class DocumentoReportePrueba
{
    public const string TituloTablaLarga = "Ventas por categoría, marca y medio de pago del período";
    public const string TituloTablaRepetida = "Resumen";

    public static DocumentoReporte Completo()
    {
        var columnas = new[]
        {
            new ColumnaReporte("Artículo", TipoColumnaEnum.Texto),
            new ColumnaReporte("Unidades", TipoColumnaEnum.Entero),
            new ColumnaReporte("Importe", TipoColumnaEnum.Moneda),
            new ColumnaReporte("Margen", TipoColumnaEnum.Porcentaje),
            new ColumnaReporte("Última venta", TipoColumnaEnum.Fecha)
        };

        var filas = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Cuaderno año 2024 ñandú", 12, 1234.5m, 25.5m, new DateOnly(2026, 10, 1) },
            new object?[] { "Lápiz acuarelable", 3, 99.99m, 8m, null },
            new object?[] { "Sin datos", null, null, null, null }
        };

        var tablaSimple = new TablaReporte(TituloTablaRepetida, columnas, filas);
        var otraColumna = new[] { new ColumnaReporte("Clave", TipoColumnaEnum.Texto) };

        return new DocumentoReporte(
            "Reporte de ventas",
            [new FiltroAplicado("Desde", "01/10/2026"), new FiltroAplicado("Hasta", "15/10/2026")],
            new DateTime(2026, 10, 15, 14, 30, 0, DateTimeKind.Unspecified),
            "Gerente Pérez",
            [
                tablaSimple,
                new TablaReporte(TituloTablaLarga, otraColumna, [new object?[] { "a" }]),
                new TablaReporte(TituloTablaRepetida, otraColumna, [new object?[] { "b" }])
            ],
            [
                new GraficoReporte("Ventas por día", TipoGraficoEnum.Barras, ["Lun", "Mar", "Miércoles"], [new SerieGrafico("Importe", [100m, 250.5m, 80m])]),
                new GraficoReporte("Tendencia", TipoGraficoEnum.Lineas, ["Ene", "Feb", "Mar"], [new SerieGrafico("Año", [1m, 3m, 2m]), new SerieGrafico("Anterior", [2m, 2m, 1m])])
            ]);
    }

    public static DocumentoReporte SinGraficosNiFiltros()
    {
        return new DocumentoReporte(
            "Mínimo",
            [],
            new DateTime(2026, 10, 15, 9, 0, 0),
            "Gerente",
            [new TablaReporte("Datos", [new ColumnaReporte("Texto", TipoColumnaEnum.Texto)], [new object?[] { "x" }])],
            []);
    }

    public static DocumentoReporte ConFilas(int cantidad)
    {
        var filas = Enumerable.Range(1, cantidad)
            .Select(i => (IReadOnlyList<object?>)new object?[] { $"Artículo {i}", i, i * 10.25m })
            .ToList();

        return new DocumentoReporte(
            "Reporte largo",
            [new FiltroAplicado("Período", "Octubre")],
            new DateTime(2026, 10, 15, 9, 0, 0),
            "Gerente",
            [
                new TablaReporte(
                    "Detalle",
                    [new ColumnaReporte("Artículo", TipoColumnaEnum.Texto), new ColumnaReporte("Cantidad", TipoColumnaEnum.Entero), new ColumnaReporte("Importe", TipoColumnaEnum.Moneda)],
                    filas)
            ],
            []);
    }
}
