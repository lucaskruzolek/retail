using Retail.Application.DTOs.Presupuestos;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Fila de la grilla de presupuestos. Agrega al <see cref="PresupuestoResumenDto"/> lo que es solo de presentación:
/// número con ceros, fecha de emisión en hora local, cliente con su valor por omisión y el estado del badge.
/// </summary>
public sealed class PresupuestoFila
{
    public PresupuestoFila(PresupuestoResumenDto resumen, TimeProvider reloj)
    {
        Resumen = resumen ?? throw new ArgumentNullException(nameof(resumen));
        ArgumentNullException.ThrowIfNull(reloj);

        EmisionLocal = PresentacionPresupuesto.ALocal(resumen.FechaEmision, reloj);
        Vencimiento = PresentacionPresupuesto.ADia(resumen.FechaVencimiento);
        EstadoVisual = PresentacionPresupuesto.ResolverEstado(resumen.Estado, resumen.EstaVigente);
    }

    public PresupuestoResumenDto Resumen { get; }

    public int IdPresupuesto => Resumen.IdPresupuesto;

    /// <summary>Número como se imprime (00000123).</summary>
    public string Numero => PresentacionPresupuesto.FormatearNumero(Resumen.IdPresupuesto);

    /// <summary>Nota interna (PS-20); vacía si no tiene.</summary>
    public string Titulo => Resumen.Titulo ?? string.Empty;

    /// <summary>El título como tooltip de la celda; <c>null</c> sin título, para que no aparezca un globo vacío.</summary>
    public string? TituloTooltip => string.IsNullOrWhiteSpace(Resumen.Titulo) ? null : Resumen.Titulo;

    public DateTime EmisionLocal { get; }

    /// <summary>Último día de validez (inclusive) a medianoche, listo para formatear como fecha.</summary>
    public DateTime Vencimiento { get; }

    public string ClienteTexto => string.IsNullOrWhiteSpace(Resumen.ClienteNombre) ? "Consumidor Final" : Resumen.ClienteNombre;

    public decimal Total => Resumen.Total;

    public EstadoVisualPresupuesto EstadoVisual { get; }

    public string EstadoTexto => PresentacionPresupuesto.TextoDe(EstadoVisual);

    /// <summary>Solo un presupuesto pendiente (vigente o vencido) se puede anular.</summary>
    public bool PuedeAnular => Resumen.Estado == EstadoPresupuestoEnum.Pendiente;
}
