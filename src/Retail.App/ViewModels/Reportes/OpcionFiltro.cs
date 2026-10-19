namespace Retail.App.ViewModels.Reportes;

/// <summary>
/// Opción de un combo de filtro (categoría, marca o cajero). La opción "Todas" lleva <see cref="Id"/> nulo: es el
/// valor que el filtro del servicio interpreta como "sin filtrar".
/// </summary>
/// <param name="Id">Identificador del registro, o <c>null</c> para la opción "Todas".</param>
/// <param name="Nombre">Texto que ve el Gerente.</param>
public sealed record OpcionFiltro(int? Id, string Nombre);
