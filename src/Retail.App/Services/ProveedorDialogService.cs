using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.ViewModels.Proveedores;
using Retail.App.Views.Dialogs;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;

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
        var vm = _serviceProvider.GetRequiredService<ProveedorFormViewModel>();
        vm.ConfigurarAlta();

        var dialog = new ProveedorFormDialog
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var resultado = dialog.ShowDialog();
        return await Task.FromResult(resultado == true && vm.DialogResult);
    }

    public async Task<bool> AbrirFormularioEditarProveedorAsync(ProveedorDto proveedor)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        var vm = _serviceProvider.GetRequiredService<ProveedorFormViewModel>();
        vm.ConfigurarEdicion(proveedor);

        var dialog = new ProveedorFormDialog
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var resultado = dialog.ShowDialog();
        return await Task.FromResult(resultado == true && vm.DialogResult);
    }

    public async Task<bool> AbrirSelectorVinculacionArticuloAsync(CatalogoProveedorDto itemCatalogo)
    {
        ArgumentNullException.ThrowIfNull(itemCatalogo);

        var vm = _serviceProvider.GetRequiredService<VincularArticuloModalViewModel>();
        await vm.InicializarAsync(itemCatalogo);

        var dialog = new VincularArticuloModalDialog
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var resultado = dialog.ShowDialog();
        if (resultado == true && vm.DialogResult && vm.IdArticuloSeleccionado.HasValue)
        {
            var proveedorService = _serviceProvider.GetRequiredService<IProveedorService>();
            await proveedorService.VincularArticuloACatalogoAsync(vm.IdArticuloSeleccionado.Value, itemCatalogo.Id);
            return true;
        }

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

    public async Task AbrirImportadorCatalogosAsync(ProveedorDto? proveedor = null)
    {
        _navigationService.NavigateTo<ImportadorCatalogosView>(view =>
        {
            if (view.DataContext is ImportadorCatalogosViewModel vm)
            {
                _ = vm.InicializarAsync(proveedor);
            }
        });
        await Task.CompletedTask;
    }

    public async Task<ResultadoImportacionDto?> AbrirImportarPlanillaAsync(ProveedorDto proveedor)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        var vm = _serviceProvider.GetRequiredService<ImportarPlanillaViewModel>();
        vm.Inicializar(proveedor);

        var dialog = new ImportarPlanillaDialog
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var resultado = dialog.ShowDialog();
        if (resultado == true && vm.DialogResult && vm.UltimoResultado != null)
        {
            return await Task.FromResult(vm.UltimoResultado);
        }

        return await Task.FromResult<ResultadoImportacionDto?>(null);
    }
}
