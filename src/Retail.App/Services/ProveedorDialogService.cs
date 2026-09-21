using Microsoft.Extensions.DependencyInjection;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Proveedores;
using System.Windows;

namespace Retail.App.Services;

public class ProveedorDialogService : IProveedorDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;

    public ProveedorDialogService(IServiceProvider serviceProvider, INavigationService navigationService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
    }

    public async Task<bool> AbrirFormularioNuevoProveedorAsync()
    {
        // Omitido por ahora de forma segura ya que el alta se puede gestionar o implementar después.
        MessageBox.Show("Formulario de nuevo proveedor en construcción.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        await Task.CompletedTask;
        return false;
    }

    public async Task<bool> AbrirFormularioEditarProveedorAsync(ProveedorDto proveedor)
    {
        MessageBox.Show($"Editar proveedor: {proveedor.RazonSocial}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        await Task.CompletedTask;
        return false;
    }

    public async Task<bool> ConfirmarEliminacionAsync(string razonSocial)
    {
        var resultado = MessageBox.Show(
            $"¿Está seguro que desea dar de baja al proveedor \"{razonSocial}\"?",
            "Confirmar Baja",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return await Task.FromResult(resultado == MessageBoxResult.Yes);
    }

    public async Task AbrirImportadorCatalogosAsync(ProveedorDto proveedor)
    {
        // Navega a la vista del importador pasando el proveedor activo al ViewModel mediante la configuración del frame
        _navigationService.NavigateTo<ImportadorCatalogosView>(view =>
        {
            if (view.DataContext is ViewModels.Proveedores.ImportadorCatalogosViewModel vm)
            {
                vm.ProveedorActivo = proveedor;
                _ = vm.CargarCatalogoAsync();
            }
        });
        await Task.CompletedTask;
    }
}