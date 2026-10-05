namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Fila de catálogo de proveedor extraída de la planilla importada.
/// </summary>
public record class ItemCatalogoImportadoDto
{
    /// <summary>Número de fila en la hoja original (el mismo que muestra Excel), para reportar errores al usuario.</summary>
    public required int NumeroFila { get; init; }
    public required string CodigoProveedor { get; init; }
    public string? CodigoBarras { get; init; }
    public required string Descripcion { get; init; }
    public required decimal PrecioCosto { get; init; }
}
