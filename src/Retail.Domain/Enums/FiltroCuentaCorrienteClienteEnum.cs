namespace Retail.Domain.Enums;

/// <summary>
/// Estados de cuenta corriente por los que se filtra el padrón de clientes. Son disjuntos: elegir los dos equivale a
/// no filtrar.
/// </summary>
public enum FiltroCuentaCorrienteClienteEnum
{
    /// <summary>El cliente tiene la cuenta corriente habilitada.</summary>
    ConCuentaCorriente = 1,

    /// <summary>El cliente no tiene la cuenta corriente habilitada.</summary>
    SinCuentaCorriente = 2
}
