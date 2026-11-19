using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Retail.Application.DTOs.Listas;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Hardware;
using Retail.Infrastructure.Pdf;

namespace Retail.Infrastructure.Listas;

/// <summary>
/// Genera el PDF A4 de una lista de artículos con QuestPDF (RF-30): membrete, nombre de la lista, leyenda de precios,
/// tabla de renglones disponibles, total y leyenda fiscal. No tiene estado, por lo que se registra como Singleton, y no
/// cierra el <see cref="Stream"/> recibido.
/// </summary>
public sealed class ExportadorListaPdf : IExportadorListaPdf
{
    private readonly TicketPrinterOptions _membrete;

    public ExportadorListaPdf(IOptions<TicketPrinterOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _membrete = opciones.Value;
    }

    public async Task ExportarAsync(ListaDto lista, DateOnly fechaPrecios, Stream destino, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lista);
        ArgumentNullException.ThrowIfNull(destino);

        if (!destino.CanWrite)
        {
            throw new ArgumentException("El destino de la exportación debe permitir escritura.", nameof(destino));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var contenido = ContenidoListaPdf.Crear(lista, fechaPrecios, _membrete);

        // QuestPDF es síncrono y consume CPU: se saca del hilo de la interfaz (Ley 4).
        await Task.Run(() => CrearDocumento(contenido).GeneratePdf(destino), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Compone el documento QuestPDF leyendo solo el modelo intermedio; no conoce el DTO.</summary>
    internal static Document CrearDocumento(ContenidoListaPdf contenido)
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
                    columna.Item().Element(c => ComponerTitulo(c, contenido));
                    columna.Item().Element(c => ComponerTabla(c, contenido));
                    columna.Item().Element(c => ComponerTotal(c, contenido));
                });
                pagina.Footer().Element(c => ComponerPie(c, contenido));
            });
        });
    }

    private static void ComponerMembrete(IContainer contenedor, ContenidoListaPdf contenido)
    {
        contenedor.PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Column(columna =>
        {
            columna.Spacing(2);
            columna.Item().Text(contenido.NombreComercio).FontSize(16).Bold();
            columna.Item().Text(contenido.Direccion).FontColor(Colors.Grey.Darken2);
            columna.Item().Text($"{contenido.Cuit}  ·  {contenido.CondicionIva}").FontColor(Colors.Grey.Darken2);
        });
    }

    private static void ComponerTitulo(IContainer contenedor, ContenidoListaPdf contenido)
    {
        contenedor.Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Text(contenido.Titulo).FontSize(18).Bold();
            columna.Item().Text(contenido.LeyendaPrecios).Bold();
        });
    }

    private static void ComponerTabla(IContainer contenedor, ContenidoListaPdf contenido)
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

    private static void ComponerTotal(IContainer contenedor, ContenidoListaPdf contenido)
    {
        contenedor.AlignRight().Width(220).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(3).Row(fila =>
        {
            fila.RelativeItem().Text("TOTAL").FontSize(13).Bold();
            fila.AutoItem().Text(contenido.Total).FontSize(13).Bold();
        });
    }

    private static void ComponerPie(IContainer contenedor, ContenidoListaPdf contenido)
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
