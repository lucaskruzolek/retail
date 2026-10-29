using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Fila de la grilla de pedidos. Agrega al <see cref="PedidoResumenDto"/> lo que es solo de presentación: la fecha en
/// hora local (el dominio guarda UTC) y la antigüedad de los pedidos abiertos (R-4 del rector).
/// </summary>
public sealed class PedidoFila
{
    public PedidoFila(PedidoResumenDto resumen, TimeProvider reloj)
    {
        Resumen = resumen ?? throw new ArgumentNullException(nameof(resumen));
        ArgumentNullException.ThrowIfNull(reloj);

        // Un borrador se fecha al crearse; los demás, al emitirse (un cancelado en borrador no tiene emisión).
        var fechaUtc = resumen.FechaEmision ?? resumen.FechaCreacion;
        Fecha = TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, reloj.LocalTimeZone);
        Antiguedad = EstaAbierto ? TextoAntiguedad(Fecha, reloj) : string.Empty;
    }

    public PedidoResumenDto Resumen { get; }

    public int IdPedido => Resumen.IdPedido;

    public string RazonSocialProveedor => Resumen.RazonSocialProveedor;

    public EstadoPedidoEnum Estado => Resumen.Estado;

    public int CantidadRenglones => Resumen.CantidadRenglones;

    public decimal TotalEstimado => Resumen.TotalEstimado;

    /// <summary>Fecha de emisión (o de creación, si no se emitió) en hora local.</summary>
    public DateTime Fecha { get; }

    /// <summary>Días que lleva abierto un borrador o un pedido emitido; vacío si ya está cerrado.</summary>
    public string Antiguedad { get; }

    public bool EstaAbierto => Estado is EstadoPedidoEnum.Borrador or EstadoPedidoEnum.Emitido;

    private static string TextoAntiguedad(DateTime fechaLocal, TimeProvider reloj)
    {
        var hoy = reloj.GetLocalNow().Date;
        var dias = (hoy - fechaLocal.Date).Days;

        return dias switch
        {
            <= 0 => "Hoy",
            1 => "1 día",
            _ => $"{dias} días"
        };
    }
}
