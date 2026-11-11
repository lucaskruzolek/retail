using Retail.Domain.Enums;

namespace Retail.Domain.Common;

/// <summary>
/// Reglas de redondeo monetario del sistema.
/// <list type="bullet">
/// <item><description><see cref="Redondear"/> (D-06): 2 decimales, alejándose del cero en el punto medio. Lo usan los
/// costos, los subtotales de compras y pedidos, los porcentajes y los importes de los reportes, para que nunca difieran
/// por un centavo.</description></item>
/// <item><description><see cref="RedondearPrecio"/> (D-38): el precio de venta que resulta del markup, sin centavos y
/// según la política global del comercio (<see cref="RedondeoPrecioEnum"/>).</description></item>
/// </list>
/// </summary>
public static class ReglasMonetarias
{
    /// <summary>Redondea un importe o porcentaje a 2 decimales (el punto medio se aleja del cero).</summary>
    /// <param name="importe">Valor a redondear.</param>
    /// <returns>El valor con 2 decimales.</returns>
    public static decimal Redondear(decimal importe)
    {
        return Math.Round(importe, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Redondea un precio de venta a pesos enteros según la política del comercio. Las políticas "inferior" nunca dejan el
    /// precio por debajo de <paramref name="minimo"/> (el costo): si el múltiplo inferior lo hiciera, se usa el superior,
    /// porque redondear no puede convertir un markup positivo en una venta a pérdida (ni en un precio cero).
    /// </summary>
    /// <param name="precio">Precio sin redondear (costo × (1 + markup)); no negativo.</param>
    /// <param name="redondeo">Política de redondeo vigente.</param>
    /// <param name="minimo">Valor por debajo del cual el precio no puede quedar (el costo de reposición); no negativo.</param>
    /// <returns>El precio redondeado: un entero (múltiplo de 1, 5, 10 o 100 según la política).</returns>
    public static decimal RedondearPrecio(decimal precio, RedondeoPrecioEnum redondeo, decimal minimo)
    {
        if (precio < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(precio), "El precio no puede ser negativo.");
        }

        if (minimo < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minimo), "El mínimo no puede ser negativo.");
        }

        var (multiplo, sentido) = redondeo switch
        {
            RedondeoPrecioEnum.EnteroMasCercano => (1m, SentidoRedondeo.Cercano),
            RedondeoPrecioEnum.MultiploDe5Superior => (5m, SentidoRedondeo.Superior),
            RedondeoPrecioEnum.MultiploDe5Inferior => (5m, SentidoRedondeo.Inferior),
            RedondeoPrecioEnum.MultiploDe10Superior => (10m, SentidoRedondeo.Superior),
            RedondeoPrecioEnum.MultiploDe10Inferior => (10m, SentidoRedondeo.Inferior),
            RedondeoPrecioEnum.MultiploDe100Superior => (100m, SentidoRedondeo.Superior),
            RedondeoPrecioEnum.MultiploDe100Inferior => (100m, SentidoRedondeo.Inferior),
            _ => throw new ArgumentOutOfRangeException(nameof(redondeo), redondeo, "La política de redondeo de precios no es válida.")
        };

        var redondeado = AlMultiplo(precio, multiplo, sentido);

        // El múltiplo superior siempre es >= precio, y el precio de un markup no negativo es >= costo.
        return redondeado < minimo ? AlMultiplo(precio, multiplo, SentidoRedondeo.Superior) : redondeado;
    }

    private static decimal AlMultiplo(decimal precio, decimal multiplo, SentidoRedondeo sentido)
    {
        var cociente = precio / multiplo;
        var entero = sentido switch
        {
            SentidoRedondeo.Superior => Math.Ceiling(cociente),
            SentidoRedondeo.Inferior => Math.Floor(cociente),
            _ => Math.Round(cociente, 0, MidpointRounding.AwayFromZero)
        };

        return entero * multiplo;
    }

    private enum SentidoRedondeo
    {
        Cercano,
        Superior,
        Inferior,
    }
}
