namespace Retail.Application.DTOs.Auth;

/// <summary>
/// Representa el resultado devuelto por el procedimiento almacenado sp_RegistrarIntentoLogin.
/// </summary>
public class ResultadoRegistroIntentoDto
{
    /// <summary>
    /// 0 = Aceptado, 1 = Contraseña errónea / Rechazado, 2 = Cuenta bloqueada.
    /// </summary>
    public byte Resultado { get; init; }

    public int IntentosRestantes { get; init; }

    public DateTime? BloqueadoHasta { get; init; }

    public bool EsExitoso => Resultado == 0;
    public bool EstaBloqueado => Resultado == 2;
}
