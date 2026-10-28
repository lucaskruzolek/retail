namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Contenido de la semilla de diccionarios de clasificación (Track 6.6, M-59) tal como la leyó y validó
/// <see cref="LectorSemillaDiccionarios"/>: una lista por archivo, con los textos ya en la forma que exigen las reglas
/// del Dominio. La carga la hace <c>SemilleroDiccionarios</c>.
/// </summary>
public sealed record SemillaDiccionarios(
    IReadOnlyList<CategoriaSemilla> Categorias,
    IReadOnlyList<TerminoSemilla> Terminos,
    IReadOnlyList<MarcaSemilla> Marcas,
    IReadOnlyList<PrefijoSemilla> Prefijos,
    IReadOnlyList<ParSemilla> Pares);
