namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando la autenticación falla por nombre de usuario inexistente o contraseña errónea.
/// </summary>
public class CredencialesInvalidasException : DomainException
{
    public CredencialesInvalidasException()
        : base("Nombre de usuario o contraseña incorrectos.")
    {
    }

    public CredencialesInvalidasException(string message)
        : base(message)
    {
    }
}
