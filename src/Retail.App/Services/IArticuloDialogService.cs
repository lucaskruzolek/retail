using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;

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

    Task<CatalogoProveedorDto?> AbrirSelectorCatalogoProveedorAsync(string? textoInicial = null, int? idProveedor = null);

    /// <summary>
    /// Alta de una presentación derivada del artículo indicado (RF-21). Devuelve los datos guardados o null si se canceló.
    /// </summary>
    CrearPresentacionDto? MostrarDialogoCrearPresentacion(
        ArticuloDto origen,
        Func<CrearPresentacionDto, Task>? onGuardarAsync = null);

    /// <summary>
    /// Fraccionamiento del origen en la presentación indicada (RF-21). Devuelve las unidades obtenidas o null si se canceló.
    /// </summary>
    int? MostrarDialogoFraccionar(
        ArticuloDto derivado,
        ArticuloDto origen,
        Func<FraccionarDto, Task<int>>? onConfirmarAsync = null);
}
