using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.ViewModels.Articulos;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Articulos;

namespace Retail.App.Services;

/// <summary>
/// Implementación de los servicios de diálogo modal para el módulo de catálogo de artículos.
/// </summary>
public class ArticuloDialogService : IArticuloDialogService
{
    private readonly ILogger<ArticuloDialogService> _logger;

    public ArticuloDialogService(ILogger<ArticuloDialogService>? logger = null)
    {
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
}
