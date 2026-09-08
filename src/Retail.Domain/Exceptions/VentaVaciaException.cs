namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta registrar o confirmar una venta sin artículos asociados.
/// </summary>
public class VentaVaciaException : DomainException
{
    public VentaVaciaException()
        : base("No se puede registrar una venta sin artículos.")
    {
    }

    public VentaVaciaException(string message)
        : base(message)
    {
    }
}
