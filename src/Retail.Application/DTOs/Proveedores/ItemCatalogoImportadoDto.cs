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

    /// <summary>
    /// Rubro, categoría y marca tal como los escribe el proveedor, si la planilla los trae (Track 6.6, M-12).
    /// </summary>
    public string? RubroProveedor { get; init; }

    public string? CategoriaProveedor { get; init; }

    public string? MarcaProveedor { get; init; }
}
