using Retail.Application.DTOs.Articulos;

namespace Retail.App.Services;

/// <summary>
/// Contrato para la interacción con diálogos y ventanas modales del catálogo de artículos.
/// Desacopla la lógica de presentación (ViewModels) de la instanciación de ventanas WPF para facilitar pruebas unitarias.
/// </summary>
public interface IArticuloDialogService
{
    bool Confirmar(string titulo, string mensaje);

    void MostrarError(string titulo, string mensaje);

    void MostrarInformacion(string titulo, string mensaje);

    CrearArticuloDto? MostrarDialogoCrear(
        IReadOnlyList<CategoriaDto> categorias,
        IReadOnlyList<MarcaDto> marcas,
        Func<CrearArticuloDto, Task>? onGuardarAsync = null);

    ActualizarArticuloDto? MostrarDialogoModificar(
        ArticuloDto articulo,
        IReadOnlyList<CategoriaDto> categorias,
        IReadOnlyList<MarcaDto> marcas,
        Func<ActualizarArticuloDto, Task>? onGuardarAsync = null);
}
