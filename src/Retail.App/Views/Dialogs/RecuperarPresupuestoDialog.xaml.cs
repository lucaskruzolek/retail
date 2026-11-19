using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Presupuestos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// "Recuperar presupuesto" del Punto de venta (RF-12): buscador y lista de presupuestos pendientes. Enter (en el buscador
/// o en la grilla) o doble clic recuperan la fila seleccionada; la flecha abajo pasa del buscador a la grilla. Se cierra
/// solo cuando recuperó; si falla, muestra el motivo dentro del diálogo.
/// </summary>
public partial class RecuperarPresupuestoDialog : FluentWindow
{
    public RecuperarPresupuestoViewModel ViewModel { get; }

    public RecuperarPresupuestoDialog(RecuperarPresupuestoViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Loaded += OnLoaded;
    }

    // La primera carga se dispara al mostrarse la ventana, igual que en SeleccionarClienteModalDialog.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TxtBusqueda.Focus();
        _ = ViewModel.CargarInicialAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecuperarPresupuestoViewModel.Recuperado) && ViewModel.Recuperado)
        {
            Close();
        }
    }

    private void OnBusquedaPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = ViewModel.ConfirmarAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && ViewModel.Presupuestos.Count > 0)
        {
            PasarFocoALaGrilla();
            e.Handled = true;
        }
    }

    private void OnGrillaPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // La grilla se queda con Enter para bajar de fila: se lo intercepta para recuperar la seleccionada
        if (e.Key == Key.Enter)
        {
            _ = ViewModel.ConfirmarAsync();
            e.Handled = true;
        }
    }

    private void OnGrillaMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Solo una fila: el doble clic en el encabezado (ordenar) no recupera nada
        if (e.OriginalSource is DependencyObject origen
            && ItemsControl.ContainerFromElement(Grilla, origen) is DataGridRow fila
            && fila.Item is PresupuestoFila)
        {
            _ = ViewModel.ConfirmarAsync();
        }
    }

    private void PasarFocoALaGrilla()
    {
        ViewModel.PresupuestoSeleccionado ??= ViewModel.Presupuestos[0];
        Grilla.UpdateLayout();

        if (Grilla.ItemContainerGenerator.ContainerFromItem(ViewModel.PresupuestoSeleccionado) is DataGridRow fila)
        {
            fila.Focus();
        }
        else
        {
            Grilla.Focus();
        }
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
