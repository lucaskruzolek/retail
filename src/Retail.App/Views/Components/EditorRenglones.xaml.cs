using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Comun;

namespace Retail.App.Views.Components;

/// <summary>
/// Editor de renglones (módulo 5.4, PE-09): buscador de artículos con desplegable, grilla de renglones y aviso de
/// artículos dados de baja. Su DataContext es un <see cref="EditorRenglonesViewModel"/>, que enlaza el anfitrión
/// (<c>DataContext="{Binding Editor}"</c>). Atiende las teclas del buscador (flechas, Enter y Esc) y el mouse sobre el
/// desplegable; el resto es XAML.
/// </summary>
public partial class EditorRenglones : UserControl
{
    public static readonly DependencyProperty EncabezadoPrecioProperty =
        DependencyProperty.Register(
            nameof(EncabezadoPrecio),
            typeof(string),
            typeof(EditorRenglones),
            new PropertyMetadata("Precio de hoy", AlCambiarEncabezadoPrecio));

    public static readonly DependencyProperty TextoEstadoVacioProperty =
        DependencyProperty.Register(
            nameof(TextoEstadoVacio),
            typeof(string),
            typeof(EditorRenglones),
            new PropertyMetadata("Busque un artículo para agregarlo a la lista.", AlCambiarTextoEstadoVacio));

    public EditorRenglones()
    {
        InitializeComponent();

        CuadroBusqueda.PreviewKeyDown += OnBuscadorPreviewKeyDown;
    }

    /// <summary>Encabezado de la columna del precio unitario: "Precio de hoy" en las listas, "Precio" en los presupuestos.</summary>
    public string EncabezadoPrecio
    {
        get => (string)GetValue(EncabezadoPrecioProperty);
        set => SetValue(EncabezadoPrecioProperty, value);
    }

    /// <summary>Texto del estado vacío de la grilla.</summary>
    public string TextoEstadoVacio
    {
        get => (string)GetValue(TextoEstadoVacioProperty);
        set => SetValue(TextoEstadoVacioProperty, value);
    }

    private EditorRenglonesViewModel? ViewModel => DataContext as EditorRenglonesViewModel;

    /// <summary>Lleva el foco al cuadro de búsqueda (el anfitrión lo usa al abrir el diálogo).</summary>
    public void EnfocarBuscador()
    {
        CuadroBusqueda.Focus();
    }

    // Una columna de la grilla no está en el árbol visual: no puede enlazar su encabezado, se asigna desde acá.
    private static void AlCambiarEncabezadoPrecio(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EditorRenglones)d).ColumnaPrecio.Header = e.NewValue;
    }

    private static void AlCambiarTextoEstadoVacio(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EditorRenglones)d).TextoVacio.Text = (string)e.NewValue;
    }

    /// <summary>
    /// Se usa PreviewKeyDown (tunneling): el TextBox marca ↑ y ↓ como manejadas para mover el cursor, así que un KeyDown
    /// nunca las recibiría. Enter con el buscador vacío sigue de largo hasta el botón por defecto del diálogo (IsDefault).
    /// </summary>
    private void OnBuscadorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter when !string.IsNullOrWhiteSpace(viewModel.TextoBusqueda):
                viewModel.AgregarResultadoSeleccionadoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down when viewModel.MostrarResultados:
                viewModel.MoverSeleccionBusquedaAbajoCommand.Execute(null);
                MostrarResultadoSeleccionado();
                e.Handled = true;
                break;

            case Key.Up when viewModel.MostrarResultados:
                viewModel.MoverSeleccionBusquedaArribaCommand.Execute(null);
                MostrarResultadoSeleccionado();
                e.Handled = true;
                break;

            case Key.Escape when viewModel.MostrarResultados || viewModel.TextoBusqueda.Length > 0:
                viewModel.CerrarBusquedaCommand.Execute(null);
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

    /// <summary>Un clic sobre una fila agrega el artículo (no sobre la barra de desplazamiento).</summary>
    private void OnResultadosPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ResultadoBajo(e.OriginalSource) is not { } fila || ViewModel is not { } viewModel)
        {
            return;
        }

        fila.IsSelected = true;
        viewModel.AgregarResultadoSeleccionadoCommand.Execute(null);
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
