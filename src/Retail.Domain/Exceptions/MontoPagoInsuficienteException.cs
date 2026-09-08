namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando la suma de pagos imputados a una venta es inferior al importe total a abonar.
/// </summary>
public class MontoPagoInsuficienteException : DomainException
{
    public decimal TotalVenta { get; }
    public decimal TotalPagado { get; }
    public decimal Faltante => TotalVenta - TotalPagado;

    public MontoPagoInsuficienteException(decimal totalVenta, decimal totalPagado)
        : base($"El total de los pagos ({totalPagado:C}) es menor al importe total de la venta ({totalVenta:C}). Faltante: {totalVenta - totalPagado:C}.")
    {
        TotalVenta = totalVenta;
        TotalPagado = totalPagado;
    }

    public MontoPagoInsuficienteException(string message)
        : base(message)
    {
    }
}
