namespace Retail.Domain.Enums;

/// <summary>
/// Condición del cliente frente al Impuesto al Valor Agregado (IVA).
/// </summary>
public enum CondicionIvaEnum
{
    ConsumidorFinal = 1,
    ResponsableInscripto = 2,
    Monotributo = 3,
    Exento = 4
}
