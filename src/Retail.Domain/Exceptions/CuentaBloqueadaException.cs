namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se opera con una cuenta bloqueada por intentos de autorización fallidos (D-31).
/// </summary>
public class CuentaBloqueadaException : DomainException
{
    public CuentaBloqueadaException()
        : base("La cuenta está bloqueada por intentos de autorización fallidos. Pídale a un Gerente que la desbloquee.")
    {
    }

    public CuentaBloqueadaException(string message)
        : base(message)
    {
    }
}
