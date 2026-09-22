namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Configuración de mapeo de columnas para la importación masiva de planillas Excel/CSV de proveedores.
/// </summary>
public record class MapeoColumnasDto
{
    public required int IdProveedor { get; init; }
    public required string ColumnaCodigo { get; init; }
    public string? ColumnaCodigoBarras { get; init; }
    public required string ColumnaDescripcion { get; init; }
    public required string ColumnaPrecioCosto { get; init; }
    public int FilaInicial { get; init; } = 2;
}
