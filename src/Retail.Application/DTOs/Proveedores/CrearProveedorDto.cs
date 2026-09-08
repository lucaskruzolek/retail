namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Parámetros para dar de alta un distribuidor mayorista en el sistema.
/// </summary>
public record class CrearProveedorDto
{
    public required string RazonSocial { get; init; }
    public required string Cuit { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
}
