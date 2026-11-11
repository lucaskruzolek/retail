using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Ajustes;

/// <summary>Una política de redondeo del precio de venta tal como la muestra el selector de Ajustes (D-38).</summary>
/// <param name="Valor">Política del Dominio.</param>
/// <param name="Texto">Nombre legible, por ejemplo "Al múltiplo de 10 superior".</param>
public sealed record OpcionRedondeoPrecio(RedondeoPrecioEnum Valor, string Texto)
{
    /// <summary>Las siete políticas, en el orden del selector.</summary>
    public static IReadOnlyList<OpcionRedondeoPrecio> Todas { get; } =
    [
        new(RedondeoPrecioEnum.EnteroMasCercano, "Al peso entero más cercano"),
        new(RedondeoPrecioEnum.MultiploDe5Superior, "Al múltiplo de 5 superior"),
        new(RedondeoPrecioEnum.MultiploDe5Inferior, "Al múltiplo de 5 inferior"),
        new(RedondeoPrecioEnum.MultiploDe10Superior, "Al múltiplo de 10 superior"),
        new(RedondeoPrecioEnum.MultiploDe10Inferior, "Al múltiplo de 10 inferior"),
        new(RedondeoPrecioEnum.MultiploDe100Superior, "Al múltiplo de 100 superior"),
        new(RedondeoPrecioEnum.MultiploDe100Inferior, "Al múltiplo de 100 inferior"),
    ];
}
