using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Proveedores;

namespace Retail.App.Services;

/// <summary>
/// Contrato para la interacción con diálogos y ventanas modales del módulo de Proveedores.
/// Desacopla la lógica de presentación (ViewModels) de la instanciación de ventanas WPF.
/// </summary>
public interface IProveedorDialogService
{
    /// <summary>Abre el formulario modal de alta de proveedor. Retorna true si se guardó exitosamente.</summary>
    Task<bool> AbrirFormularioNuevoProveedorAsync();

    /// <summary>Abre el formulario modal de edición de proveedor. Retorna true si se guardó exitosamente.</summary>
    Task<bool> AbrirFormularioEditarProveedorAsync(ProveedorDto proveedor);

    /// <summary>Abre el diálogo modal asistido para vincular un ítem mayorista a un artículo existente en tienda (RF-05).</summary>
    Task<bool> AbrirSelectorVinculacionArticuloAsync(CatalogoProveedorDto itemCatalogo);

    /// <summary>Muestra un diálogo de confirmación de baja lógica. Retorna true si el usuario confirma.</summary>
    Task<bool> ConfirmarEliminacionAsync(string razonSocial);

    /// <summary>Abre la ventana del importador de catálogos para el proveedor dado (o catálogo general si es null).</summary>
    Task AbrirImportadorCatalogosAsync(ProveedorDto? proveedor = null);

    /// <summary>Abre el diálogo modal de equivalencias de clasificación del proveedor (Track 6.6, P6-7).</summary>
    Task AbrirEquivalenciasProveedorAsync(ProveedorDto proveedor);

    /// <summary>Abre el diálogo modal de importación de planillas de un distribuidor. Retorna el resultado si se importó.</summary>
    Task<ResultadoImportacionDto?> AbrirImportarPlanillaAsync(ProveedorDto proveedor);

    /// <summary>
    /// Abre el modal de incorporación con el ViewModel ya inicializado por la pantalla (G5 del Track 4.3): calcula las
    /// sugerencias con el diálogo abierto y las cancela al cerrarlo. Devuelve <c>true</c> si el usuario confirmó; la
    /// incorporación la guarda quien lo abre, con <see cref="IncorporarArticulosModalViewModel.ObtenerDto"/>.
    /// </summary>
    bool MostrarIncorporacion(IncorporarArticulosModalViewModel modal);
}
