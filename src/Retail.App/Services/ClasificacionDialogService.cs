using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.ViewModels.Articulos;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Articulos;

namespace Retail.App.Services;

public class ClasificacionDialogService : IClasificacionDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public ClasificacionDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public bool AbrirFormularioRubro(RubroDto? existente)
    {
        var vm = _serviceProvider.GetRequiredService<ClasificacionFormViewModel>();
        if (existente is null)
        {
            vm.ConfigurarAltaRubro();
        }
        else
        {
            vm.ConfigurarEdicionRubro(existente);
        }

        return Mostrar(vm);
    }

    public bool AbrirFormularioCategoria(CategoriaDto? existente, IReadOnlyList<RubroDto> rubros, int? idRubroPreseleccionado)
    {
        ArgumentNullException.ThrowIfNull(rubros);

        var vm = _serviceProvider.GetRequiredService<ClasificacionFormViewModel>();
        if (existente is null)
        {
            vm.ConfigurarAltaCategoria(rubros, idRubroPreseleccionado);
        }
        else
        {
            vm.ConfigurarEdicionCategoria(existente, rubros);
        }

        return Mostrar(vm);
    }

    public bool AbrirFormularioMarca(MarcaDto? existente)
    {
        var vm = _serviceProvider.GetRequiredService<ClasificacionFormViewModel>();
        if (existente is null)
        {
            vm.ConfigurarAltaMarca();
        }
        else
        {
            vm.ConfigurarEdicionMarca(existente);
        }

        return Mostrar(vm);
    }

    public bool AbrirAdministracion()
    {
        var vm = _serviceProvider.GetRequiredService<AdministrarClasificacionViewModel>();
        try
        {
            var dialog = _serviceProvider.GetRequiredService<AdministrarClasificacionDialog>();
            dialog.DataContext = vm;
            AsignarPropietario(dialog);
            dialog.ShowDialog();
            return vm.HuboCambios;
        }
        finally
        {
            vm.Dispose();
        }
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        var resultado = MessageBox.Show(
            mensaje,
            titulo,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return resultado == MessageBoxResult.Yes;
    }

    public void MostrarError(string titulo, string mensaje)
    {
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static bool Mostrar(ClasificacionFormViewModel vm)
    {
        var dialog = new ClasificacionFormDialog
        {
            DataContext = vm
        };

        AsignarPropietario(dialog);

        var resultado = dialog.ShowDialog();
        return resultado == true && vm.DialogResult;
    }

    private static void AsignarPropietario(Window dialog)
    {
        // Un diálogo abierto desde otro diálogo debe colgar de la ventana activa y no del Shell.
        var propietario = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;
        if (propietario is { IsVisible: true })
        {
            dialog.Owner = propietario;
        }
    }
}
