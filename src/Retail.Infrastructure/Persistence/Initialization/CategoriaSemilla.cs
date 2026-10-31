namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Fila de <c>taxonomia.csv</c>: una categoría con el rubro al que pertenece.
/// </summary>
public sealed record CategoriaSemilla(string Rubro, string Categoria);
