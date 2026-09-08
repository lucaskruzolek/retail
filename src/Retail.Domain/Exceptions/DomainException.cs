namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción base para todas las violaciones de reglas de negocio e invariantes del dominio.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
