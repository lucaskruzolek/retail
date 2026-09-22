using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un distribuidor o proveedor mayorista.
/// </summary>
public class Proveedor : BaseEntity, IAggregateRoot
{
    public string RazonSocial { get; set; } = string.Empty;

    public string Cuit { get; set; } = string.Empty;

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public ICollection<CatalogoProveedor> Catalogos { get; set; } = new List<CatalogoProveedor>();

    public ICollection<Compra> Compras { get; set; } = new List<Compra>();

    public void ActualizarDatos(string razonSocial, string cuit, string? telefono = null, string? email = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(razonSocial);
        ArgumentException.ThrowIfNullOrWhiteSpace(cuit);

        RazonSocial = razonSocial.Trim();
        Cuit = cuit.Trim();
        Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }
}
