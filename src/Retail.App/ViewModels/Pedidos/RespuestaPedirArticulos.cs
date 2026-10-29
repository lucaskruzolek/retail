using Retail.Application.DTOs.Pedidos;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Lo que devuelve el modal de pedido: la acción elegida y, solo si se confirmó, los renglones con su cantidad.
/// </summary>
/// <param name="Accion">Cómo se cerró el modal.</param>
/// <param name="Renglones">Los renglones confirmados; vacío si la acción no es <see cref="AccionPedirArticulosEnum.Confirmar"/>.</param>
public sealed record RespuestaPedirArticulos(AccionPedirArticulosEnum Accion, IReadOnlyList<RenglonAAgregarDto> Renglones)
{
    public static RespuestaPedirArticulos Cancelada { get; } = new(AccionPedirArticulosEnum.Cancelar, []);

    public static RespuestaPedirArticulos IncorporarPrimero { get; } = new(AccionPedirArticulosEnum.IncorporarPrimero, []);

    public static RespuestaPedirArticulos Confirmada(IReadOnlyList<RenglonAAgregarDto> renglones)
    {
        return new RespuestaPedirArticulos(AccionPedirArticulosEnum.Confirmar, renglones);
    }
}
