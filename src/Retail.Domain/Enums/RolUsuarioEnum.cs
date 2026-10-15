namespace Retail.Domain.Enums;

/// <summary>
/// Roles de acceso al sistema con permisos basados en RBAC.
/// Los valores coinciden con los identificadores de la tabla de roles: no se renumeran, solo se agregan.
/// </summary>
public enum RolUsuarioEnum
{
    Cajero = 1,
    Encargado = 2,
    Gerente = 3,

    /// <summary>
    /// Soporte técnico opcional: gestiona Cajeros y Encargados, pero no opera el negocio. En los respaldos solo puede
    /// consultar el historial de copias y la última restauración: no genera ni restaura copias (RF-03, RF-22).
    /// </summary>
    Administrador = 4
}
