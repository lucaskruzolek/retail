namespace Retail.Application.Services.Reportes;

/// <summary>
/// Nombres legibles de los ids presentes en el filtro de un reporte (D-24). Un valor <see langword="null"/> significa
/// que el filtro no tiene ese id y el documento muestra "Todas" o "Todos".
/// </summary>
internal sealed record NombresFiltro(string? Categoria, string? Marca, string? Cajero);
