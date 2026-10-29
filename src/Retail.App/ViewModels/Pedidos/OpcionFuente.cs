using Retail.Application.DTOs.Pedidos;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Una opción del combo de fuente del modal de pedido (Track 4.3, A-18): proveedor · costo · fecha de la lista, con la
/// principal marcada. La fecha llega en UTC y se muestra en hora local, como el resto de la aplicación: así una lista
/// importada de noche no aparece con la fecha del día siguiente.
/// </summary>
public sealed class OpcionFuente
{
    public OpcionFuente(FuenteRenglonDto fuente)
    {
        Fuente = fuente ?? throw new ArgumentNullException(nameof(fuente));
        FechaLocal = DateTime.SpecifyKind(fuente.FechaActualizacion, DateTimeKind.Utc).ToLocalTime();
    }

    public FuenteRenglonDto Fuente { get; }

    public string RazonSocialProveedor => Fuente.RazonSocialProveedor;

    public decimal CostoActual => Fuente.CostoActual;

    public DateTime FechaLocal { get; }

    public bool EsPrincipal => Fuente.EsPrincipal;
}
