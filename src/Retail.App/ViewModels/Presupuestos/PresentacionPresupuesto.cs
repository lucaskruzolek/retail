using System.Globalization;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Reglas de presentación compartidas por la grilla de presupuestos y su detalle: número con ceros, estado visible y
/// fechas en hora local. Es lógica de pantalla (no de negocio), por eso vive en la capa de la UI.
/// </summary>
public static class PresentacionPresupuesto
{
    /// <summary>Número del presupuesto como se imprime: ocho dígitos con ceros a la izquierda (00000123).</summary>
    public static string FormatearNumero(int idPresupuesto)
    {
        return idPresupuesto.ToString("D8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Combina el estado persistido con la vigencia que calculó el servidor: un <c>Pendiente</c> es "Vigente" o "Vencido"
    /// según su fecha; los estados finales se muestran tal cual.
    /// </summary>
    public static EstadoVisualPresupuesto ResolverEstado(EstadoPresupuestoEnum estado, bool estaVigente)
    {
        return estado switch
        {
            EstadoPresupuestoEnum.Pendiente => estaVigente ? EstadoVisualPresupuesto.Vigente : EstadoVisualPresupuesto.Vencido,
            EstadoPresupuestoEnum.Convertido => EstadoVisualPresupuesto.Convertido,
            EstadoPresupuestoEnum.Anulado => EstadoVisualPresupuesto.Anulado,
            EstadoPresupuestoEnum.Reemplazado => EstadoVisualPresupuesto.Reemplazado,
            _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado de presupuesto no soportado.")
        };
    }

    /// <summary>Texto del badge.</summary>
    public static string TextoDe(EstadoVisualPresupuesto estado)
    {
        return estado switch
        {
            EstadoVisualPresupuesto.Vigente => "Vigente",
            EstadoVisualPresupuesto.Vencido => "Vencido",
            EstadoVisualPresupuesto.Convertido => "Convertido",
            EstadoVisualPresupuesto.Anulado => "Anulado",
            EstadoVisualPresupuesto.Reemplazado => "Reemplazado",
            _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado visual no soportado.")
        };
    }

    /// <summary>Un instante UTC (así guarda el dominio) en la hora local del comercio.</summary>
    public static DateTime ALocal(DateTime fechaUtc, TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc), reloj.LocalTimeZone);
    }

    /// <summary>Un día del calendario como <see cref="DateTime"/> a medianoche, para darle formato en XAML.</summary>
    public static DateTime ADia(DateOnly fecha)
    {
        return fecha.ToDateTime(TimeOnly.MinValue);
    }
}
