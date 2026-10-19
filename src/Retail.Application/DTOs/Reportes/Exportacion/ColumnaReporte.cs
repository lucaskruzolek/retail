namespace Retail.Application.DTOs.Reportes.Exportacion;

/// <summary>Encabezado y tipo de una columna de <see cref="TablaReporte"/>.</summary>
public record ColumnaReporte(string Encabezado, TipoColumnaEnum Tipo);
