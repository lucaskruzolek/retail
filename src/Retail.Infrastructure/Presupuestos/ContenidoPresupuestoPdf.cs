using System.Globalization;
using Retail.Application.DTOs.Presupuestos;
using Retail.Infrastructure.Hardware;

namespace Retail.Infrastructure.Presupuestos;

/// <summary>
/// Todos los textos que se imprimen en el PDF de un presupuesto, ya formateados. Es el modelo intermedio entre el
/// <see cref="PresupuestoDto"/> y el compositor de QuestPDF: separa "qué se dice" de "cómo se dibuja", y permite
/// verificar con tests el contenido del documento (QuestPDF comprime las páginas, así que buscar texto en los bytes
/// del PDF no prueba nada). El título (PS-20) no tiene lugar acá: es de uso interno y nunca se entrega al cliente.
/// </summary>
internal sealed record ContenidoPresupuestoPdf(
    string NombreComercio,
    string Direccion,
    string Cuit,
    string CondicionIva,
    string Encabezado,
    string FechaEmision,
    string ValidoHasta,
    string Cliente,
    IReadOnlyList<RenglonPresupuestoPdf> Renglones,
    string Subtotal,
    string? Descuento,
    string Total,
    IReadOnlyList<string> Leyendas)
{
    internal const string ConsumidorFinal = "Consumidor Final";
    internal const string LeyendaNoValidoComoFactura = "Documento no válido como factura";
    internal const string LeyendaPreciosVigentes = "Los precios se mantienen hasta la fecha de validez indicada";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>Todos los textos del documento, en el orden en que se imprimen.</summary>
    public IEnumerable<string> TodosLosTextos()
    {
        yield return NombreComercio;
        yield return Direccion;
        yield return Cuit;
        yield return CondicionIva;
        yield return Encabezado;
        yield return FechaEmision;
        yield return ValidoHasta;
        yield return Cliente;

        foreach (var renglon in Renglones)
        {
            yield return renglon.Cantidad;
            yield return renglon.Descripcion;
            yield return renglon.PrecioUnitario;
            yield return renglon.Subtotal;
        }

        yield return Subtotal;

        if (Descuento is not null)
        {
            yield return Descuento;
        }

        yield return Total;

        foreach (var leyenda in Leyendas)
        {
            yield return leyenda;
        }
    }

    /// <summary>Arma el contenido a partir del presupuesto y de los datos del membrete.</summary>
    public static ContenidoPresupuestoPdf Crear(PresupuestoDto presupuesto, TicketPrinterOptions membrete)
    {
        // La emisión se guarda en UTC; EF la devuelve sin Kind, así que se la declara UTC antes de pasarla a hora local.
        var emisionLocal = DateTime.SpecifyKind(presupuesto.FechaEmision, DateTimeKind.Utc).ToLocalTime();

        return new ContenidoPresupuestoPdf(
            NombreComercio: membrete.NombreComercio,
            Direccion: membrete.Direccion,
            Cuit: $"CUIT: {membrete.Cuit}",
            CondicionIva: membrete.CondicionIva,
            Encabezado: $"PRESUPUESTO N.º {presupuesto.IdPresupuesto:D8}",
            FechaEmision: $"Fecha de emisión: {emisionLocal.ToString("dd/MM/yyyy", Cultura)}",
            ValidoHasta: $"Válido hasta: {presupuesto.FechaVencimiento.ToString("dd/MM/yyyy", Cultura)}",
            Cliente: string.IsNullOrWhiteSpace(presupuesto.ClienteNombre) ? ConsumidorFinal : presupuesto.ClienteNombre,
            Renglones: presupuesto.Items
                .Select(i => new RenglonPresupuestoPdf(
                    i.Cantidad.ToString(Cultura),
                    i.Descripcion,
                    Moneda(i.PrecioUnitarioPactado),
                    Moneda(i.SubtotalItem)))
                .ToList(),
            Subtotal: Moneda(presupuesto.Subtotal),
            Descuento: presupuesto.Descuento > 0m ? $"-{Moneda(presupuesto.Descuento)}" : null,
            Total: Moneda(presupuesto.Total),
            Leyendas: [LeyendaNoValidoComoFactura, LeyendaPreciosVigentes]);
    }

    private static string Moneda(decimal importe)
    {
        return importe.ToString("C0", Cultura);
    }
}

/// <summary>Una fila de la tabla del presupuesto, con sus textos ya formateados.</summary>
internal sealed record RenglonPresupuestoPdf(string Cantidad, string Descripcion, string PrecioUnitario, string Subtotal);
