namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Fila de <c>marcas.csv</c>: la marca con su alias, si tiene uno.
/// </summary>
public sealed record MarcaSemilla(string Marca, string? Alias);
