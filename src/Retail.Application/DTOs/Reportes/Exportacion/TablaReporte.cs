namespace Retail.Application.DTOs.Reportes.Exportacion;

/// <summary>
/// Tabla del documento. Las celdas son valores tipados (<see cref="string"/>, <see cref="int"/>,
/// <see cref="decimal"/>, <see cref="DateOnly"/> o <c>null</c>): nunca texto formateado, para que Excel conserve
/// números reales.
/// </summary>
public record TablaReporte(string Titulo, IReadOnlyList<ColumnaReporte> Columnas, IReadOnlyList<IReadOnlyList<object?>> Filas);
