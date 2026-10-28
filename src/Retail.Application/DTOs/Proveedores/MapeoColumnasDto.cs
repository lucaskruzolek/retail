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

    /// <summary>
    /// Columnas opcionales con la clasificación del proveedor (Track 6.6, M-12). Como el código de barras, si la
    /// planilla no tiene la columna se importa sin ese dato.
    /// </summary>
    public string? ColumnaRubro { get; init; }

    public string? ColumnaCategoria { get; init; }

    public string? ColumnaMarca { get; init; }

    /// <summary>Fila de la hoja donde están los encabezados (el mismo número que muestra Excel); los datos empiezan en la siguiente.</summary>
    public int FilaEncabezado { get; init; } = 1;

    /// <summary>Extensión del archivo de origen (".xlsx" o ".csv"); define el formato con el que se lee el stream.</summary>
    public string ExtensionArchivo { get; init; } = ".xlsx";
}
