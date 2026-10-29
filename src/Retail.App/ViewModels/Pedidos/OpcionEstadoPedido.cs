using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Opción del filtro por estado de <c>PedidosView</c>. La opción "Todos" lleva <see cref="Estado"/> nulo: es el valor
/// que la consulta interpreta como "sin filtrar".
/// </summary>
/// <param name="Estado">Estado a filtrar, o <c>null</c> para "Todos".</param>
/// <param name="Texto">Texto que ve el usuario (en plural: "Borradores", "Emitidos").</param>
public sealed record OpcionEstadoPedido(EstadoPedidoEnum? Estado, string Texto)
{
    public static IReadOnlyList<OpcionEstadoPedido> Todas { get; } =
    [
        new(null, "Todos"),
        new(EstadoPedidoEnum.Borrador, "Borradores"),
        new(EstadoPedidoEnum.Emitido, "Emitidos"),
        new(EstadoPedidoEnum.Recibido, "Recibidos"),
        new(EstadoPedidoEnum.Cancelado, "Cancelados")
    ];
}
