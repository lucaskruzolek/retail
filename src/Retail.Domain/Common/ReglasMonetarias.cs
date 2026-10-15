namespace Retail.Domain.Common;

/// <summary>
/// Único punto de redondeo monetario del sistema (D-06): 2 decimales, alejándose del cero en el punto medio.
/// Lo usan los precios del dominio y los importes de los reportes, para que nunca difieran por un centavo.
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
}
