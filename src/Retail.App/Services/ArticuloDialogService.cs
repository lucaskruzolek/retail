using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.ViewModels.Articulos;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;

namespace Retail.App.Services;

/// <summary>
/// Implementación de los servicios de diálogo modal para el módulo de catálogo de artículos.
/// </summary>
public class ArticuloDialogService : IArticuloDialogService
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<ArticuloDialogService> _logger;

    public ArticuloDialogService(
        IServiceProvider? serviceProvider = null,
        ILogger<ArticuloDialogService>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger ?? NullLogger<ArticuloDialogService>.Instance;
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        var result = MessageBox.Show(
            mensaje,
            titulo,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    public void MostrarError(string titulo, string mensaje)
    {
        _logger.LogError("Diálogo de error en catálogo. Título: {Titulo}, Mensaje: {Mensaje}", titulo, mensaje);

        MessageBox.Show(
            mensaje,
            titulo,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public void MostrarInformacion(string titulo, string mensaje)
    {
        MessageBox.Show(
            mensaje,
            titulo,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public CrearArticuloDto? MostrarDialogoCrear(
        IReadOnlyList<CategoriaDto> categorias,
        IReadOnlyList<MarcaDto> marcas,
        Func<CrearArticuloDto, Task>? onGuardarAsync = null)
    {
        var vm = new ArticuloFormViewModel();
        vm.ConfigurarAlta(categorias, marcas);
        vm.OnAbrirSelectorProveedorAsync = async (texto, idProv) =>
        {
            return await AbrirSelectorCatalogoProveedorAsync(texto, idProv);
        };

        if (onGuardarAsync != null)
        {
            vm.OnGuardarAsync = async () =>
            {
                await onGuardarAsync(vm.ObtenerCrearDto());
            };
        }

        var dialog = new ArticuloFormDialog(vm);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();
        return result == true ? vm.ObtenerCrearDto() : null;
    }

    public ActualizarArticuloDto? MostrarDialogoModificar(
        ArticuloDto articulo,
        IReadOnlyList<CategoriaDto> categorias,
        IReadOnlyList<MarcaDto> marcas,
        Func<ActualizarArticuloDto, Task>? onGuardarAsync = null)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        var vm = new ArticuloFormViewModel();
        vm.ConfigurarEdicion(articulo, categorias, marcas);
        vm.OnAbrirSelectorProveedorAsync = async (texto, idProv) =>
        {
            return await AbrirSelectorCatalogoProveedorAsync(texto, idProv);
        };

        if (onGuardarAsync != null)
        {
            vm.OnGuardarAsync = async () =>
            {
                await onGuardarAsync(vm.ObtenerActualizarDto());
            };
        }

        var dialog = new ArticuloFormDialog(vm);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();
        return result == true ? vm.ObtenerActualizarDto() : null;
    }

    public async Task<CatalogoProveedorDto?> AbrirSelectorCatalogoProveedorAsync(string? textoInicial = null, int? idProveedor = null)
    {
        if (_serviceProvider == null)
        {
            return null;
        }

        var vm = _serviceProvider.GetRequiredService<SeleccionarCatalogoProveedorModalViewModel>();
        await vm.InicializarAsync(textoInicial, idProveedor);

        var dialog = new SeleccionarCatalogoProveedorModalDialog(vm);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();
        return result == true && vm.DialogResult ? vm.ItemSeleccionado : null;
    }
}

