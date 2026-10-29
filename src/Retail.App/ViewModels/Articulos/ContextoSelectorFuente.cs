namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Datos del formulario de artículo que el selector de fuentes necesita para aplicar las reglas (Track 4.3, A-19):
/// la tarjeta "Artículo" de arriba, el artículo dueño (0 en el alta, A-1/M6) y los ítems que el formulario ya tiene.
/// </summary>
/// <param name="IdArticulo">Id del artículo que se edita; 0 si todavía es un alta.</param>
/// <param name="Descripcion">Descripción tipeada en el formulario (puede estar vacía en el alta).</param>
/// <param name="CodigoBarras">Código de barras del formulario, si lo hay.</param>
/// <param name="IdsEnFormulario">Ids de catálogo de las fuentes ya presentes en el formulario: no se vuelven a ofrecer.</param>
public sealed record ContextoSelectorFuente(
    int IdArticulo,
    string? Descripcion,
    string? CodigoBarras,
    IReadOnlyCollection<int> IdsEnFormulario);
