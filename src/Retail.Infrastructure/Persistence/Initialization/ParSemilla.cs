namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Fila de <c>pares.csv</c>: la categoría que sugiere un término ambiguo seguido de una segunda palabra (M-48).
/// </summary>
public sealed record ParSemilla(string Termino, string SegundaPalabra, string Categoria);
