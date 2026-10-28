using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;

namespace Retail.App.Services;

/// <summary>
/// Contrato para los diálogos modales de la administración de rubros, categorías y marcas (Track 6.5).
/// Desacopla los ViewModels de la instanciación de ventanas WPF.
/// </summary>
public interface IClasificacionDialogService
{
    /// <summary>Abre el formulario de rubro (alta si <paramref name="existente"/> es null). Retorna true si se guardó.</summary>
    bool AbrirFormularioRubro(RubroDto? existente);

    /// <summary>
    /// Abre el formulario de categoría (alta si <paramref name="existente"/> es null). En el alta, el combo
    /// arranca en <paramref name="idRubroPreseleccionado"/>. Retorna true si se guardó.
    /// </summary>
    bool AbrirFormularioCategoria(CategoriaDto? existente, IReadOnlyList<RubroDto> rubros, int? idRubroPreseleccionado);

    /// <summary>Abre el formulario de marca (alta si <paramref name="existente"/> es null). Retorna true si se guardó.</summary>
    bool AbrirFormularioMarca(MarcaDto? existente);

    /// <summary>
    /// Abre el diálogo de administración de la clasificación (rubros, categorías y marcas).
    /// Retorna true si se guardó o dio de baja algún elemento, para que el llamador recargue sus combos.
    /// </summary>
    bool AbrirAdministracion();

    /// <summary>
    /// Abre el formulario de término (Track 6.6): el texto y la categoría, o ninguna si queda ambiguo. Con
    /// <paramref name="existente"/> solo cambia la categoría. Retorna lo cargado, o null si se canceló.
    /// </summary>
    ResultadoFormularioDiccionario? AbrirFormularioTermino(TerminoDto? existente, IReadOnlyList<CategoriaDto> categorias);

    /// <summary>
    /// Abre el formulario de alias de <paramref name="marca"/>, la seleccionada en la sección Marcas (M-68). Retorna
    /// lo cargado, o null si se canceló.
    /// </summary>
    ResultadoFormularioDiccionario? AbrirFormularioAlias(MarcaDto marca);

    /// <summary>
    /// Abre el formulario de prefijo EAN de <paramref name="marca"/>, la seleccionada en la sección Marcas (M-68).
    /// Retorna lo cargado, o null si se canceló.
    /// </summary>
    ResultadoFormularioDiccionario? AbrirFormularioPrefijo(MarcaDto marca);

    /// <summary>
    /// Abre el formulario de par de un término ambiguo: la segunda palabra y su categoría, o ninguna si queda ignorado.
    /// Con <paramref name="existente"/> solo cambia el destino. Retorna lo cargado, o null si se canceló.
    /// </summary>
    ResultadoFormularioDiccionario? AbrirFormularioPar(TerminoDto termino, ParDto? existente, IReadOnlyList<CategoriaDto> categorias);

    /// <summary>Muestra una confirmación Sí/No de advertencia. Retorna true si el usuario confirma.</summary>
    bool Confirmar(string titulo, string mensaje);

    /// <summary>Muestra un mensaje de error modal.</summary>
    void MostrarError(string titulo, string mensaje);
}
