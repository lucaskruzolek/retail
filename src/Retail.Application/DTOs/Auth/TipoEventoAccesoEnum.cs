namespace Retail.Application.DTOs.Auth;

/// <summary>
/// Qué hecho registra una fila de la auditoría de accesos (columna <c>tipo_evento</c> de AUDITORIA_ACCESOS).
/// </summary>
public enum TipoEventoAccesoEnum : byte
{
    /// <summary>Intento de inicio de sesión (lo registra sp_RegistrarIntentoLogin).</summary>
    InicioSesion = 0,

    /// <summary>Intento de un Administrador de autorizar una restauración con la credencial de un Gerente (D-30).</summary>
    AutorizacionRestauracion = 1
}
