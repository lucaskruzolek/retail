using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.ViewModels.Proveedores;
using Retail.App.Views.Dialogs;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Clasificacion;
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
            await proveedorService.VincularArticuloACatalogoAsync(
                vm.IdArticuloSeleccionado.Value,
                itemCatalogo.Id,
                vm.ComoPrincipal);
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

    public async Task AbrirEquivalenciasProveedorAsync(ProveedorDto proveedor)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        var vm = _serviceProvider.GetRequiredService<EquivalenciasProveedorViewModel>();
        try
        {
            vm.Configurar(proveedor);

            var dialog = new EquivalenciasProveedorDialog
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current.MainWindow
            };

            dialog.ShowDialog();
        }
        finally
        {
            vm.Dispose();
        }

        await Task.CompletedTask;
    }

    public async Task<bool> AbrirAsociarValoresCatalogoAsync(ProveedorDto proveedor, IReadOnlyList<ValorSinTraducirDto> valores)
    {
        ArgumentNullException.ThrowIfNull(proveedor);
        ArgumentNullException.ThrowIfNull(valores);

        var vm = _serviceProvider.GetRequiredService<AsociarValoresCatalogoViewModel>();
        try
        {
            vm.Inicializar(proveedor, valores);
            await vm.CargarDestinosAsync();

            var dialog = new AsociarValoresCatalogoDialog
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current.MainWindow
            };

            var resultado = dialog.ShowDialog();
            return resultado == true && vm.DialogResult;
        }
        finally
        {
            vm.Dispose();
        }
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

    public bool MostrarIncorporacion(IncorporarArticulosModalViewModel modal)
    {
        ArgumentNullException.ThrowIfNull(modal);

        var dialog = new IncorporarArticulosModalDialog
        {
            DataContext = modal,
            Owner = System.Windows.Application.Current.MainWindow
        };

        // Las sugerencias se calculan en segundo plano mientras el diálogo ya está abierto (Ley 4).
        _ = modal.IniciarAsync();
        var resultado = dialog.ShowDialog();
        modal.Finalizar();

        return resultado == true && modal.DialogResult;
    }
}
