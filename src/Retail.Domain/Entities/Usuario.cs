using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un operador del sistema con credenciales protegidas y rol asignado.
/// </summary>
public class Usuario : BaseEntity, IAggregateRoot
{
    public string NombreUsuario { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public int IdRol { get; set; }

    public Rol? Rol { get; set; }

    public ICollection<TurnoCaja> TurnosCaja { get; set; } = new List<TurnoCaja>();

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    public ICollection<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();

    public ICollection<Compra> Compras { get; set; } = new List<Compra>();

    public ICollection<CobranzaCliente> CobranzasClientes { get; set; } = new List<CobranzaCliente>();
}
