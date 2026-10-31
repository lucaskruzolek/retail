namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Fila de <c>terminos.csv</c>. <see cref="Categoria"/> es <c>null</c> cuando el término es ambiguo (M-27).
/// </summary>
public sealed record TerminoSemilla(string Termino, string? Categoria);
