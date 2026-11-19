using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Listas;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// "Cargar → Lista" del Punto de venta (L-05): buscador y lista de las listas de artículos. Enter (en el buscador o en la
/// grilla) o doble clic eligen la fila seleccionada; la flecha abajo pasa del buscador a la grilla; Esc cancela (IsCancel del botón Cancelar). Se cierra
/// solo cuando hay una lista elegida.
/// </summary>
public partial class SeleccionarListaDialog : FluentWindow
{
    public SeleccionarListaViewModel ViewModel { get; }

    public SeleccionarListaDialog(SeleccionarListaViewModel viewModel)
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

    // La primera carga se dispara al mostrarse la ventana, igual que en RecuperarPresupuestoDialog.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TxtBusqueda.Focus();
        _ = ViewModel.CargarInicialAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SeleccionarListaViewModel.Elegida) && ViewModel.Elegida)
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
        else if (e.Key == Key.Down && ViewModel.Listas.Count > 0)
        {
            PasarFocoALaGrilla();
            e.Handled = true;
        }
    }

    private void OnGrillaPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // La grilla se queda con Enter para bajar de fila: se lo intercepta para elegir la seleccionada
        if (e.Key == Key.Enter)
        {
            _ = ViewModel.ConfirmarAsync();
            e.Handled = true;
        }
    }

    private void OnGrillaMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Solo una fila: el doble clic en el encabezado (ordenar) no elige nada
        if (e.OriginalSource is DependencyObject origen
            && ItemsControl.ContainerFromElement(Grilla, origen) is DataGridRow fila
            && fila.Item is ListaFila)
        {
            _ = ViewModel.ConfirmarAsync();
        }
    }

    private void PasarFocoALaGrilla()
    {
        ViewModel.ListaSeleccionada ??= ViewModel.Listas[0];
        Grilla.UpdateLayout();

        if (Grilla.ItemContainerGenerator.ContainerFromItem(ViewModel.ListaSeleccionada) is DataGridRow fila)
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
