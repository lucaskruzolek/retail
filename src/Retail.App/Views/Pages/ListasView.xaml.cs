using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Listas;

namespace Retail.App.Views.Pages;

/// <summary>
/// Pestaña Listas (IU-14). Solo conecta la grilla con el ViewModel: el doble clic y Enter abren el editor.
/// </summary>
public partial class ListasView : UserControl
{
    public ListasViewModel ViewModel { get; }

    public ListasView(ListasViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            // La vista se recrea al volver a la página: solo consulta la primera vez
            if (ViewModel.Listas.Count == 0 && !ViewModel.IsBusy)
            {
                await ViewModel.InicializarAsync();
            }
        };
    }

    private void OnGrillaMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Solo una fila: el doble clic en el encabezado (ordenar) o en un botón de acción no abre nada
        if (e.OriginalSource is DependencyObject origen
            && ItemsControl.ContainerFromElement(Grilla, origen) is DataGridRow fila
            && fila.Item is ListaFila lista
            && !EsBoton(origen))
        {
            ViewModel.EditarCommand.Execute(lista);
        }
    }

    private void OnGrillaPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // La grilla se queda con Enter para bajar de fila: se lo intercepta para editar la seleccionada
        if (e.Key == Key.Enter
            && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase
            && ViewModel.ListaSeleccionada is { } seleccionada)
        {
            ViewModel.EditarCommand.Execute(seleccionada);
            e.Handled = true;
        }
    }

    private static bool EsBoton(DependencyObject origen)
    {
        for (var actual = origen; actual is not null; actual = Padre(actual))
        {
            if (actual is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            if (actual is DataGridRow)
            {
                return false;
            }
        }

        return false;
    }

    // Un Run (texto dentro de un TextBlock) no es un Visual: se sube por el árbol lógico
    private static DependencyObject? Padre(DependencyObject elemento)
    {
        return elemento is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(elemento)
            : LogicalTreeHelper.GetParent(elemento);
    }
}
