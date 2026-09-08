namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Información de un proveedor o distribuidor mayorista.
/// </summary>
public record class ProveedorDto
{
    public required int IdProveedor { get; init; }
    public required string RazonSocial { get; init; }
    public required string Cuit { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
}
