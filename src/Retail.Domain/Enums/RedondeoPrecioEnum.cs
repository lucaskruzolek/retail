namespace Retail.Domain.Enums;

/// <summary>
/// Cómo se redondea el precio de venta que resulta de aplicar el markup al costo (D-38). Es una política global del
/// comercio (<see cref="Entities.ConfiguracionComercial"/>): en la Argentina ya no se cobran centavos, y muchos comercios
/// prefieren además precios "redondos". Solo afecta al precio de venta; los costos y los porcentajes conservan dos
/// decimales (<see cref="Common.ReglasMonetarias.Redondear"/>).
/// </summary>
public enum RedondeoPrecioEnum
{
    /// <summary>Al peso entero más cercano ($ 1.663,40 → $ 1.663); el punto medio se aleja del cero.</summary>
    EnteroMasCercano = 0,

    /// <summary>Al múltiplo de 5 igual o superior ($ 1.663,40 → $ 1.665).</summary>
    MultiploDe5Superior = 1,

    /// <summary>Al múltiplo de 5 igual o inferior ($ 1.663,40 → $ 1.660).</summary>
    MultiploDe5Inferior = 2,

    /// <summary>Al múltiplo de 10 igual o superior ($ 1.663,40 → $ 1.670).</summary>
    MultiploDe10Superior = 3,

    /// <summary>Al múltiplo de 10 igual o inferior ($ 1.663,40 → $ 1.660).</summary>
    MultiploDe10Inferior = 4,

    /// <summary>Al múltiplo de 100 igual o superior ($ 1.663,40 → $ 1.700).</summary>
    MultiploDe100Superior = 5,

    /// <summary>Al múltiplo de 100 igual o inferior ($ 1.663,40 → $ 1.600).</summary>
    MultiploDe100Inferior = 6,
}
