namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando una cuenta de usuario se encuentra temporalmente bloqueada
/// debido a la superación del límite de intentos fallidos de inicio de sesión.
/// </summary>
public class CuentaBloqueadaException : DomainException
{
    public DateTime? BloqueadoHastaUtc { get; }

    public CuentaBloqueadaException(DateTime? bloqueadoHastaUtc = null)
        : base(bloqueadoHastaUtc.HasValue
            ? $"La cuenta se encuentra temporalmente bloqueada por reiterados intentos fallidos hasta las {bloqueadoHastaUtc.Value.ToLocalTime():HH:mm}."
            : "La cuenta se encuentra temporalmente bloqueada por reiterados intentos fallidos.")
    {
        BloqueadoHastaUtc = bloqueadoHastaUtc;
    }
}
