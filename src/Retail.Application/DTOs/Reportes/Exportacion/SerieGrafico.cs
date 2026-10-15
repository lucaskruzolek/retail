namespace Retail.Application.DTOs.Reportes.Exportacion;

/// <summary>Serie de valores de un gráfico; hay un valor por etiqueta.</summary>
public record SerieGrafico(string Nombre, IReadOnlyList<decimal> Valores);
