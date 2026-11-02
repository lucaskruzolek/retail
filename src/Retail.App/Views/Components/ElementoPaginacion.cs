namespace Retail.App.Views.Components;

/// <summary>
/// Un lugar de la lista de páginas de <see cref="PaginationBar"/>: un número de página, o un "…" cuando
/// <see cref="Numero"/> es nulo (páginas salteadas).
/// </summary>
public sealed record class ElementoPaginacion(int? Numero, bool EsActual)
{
    public bool EsSalto => Numero is null;
}
