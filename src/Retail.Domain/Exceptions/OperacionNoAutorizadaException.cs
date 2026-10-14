namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando el usuario que ejecuta una operación no tiene autoridad para realizarla según su rol
/// (RNF-06). La verifican los servicios de aplicación además de la UI: ocultar un botón no es un control de acceso.
/// </summary>
public class OperacionNoAutorizadaException : DomainException
{
    public OperacionNoAutorizadaException(string message)
        : base(message)
    {
    }
}
