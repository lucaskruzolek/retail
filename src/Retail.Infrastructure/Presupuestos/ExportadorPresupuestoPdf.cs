using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Hardware;
using Retail.Infrastructure.Pdf;

namespace Retail.Infrastructure.Presupuestos;

/// <summary>
/// Genera el PDF A4 de un presupuesto con QuestPDF (PS-07): membrete, número, fechas, cliente, tabla de renglones,
/// totales y leyendas. No imprime el título (PS-20). No tiene estado, por lo que se registra como Singleton, y no
/// cierra el <see cref="Stream"/> recibido.
/// </summary>
public sealed class ExportadorPresupuestoPdf : IExportadorPresupuestoPdf
{
    private readonly TicketPrinterOptions _membrete;

    public ExportadorPresupuestoPdf(IOptions<TicketPrinterOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _membrete = opciones.Value;
    }

    public async Task ExportarAsync(PresupuestoDto presupuesto, Stream destino, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presupuesto);
        ArgumentNullException.ThrowIfNull(destino);

        if (!destino.CanWrite)
        {
            throw new ArgumentException("El destino de la exportación debe permitir escritura.", nameof(destino));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var contenido = ContenidoPresupuestoPdf.Crear(presupuesto, _membrete);

        // QuestPDF es síncrono y consume CPU: se saca del hilo de la interfaz (Ley 4), igual que el exportador de reportes.
        await Task.Run(() => CrearDocumento(contenido).GeneratePdf(destino), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Compone el documento QuestPDF leyendo solo el modelo intermedio; no conoce el DTO ni el título.</summary>
    internal static Document CrearDocumento(ContenidoPresupuestoPdf contenido)
    {
        ConfiguracionQuestPdf.Inicializar();

        return Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(t => t.FontFamily(ConfiguracionQuestPdf.FuenteDelDocumento).FontSize(10));

                pagina.Header().Element(c => ComponerMembrete(c, contenido));
                pagina.Content().PaddingVertical(12).Column(columna =>
                {
                    columna.Spacing(14);
                    columna.Item().Element(c => ComponerDatos(c, contenido));
                    columna.Item().Element(c => ComponerTabla(c, contenido));
                    columna.Item().Element(c => ComponerTotales(c, contenido));
                });
                pagina.Footer().Element(c => ComponerPie(c, contenido));
            });
        });
    }

    private static void ComponerMembrete(IContainer contenedor, ContenidoPresupuestoPdf contenido)
    {
        contenedor.PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Column(columna =>
        {
            columna.Spacing(2);
            columna.Item().Text(contenido.NombreComercio).FontSize(16).Bold();
            columna.Item().Text(contenido.Direccion).FontColor(Colors.Grey.Darken2);
            columna.Item().Text($"{contenido.Cuit}  ·  {contenido.CondicionIva}").FontColor(Colors.Grey.Darken2);
        });
    }

    private static void ComponerDatos(IContainer contenedor, ContenidoPresupuestoPdf contenido)
    {
        contenedor.Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Text(contenido.Encabezado).FontSize(18).Bold();
            columna.Item().Text(contenido.FechaEmision);
            columna.Item().Text(contenido.ValidoHasta).Bold();
            columna.Item().PaddingTop(4).Text(contenido.Cliente).FontSize(12);
        });
    }

    private static void ComponerTabla(IContainer contenedor, ContenidoPresupuestoPdf contenido)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(c =>
            {
                c.ConstantColumn(45);
                c.RelativeColumn();
                c.ConstantColumn(90);
                c.ConstantColumn(90);
            });

            tabla.Header(encabezado =>
            {
                encabezado.Cell().Element(CeldaEncabezado).Text("Cant.").Bold();
                encabezado.Cell().Element(CeldaEncabezado).Text("Descripción").Bold();
                encabezado.Cell().Element(CeldaEncabezado).AlignRight().Text("P. unitario").Bold();
                encabezado.Cell().Element(CeldaEncabezado).AlignRight().Text("Subtotal").Bold();
            });

            foreach (var renglon in contenido.Renglones)
            {
                tabla.Cell().Element(CeldaCuerpo).Text(renglon.Cantidad);
                tabla.Cell().Element(CeldaCuerpo).Text(renglon.Descripcion);
                tabla.Cell().Element(CeldaCuerpo).AlignRight().Text(renglon.PrecioUnitario);
                tabla.Cell().Element(CeldaCuerpo).AlignRight().Text(renglon.Subtotal);
            }
        });
    }

    private static void ComponerTotales(IContainer contenedor, ContenidoPresupuestoPdf contenido)
    {
        contenedor.AlignRight().Width(220).Column(columna =>
        {
            columna.Spacing(3);
            columna.Item().Row(fila => ComponerFilaTotal(fila, "Subtotal", contenido.Subtotal, esTotal: false));

            if (contenido.Descuento is not null)
            {
                columna.Item().Row(fila => ComponerFilaTotal(fila, "Descuento", contenido.Descuento, esTotal: false));
            }

            columna.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(3)
                .Row(fila => ComponerFilaTotal(fila, "TOTAL", contenido.Total, esTotal: true));
        });
    }

    private static void ComponerFilaTotal(RowDescriptor fila, string etiqueta, string importe, bool esTotal)
    {
        var tamano = esTotal ? 13 : 10;
        var textoEtiqueta = fila.RelativeItem().Text(etiqueta).FontSize(tamano);
        var textoImporte = fila.AutoItem().Text(importe).FontSize(tamano);

        if (esTotal)
        {
            textoEtiqueta.Bold();
            textoImporte.Bold();
        }
    }

    private static void ComponerPie(IContainer contenedor, ContenidoPresupuestoPdf contenido)
    {
        contenedor.BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(6).Column(columna =>
        {
            foreach (var leyenda in contenido.Leyendas)
            {
                columna.Item().AlignCenter().Text(leyenda).FontSize(8).FontColor(Colors.Grey.Darken2);
            }
        });
    }

    private static IContainer CeldaEncabezado(IContainer contenedor)
    {
        return contenedor.BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4).PaddingHorizontal(2);
    }

    private static IContainer CeldaCuerpo(IContainer contenedor)
    {
        return contenedor.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(2);
    }
}
