using Retail.Application.DTOs.Articulos;

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

    /// <summary>Muestra una confirmación Sí/No de advertencia. Retorna true si el usuario confirma.</summary>
    bool Confirmar(string titulo, string mensaje);

    /// <summary>Muestra un mensaje de error modal.</summary>
    void MostrarError(string titulo, string mensaje);
}
