using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Representa un rol o perfil de acceso de un operador dentro del sistema (RBAC).
/// </summary>
public class Rol : BaseEntity
{
    public string NombreRol { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
