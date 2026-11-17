using System.Globalization;
using Retail.App.ViewModels.Presupuestos;

namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// Presupuesto del que viene el ticket del POS (RF-12): lo que la etiqueta muestra y el número que el cobro manda al
/// servidor como <c>IdPresupuestoOrigen</c>.
/// </summary>
/// <param name="Id">Número del presupuesto.</param>
/// <param name="Titulo">Nota interna (PS-20); solo se ve dentro del sistema, en el tooltip de la etiqueta.</param>
/// <param name="Vigente">Si venció al recuperarlo, el ticket quedó a precios de catálogo y sin topes (PS-03).</param>
/// <param name="FechaVencimiento">Último día de validez, inclusive.</param>
public sealed record PresupuestoCargado(int Id, string? Titulo, bool Vigente, DateOnly FechaVencimiento)
{
    public string Numero => PresentacionPresupuesto.FormatearNumero(Id);

    /// <summary>Texto de la etiqueta: "Presupuesto N.º 00000123 · vigente hasta 02/12" o "· vencido: precios de catálogo".</summary>
    public string Etiqueta => Vigente
        ? $"Presupuesto N.º {Numero} · vigente hasta {FechaVencimiento.ToString("dd/MM", CultureInfo.InvariantCulture)}"
        : $"Presupuesto N.º {Numero} · vencido: precios de catálogo";

    /// <summary>Tooltip de la etiqueta: el título interno, si existe.</summary>
    public string Detalle => string.IsNullOrWhiteSpace(Titulo)
        ? Etiqueta
        : $"{Etiqueta}\nTítulo interno: {Titulo}";
}
