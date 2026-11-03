using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Compras;

namespace Retail.App.Views.Pages;

/// <summary>
/// Pantalla de ingreso de compras a distribuidores (Módulo 4.2). El único atajo es F12 (registrar), declarado en el XAML;
/// el resto de la interacción del buscador es teclado y mouse sobre su desplegable, igual que en el POS.
/// </summary>
public partial class ComprasView : UserControl
{
    public ComprasViewModel ViewModel { get; }

    public ComprasView(ComprasViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        // Las columnas de un DataGrid no heredan el DataContext, así que no pueden enlazar su Visibility: la columna
        // "Pedido" (cantidad pedida) solo se muestra mientras la compra tiene un pedido vinculado.
        ActualizarColumnaPedido();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ComprasViewModel.TienePedidoOrigen))
            {
                ActualizarColumnaPedido();
            }
        };

        // El buscador es el del ListToolbar: sus teclas (flechas, Enter y Esc) se atienden desde acá
        BarraBusqueda.Buscador.PreviewKeyDown += OnTxtBuscadorPreviewKeyDown;

        Loaded += async (_, _) =>
        {
            if (ViewModel.Proveedores.Count == 0)
            {
                await ViewModel.CargarProveedoresCommand.ExecuteAsync(null);
            }
        };
    }

    private void ActualizarColumnaPedido()
    {
        ColumnaPedido.Visibility = ViewModel.TienePedidoOrigen ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Se usa PreviewKeyDown (tunneling): el TextBox marca ↑ y ↓ como manejadas para mover el cursor, así que un KeyDown
    /// nunca las recibiría y la selección del desplegable no se movería.
    /// </summary>
    private void OnTxtBuscadorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
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

    /// <summary>Desplaza el desplegable hasta la fila elegida con el teclado (no con el hover, para no mover la lista bajo el mouse).</summary>
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

    /// <summary>
    /// Se consume el botón presionado sobre una fila para que el ListBox no le quite el foco al buscador: así las
    /// flechas y Enter siguen funcionando después de usar el mouse. El clic se completa al soltar el botón.
    /// </summary>
    private void OnResultadosPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is not null)
        {
            e.Handled = true;
        }
    }

    /// <summary>Un clic sobre una fila agrega el artículo a la compra (no sobre la barra de desplazamiento).</summary>
    private void OnResultadosPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is not { } fila)
        {
            return;
        }

        fila.IsSelected = true;
        ViewModel.AgregarResultadoSeleccionadoCommand.Execute(null);
        BarraBusqueda.Buscador.Focus();
        e.Handled = true;
    }

    private ListBoxItem? ResultadoBajo(object origen)
    {
        return origen is DependencyObject elemento
            ? ItemsControl.ContainerFromElement(ListaResultados, elemento) as ListBoxItem
            : null;
    }
}
