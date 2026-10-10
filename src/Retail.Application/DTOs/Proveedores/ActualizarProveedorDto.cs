namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Parámetros para modificar los datos de un distribuidor mayorista. Es un comando propio y no reutiliza
/// <see cref="ProveedorDto"/>, que es el resultado de una consulta: separar lectura y escritura (CQS) evita que
/// un campo agregado para mostrar en la grilla pase a ser editable sin querer.
/// </summary>
public record class ActualizarProveedorDto
{
    public required int IdProveedor { get; init; }
    public required string RazonSocial { get; init; }
    public required string Cuit { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
}
