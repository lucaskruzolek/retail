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

    /// <summary>Abre el diálogo modal de importación de planillas de un distribuidor. Retorna el resultado si se importó.</summary>
    Task<ResultadoImportacionDto?> AbrirImportarPlanillaAsync(ProveedorDto proveedor);
}
