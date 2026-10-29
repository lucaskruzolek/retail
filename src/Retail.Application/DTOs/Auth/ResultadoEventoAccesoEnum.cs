namespace Retail.Application.DTOs.Auth;

/// <summary>
/// Desenlace de un evento de acceso (columna <c>resultado</c> de AUDITORIA_ACCESOS). Los códigos son los mismos que
/// devuelve sp_RegistrarIntentoLogin.
/// </summary>
public enum ResultadoEventoAccesoEnum : byte
{
    /// <summary>El acceso o la autorización fueron válidos.</summary>
    Aceptado = 0,

    /// <summary>Credencial incorrecta.</summary>
    Rechazado = 1,

    /// <summary>La cuenta está bloqueada, o este intento la bloqueó.</summary>
    Bloqueado = 2
}
