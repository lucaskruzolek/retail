using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Retail.Application.DTOs.Reportes.Exportacion;

namespace Retail.Infrastructure.Reportes;

/// <summary>
/// Genera el PDF A4 de un <see cref="DocumentoReporte"/> con QuestPDF (RF-26): encabezado con título, usuario, fecha
/// y filtros; luego los gráficos y las tablas; y el pie "Página X de Y".
/// </summary>
internal static class ExportadorPdf
{
    private static readonly CultureInfo Cultura = new("es-AR");
    private const string SinDato = "—";
    private const float MinimoParaIniciarTabla = 100;

    // Desde esta cantidad de columnas el documento sale apaisado y la tabla usa letra chica (D-34).
    private const int ColumnasParaApaisar = 8;

    static ExportadorPdf()
    {
        // Licencia Community de QuestPDF (MIT): gratuita para uso educativo y para empresas con facturación anual
        // bruta menor a USD 1 M. Sin esta línea, QuestPDF lanza una excepción al generar el primer documento.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void Exportar(DocumentoReporte documento, Stream destino)
    {
        CrearDocumento(documento).GeneratePdf(destino);
    }

    /// <summary>Arma el documento QuestPDF sin escribirlo; los tests lo usan para inspeccionar las páginas como imágenes.</summary>
    internal static Document CrearDocumento(DocumentoReporte documento)
    {
        // Los gráficos se renderizan antes de armar el documento: QuestPDF puede componer el contenido más de una vez.
        var imagenes = documento.Graficos.Select(g => (Grafico: g, Png: RenderizadorGraficos.RenderizarPng(g))).ToList();

        // Una tabla de 8 o más columnas (el desglose por medio de pago de Ventas por período, D-34) no entra en A4 vertical
        // con importes de 9 cifras: todo el documento sale en A4 horizontal.
        var apaisado = documento.Tablas.Any(t => t.Columnas.Count >= ColumnasParaApaisar);

        return Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(apaisado ? PageSizes.A4.Landscape() : PageSizes.A4);
                pagina.Margin(1.5f, Unit.Centimetre);
                pagina.DefaultTextStyle(t => t.FontSize(9));

                pagina.Header().Element(c => ComponerEncabezado(c, documento));
                pagina.Content().PaddingVertical(8).Column(columna =>
                {
                    columna.Spacing(14);

                    foreach (var (grafico, png) in imagenes)
                    {
                        columna.Item().Element(c => ComponerGrafico(c, grafico, png));
                    }

                    foreach (var tabla in documento.Tablas)
                    {
                        columna.Item().Element(c => ComponerTabla(c, tabla));
                    }
                });
                pagina.Footer().AlignCenter().Text(texto =>
                {
                    texto.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Grey.Darken2));
                    texto.Span("Página ");
                    texto.CurrentPageNumber();
                    texto.Span(" de ");
                    texto.TotalPages();
                });
            });
        });
    }

    private static void ComponerEncabezado(IContainer contenedor, DocumentoReporte documento)
    {
        contenedor.PaddingBottom(6).BorderBottom(1).BorderColor(Colors.Grey.Medium).Column(columna =>
        {
            columna.Spacing(3);
            columna.Item().Text(documento.Titulo).FontSize(20).Bold();
            columna.Item().Text($"Generado el {documento.GeneradoEl.ToString("dd/MM/yyyy HH:mm", Cultura)} por {documento.GeneradoPor}")
                .FontColor(Colors.Grey.Darken2);

            if (documento.Filtros.Count > 0)
            {
                columna.Item().PaddingTop(4).Table(tabla =>
                {
                    tabla.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(110);
                        c.RelativeColumn();
                    });

                    foreach (var filtro in documento.Filtros)
                    {
                        tabla.Cell().Text(filtro.Nombre + ":").Bold().FontSize(8);
                        tabla.Cell().Text(filtro.Valor).FontSize(8);
                    }
                });
            }
        });
    }

    private static void ComponerGrafico(IContainer contenedor, GraficoReporte grafico, byte[] png)
    {
        // ShowEntire evita que el título quede en una página y la imagen en otra.
        contenedor.ShowEntire().Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Text(grafico.Titulo).FontSize(12).Bold();
            columna.Item().Image(png).FitWidth();
        });
    }

    private static void ComponerTabla(IContainer contenedor, TablaReporte tabla)
    {
        // EnsureSpace: si no quedan ~100 pt en la página (título + encabezado + al menos 2 filas), el bloque entero pasa
        // a la página siguiente, así el título nunca queda huérfano al pie. Es más simple que meter el título en el
        // Header de la tabla y no cambia cómo se reparte el resto de las filas.
        contenedor.EnsureSpace(MinimoParaIniciarTabla).Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Text(tabla.Titulo).FontSize(12).Bold();

            if (tabla.Columnas.Count == 0)
            {
                return;
            }

            // Con muchas columnas la letra baja a 8 pt para que los importes de 9 cifras no se partan en dos renglones.
            var tamanioLetra = tabla.Columnas.Count >= ColumnasParaApaisar ? 8 : 9;

            columna.Item().DefaultTextStyle(x => x.FontSize(tamanioLetra)).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    foreach (var _ in tabla.Columnas)
                    {
                        c.RelativeColumn();
                    }
                });

                // Header(...) se repite en cada página en la que continúa la tabla.
                t.Header(h =>
                {
                    foreach (var col in tabla.Columnas)
                    {
                        var celda = h.Cell().Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(3);
                        Alinear(celda, col.Tipo).Text(col.Encabezado).Bold();
                    }
                });

                foreach (var fila in tabla.Filas)
                {
                    for (var i = 0; i < tabla.Columnas.Count; i++)
                    {
                        var valor = i < fila.Count ? fila[i] : null;
                        var celda = t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
                        Alinear(celda, tabla.Columnas[i].Tipo).Text(Formatear(tabla.Columnas[i].Tipo, valor));
                    }
                }
            });
        });
    }

    private static IContainer Alinear(IContainer celda, TipoColumnaEnum tipo)
    {
        return tipo is TipoColumnaEnum.Entero or TipoColumnaEnum.Moneda or TipoColumnaEnum.Porcentaje
            ? celda.AlignRight()
            : celda;
    }

    /// <summary>Formato de presentación con cultura es-AR explícita (no depende de la configuración del equipo).</summary>
    internal static string Formatear(TipoColumnaEnum tipo, object? valor)
    {
        if (valor is null)
        {
            return SinDato;
        }

        return tipo switch
        {
            TipoColumnaEnum.Entero => Convert.ToInt64(valor, CultureInfo.InvariantCulture).ToString("N0", Cultura),
            TipoColumnaEnum.Moneda => "$ " + Convert.ToDecimal(valor, CultureInfo.InvariantCulture).ToString("#,##0.00", Cultura),
            TipoColumnaEnum.Porcentaje => Convert.ToDecimal(valor, CultureInfo.InvariantCulture).ToString("N2", Cultura) + " %",
            TipoColumnaEnum.Fecha => valor is DateOnly fecha
                ? fecha.ToString("dd/MM/yyyy", Cultura)
                : Convert.ToDateTime(valor, CultureInfo.InvariantCulture).ToString("dd/MM/yyyy", Cultura),
            _ => Convert.ToString(valor, Cultura) ?? SinDato
        };
    }
}
