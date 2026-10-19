namespace Retail.Application.DTOs.Reportes.Exportacion;

/// <summary>Gráfico del documento, descrito con datos y no con una imagen.</summary>
public record GraficoReporte(string Titulo, TipoGraficoEnum Tipo, IReadOnlyList<string> Etiquetas, IReadOnlyList<SerieGrafico> Series);
