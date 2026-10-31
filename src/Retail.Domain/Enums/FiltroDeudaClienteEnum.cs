namespace Retail.Domain.Enums;

/// <summary>
/// Estados de deuda por los que se filtra el padrón de clientes. Son disjuntos: elegir los dos equivale a no filtrar.
/// </summary>
public enum FiltroDeudaClienteEnum
{
    /// <summary>Saldo de cuenta corriente mayor a cero.</summary>
    ConDeuda = 1,

    /// <summary>Saldo de cuenta corriente igual a cero.</summary>
    SinDeuda = 2
}
