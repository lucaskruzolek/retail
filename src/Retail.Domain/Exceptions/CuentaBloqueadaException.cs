namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se opera con una cuenta bloqueada. Hay dos causas, con políticas distintas:
/// <list type="bullet">
/// <item>Intentos fallidos de inicio de sesión: bloqueo <b>temporal</b> que vence solo
/// (<see cref="BloqueadoHastaUtc"/>, política de <c>sp_RegistrarIntentoLogin</c>).</item>
/// <item>Autorizaciones fallidas de un Administrador con la credencial de un Gerente: bloqueo <b>permanente</b> hasta que
/// un Gerente lo levanta (<see cref="PorAutorizacionesFallidas"/>, D-31 del Track 6.3.1).</item>
/// </list>
/// </summary>
public class CuentaBloqueadaException : DomainException
{
    public CuentaBloqueadaException(DateTime? bloqueadoHastaUtc = null)
        : base(bloqueadoHastaUtc.HasValue
            ? $"La cuenta se encuentra temporalmente bloqueada por reiterados intentos fallidos hasta las {bloqueadoHastaUtc.Value.ToLocalTime():HH:mm}."
            : "La cuenta se encuentra temporalmente bloqueada por reiterados intentos fallidos.")
    {
        BloqueadoHastaUtc = bloqueadoHastaUtc;
    }

    private CuentaBloqueadaException(string message, bool requiereDesbloqueoDeGerente)
        : base(message)
    {
        RequiereDesbloqueoDeGerente = requiereDesbloqueoDeGerente;
    }

    /// <summary>
    /// Fin del bloqueo temporal por inicio de sesión; <c>null</c> si no se informó o si el bloqueo es permanente.
    /// </summary>
    public DateTime? BloqueadoHastaUtc { get; }

    /// <summary>
    /// Indica que el bloqueo no vence solo: solo un Gerente puede levantarlo (D-31).
    /// </summary>
    public bool RequiereDesbloqueoDeGerente { get; }

    /// <summary>
    /// Bloqueo permanente por autorizaciones fallidas con la credencial de un Gerente (D-31). Es un método de fábrica
    /// con nombre porque el constructor sin argumentos ya significa "bloqueo temporal".
    /// </summary>
    public static CuentaBloqueadaException PorAutorizacionesFallidas() => new(
        "La cuenta está bloqueada por intentos de autorización fallidos. Pídale a un Gerente que la desbloquee.",
        requiereDesbloqueoDeGerente: true);
}
