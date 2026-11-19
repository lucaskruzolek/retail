using System.Globalization;
using Retail.Application.DTOs.Listas;
using Retail.Infrastructure.Hardware;

namespace Retail.Infrastructure.Listas;

/// <summary>
/// Todos los textos que se imprimen en el PDF de una lista de artículos, ya formateados. Es el modelo intermedio
/// entre el <see cref="ListaDto"/> y el compositor de QuestPDF (igual que <c>ContenidoPresupuestoPdf</c>): separa "qué
/// se dice" de "cómo se dibuja" y permite verificar el contenido con tests. A diferencia del presupuesto, el nombre
/// de la lista sí se imprime como título (L-11), y solo salen los renglones disponibles (L-13).
/// </summary>
internal sealed record ContenidoListaPdf(
    string NombreComercio,
    string Direccion,
    string Cuit,
    string CondicionIva,
    string Titulo,
    string LeyendaPrecios,
    IReadOnlyList<RenglonListaPdf> Renglones,
    string Total,
    IReadOnlyList<string> Leyendas)
{
    internal const string LeyendaNoValidoComoFactura = "Documento no válido como factura";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>Todos los textos del documento, en el orden en que se imprimen.</summary>
    public IEnumerable<string> TodosLosTextos()
    {
        yield return NombreComercio;
        yield return Direccion;
        yield return Cuit;
        yield return CondicionIva;
        yield return Titulo;
        yield return LeyendaPrecios;

        foreach (var renglon in Renglones)
        {
            yield return renglon.Cantidad;
            yield return renglon.Descripcion;
            yield return renglon.PrecioUnitario;
            yield return renglon.Subtotal;
        }

        yield return Total;

        foreach (var leyenda in Leyendas)
        {
            yield return leyenda;
        }
    }

    /// <summary>Arma el contenido a partir de la lista, la fecha de los precios y los datos del membrete.</summary>
    public static ContenidoListaPdf Crear(ListaDto lista, DateOnly fechaPrecios, TicketPrinterOptions membrete)
    {
        return new ContenidoListaPdf(
            NombreComercio: membrete.NombreComercio,
            Direccion: membrete.Direccion,
            Cuit: $"CUIT: {membrete.Cuit}",
            CondicionIva: membrete.CondicionIva,
            Titulo: lista.Nombre,
            LeyendaPrecios: $"Precios al {fechaPrecios.ToString("dd/MM/yyyy", Cultura)}, sujetos a modificación",
            Renglones: lista.Renglones
                .Where(r => r.EstaDisponible)
                .Select(r => new RenglonListaPdf(
                    r.Cantidad.ToString(Cultura),
                    r.Descripcion,
                    Moneda(r.PrecioActual),
                    Moneda(r.Subtotal)))
                .ToList(),
            Total: Moneda(lista.TotalEstimado),
            Leyendas: [LeyendaNoValidoComoFactura]);
    }

    private static string Moneda(decimal importe)
    {
        return importe.ToString("C0", Cultura);
    }
}

/// <summary>Una fila de la tabla de la lista, con sus textos ya formateados.</summary>
internal sealed record RenglonListaPdf(string Cantidad, string Descripcion, string PrecioUnitario, string Subtotal);
