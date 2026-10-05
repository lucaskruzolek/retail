namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Resultado de leer una planilla de proveedor: las filas interpretadas y las descartadas con su motivo (RF-07).
/// </summary>
public record class ResultadoParseoCatalogoDto
{
    public required IReadOnlyList<ItemCatalogoImportadoDto> Items { get; init; }

    /// <summary>Mensajes con el formato "Fila N: motivo." para las filas que no pudieron interpretarse.</summary>
    public required IReadOnlyList<string> FilasDescartadas { get; init; }
}
