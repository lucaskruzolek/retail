using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Ventas;

namespace Retail.App.Views.Pages;

/// <summary>
/// Terminal de Punto de Venta (POS) y Mostrador (RF-09, RF-10, RNF-01).
/// </summary>
public partial class PosView : UserControl
{
    public PosViewModel ViewModel { get; }

    public PosView(PosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        InitializeComponent();

        Loaded += (_, _) =>
        {
            TxtBuscador.Focus();
        };
    }

    /// <summary>
    /// Se usa PreviewKeyDown (tunneling) y no KeyDown (bubbling): el TextBox asocia ↑ y ↓ a mover el cursor y marca
    /// esas teclas como manejadas, así que un KeyDown nunca las recibía y la selección del popup no se movía.
    /// </summary>
    private void OnTxtBuscadorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.ProcesarEnterCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && ViewModel.MostrarPopupBusqueda)
        {
            ViewModel.MoverSeleccionPopupAbajoCommand.Execute(null);
            MostrarResultadoSeleccionado();
            e.Handled = true;
        }
        else if (e.Key == Key.Up && ViewModel.MostrarPopupBusqueda)
        {
            ViewModel.MoverSeleccionPopupArribaCommand.Execute(null);
            MostrarResultadoSeleccionado();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && ViewModel.MostrarPopupBusqueda)
        {
            ViewModel.CerrarPopupBusquedaCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Con la caja cerrada el buscador está bloqueado. Al abrirla (desde el aviso), recupera el foco: el cajero escanea
    /// sin tener que hacer clic primero en el campo.
    /// </summary>
    private void OnTxtBuscadorIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            TxtBuscador.Focus();
        }
    }

    /// <summary>
    /// Desplaza el desplegable hasta la fila seleccionada con el teclado. Solo se llama desde las flechas: si se
    /// llamara también al seleccionar con el hover, la lista se movería debajo del mouse y seleccionaría otra fila.
    /// </summary>
    private void MostrarResultadoSeleccionado()
    {
        if (ListaResultados.SelectedItem is { } seleccionado)
        {
            ListaResultados.ScrollIntoView(seleccionado);
        }
    }

    /// <summary>
    /// El hover selecciona la fila: mouse y teclado comparten un único estado (IndiceResultadoSeleccionado), así que
    /// lo que está pintado de carmín es siempre lo que agregan Enter o un clic.
    /// </summary>
    private void OnResultadosMouseMove(object sender, MouseEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is { IsSelected: false } fila)
        {
            fila.IsSelected = true;
        }
    }

    /// <summary>
    /// Se consume el botón presionado sobre una fila para que el ListBox no le quite el foco al buscador: así ↑, ↓ y
    /// Enter siguen funcionando con teclado después de usar el mouse. El clic se completa al soltar el botón.
    /// </summary>
    private void OnResultadosPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is not null)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Un clic sobre una fila la agrega al ticket (no sobre la barra de desplazamiento).
    /// </summary>
    private void OnResultadosPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is not { } fila)
        {
            return;
        }

        fila.IsSelected = true;
        ViewModel.ProcesarEnterCommand.Execute(null);
        TxtBuscador.Focus();
        e.Handled = true;
    }

    /// <summary>
    /// Enter confirma la cantidad (ya está aplicada: SelectorCantidad la actualiza mientras se escribe) y Esc la descarta
    /// (el selector ya la deshizo); en los dos casos el foco vuelve al buscador para seguir escaneando sin mouse. Se marca
    /// como atendida para que la grilla no use Enter para bajar de fila.
    /// </summary>
    private void OnCantidadKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape))
        {
            return;
        }

        TxtBuscador.Focus();
        e.Handled = true;
    }

    private ListBoxItem? ResultadoBajo(object origen)
    {
        return origen is DependencyObject elemento
            ? ItemsControl.ContainerFromElement(ListaResultados, elemento) as ListBoxItem
            : null;
    }

    private void OnF1Click(object sender, RoutedEventArgs e)
    {
        TxtBuscador.Focus();
        TxtBuscador.SelectAll();
    }
}
