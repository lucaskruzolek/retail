using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Listas;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Editor de una lista de artículos (IU-14): nombre, buscador de artículos, renglones con cantidad y total de hoy.
/// Atiende las teclas del buscador (flechas, Enter y Esc) y se cierra cuando el ViewModel guardó la lista.
/// </summary>
public partial class ListaFormDialog : FluentWindow
{
    public ListaFormViewModel ViewModel { get; }

    public ListaFormDialog(ListaFormViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        CuadroBusqueda.PreviewKeyDown += OnBuscadorPreviewKeyDown;
        ViewModel.CierreSolicitado += (_, _) => Close();

        // Una lista nueva empieza por el nombre; al editar, por el buscador (lo habitual es agregar artículos)
        Loaded += (_, _) =>
        {
            if (ViewModel.EsNueva)
            {
                TxtNombre.Focus();
            }
            else
            {
                CuadroBusqueda.Focus();
            }
        };
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Se usa PreviewKeyDown (tunneling): el TextBox marca ↑ y ↓ como manejadas para mover el cursor, así que un KeyDown
    /// nunca las recibiría. Enter con el buscador vacío sigue de largo hasta el botón Guardar (IsDefault).
    /// </summary>
    private void OnBuscadorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when !string.IsNullOrWhiteSpace(ViewModel.TextoBusqueda):
                ViewModel.AgregarResultadoSeleccionadoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down when ViewModel.MostrarResultados:
                ViewModel.MoverSeleccionBusquedaAbajoCommand.Execute(null);
                MostrarResultadoSeleccionado();
                e.Handled = true;
                break;

            case Key.Up when ViewModel.MostrarResultados:
                ViewModel.MoverSeleccionBusquedaArribaCommand.Execute(null);
                MostrarResultadoSeleccionado();
                e.Handled = true;
                break;

            case Key.Escape when ViewModel.MostrarResultados || ViewModel.TextoBusqueda.Length > 0:
                ViewModel.CerrarBusquedaCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Desplaza el desplegable hasta la fila elegida con el teclado.</summary>
    private void MostrarResultadoSeleccionado()
    {
        if (ListaResultados.SelectedItem is { } seleccionado)
        {
            ListaResultados.ScrollIntoView(seleccionado);
        }
    }

    /// <summary>El hover selecciona la fila: mouse y teclado comparten un único estado de selección.</summary>
    private void OnResultadosMouseMove(object sender, MouseEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is { IsSelected: false } fila)
        {
            fila.IsSelected = true;
        }
    }

    /// <summary>Se consume el botón presionado sobre una fila para que el ListBox no le quite el foco al buscador.</summary>
    private void OnResultadosPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is not null)
        {
            e.Handled = true;
        }
    }

    /// <summary>Un clic sobre una fila agrega el artículo a la lista (no sobre la barra de desplazamiento).</summary>
    private void OnResultadosPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is not { } fila)
        {
            return;
        }

        fila.IsSelected = true;
        ViewModel.AgregarResultadoSeleccionadoCommand.Execute(null);
        CuadroBusqueda.Focus();
        e.Handled = true;
    }

    private ListBoxItem? ResultadoBajo(object origen)
    {
        return origen is DependencyObject elemento
            ? ItemsControl.ContainerFromElement(ListaResultados, elemento) as ListBoxItem
            : null;
    }
}
