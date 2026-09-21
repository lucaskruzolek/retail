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

    /// <summary>Muestra un diálogo de confirmación de baja lógica. Retorna true si el usuario confirma.</summary>
    Task<bool> ConfirmarEliminacionAsync(string razonSocial);

    /// <summary>Abre la ventana del importador de catálogos para el proveedor dado.</summary>
    Task AbrirImportadorCatalogosAsync(ProveedorDto proveedor);
}
