using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Retail.App.ViewModels.Articulos;

namespace Retail.App.Views.Pages;

public partial class ArticulosView : UserControl
{
    public ArticulosViewModel ViewModel { get; }

    public ArticulosView(ArticulosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        // El rol no cambia mientras la vista existe: sin permiso para pedir (A-14), la columna de selección sobra.
        ColumnaSeleccion.Visibility = ViewModel.PuedePedir ? Visibility.Visible : Visibility.Collapsed;

        // Asegurar vinculación determinística de comandos de mostrador en InputBindings
        foreach (var binding in InputBindings.OfType<System.Windows.Input.KeyBinding>())
        {
            if (binding.Key == System.Windows.Input.Key.F2)
            {
                binding.Command = ViewModel.NuevoArticuloCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F5)
            {
                binding.Command = ViewModel.CargarArticulosCommand;
            }
        }

        Loaded += async (_, _) =>
        {
            if (ViewModel.Articulos.Count == 0)
            {
                await ViewModel.CargarArticulosCommand.ExecuteAsync(null);
            }
        };
    }

    /// <summary>
    /// Doble clic en una fila abre la edición de ese artículo. Se engancha a la fila (no a la grilla) para que el
    /// encabezado, la barra de desplazamiento y el área vacía no lo disparen, y se ignora si el clic cae sobre un botón
    /// de la fila (casilla de selección, acciones, "+n"), que ya hace lo suyo.
    /// </summary>
    private void Fila_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || sender is not DataGridRow fila
            || fila.Item is not ArticuloFila articuloFila
            || EsClicSobreUnBoton(e.OriginalSource as DependencyObject, fila))
        {
            return;
        }

        if (ViewModel.EditarArticuloCommand.CanExecute(articuloFila.Articulo))
        {
            ViewModel.EditarArticuloCommand.Execute(articuloFila.Articulo);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Sube por el árbol visual desde el origen del clic hasta la fila buscando un <see cref="ButtonBase"/>. Los
    /// elementos que no son visuales (el texto de un Run) se resuelven por su padre lógico.
    /// </summary>
    private static bool EsClicSobreUnBoton(DependencyObject? origen, DataGridRow fila)
    {
        var actual = origen;
        while (actual is not null && !ReferenceEquals(actual, fila))
        {
            if (actual is ButtonBase)
            {
                return true;
            }

            actual = actual is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(actual)
                : LogicalTreeHelper.GetParent(actual);
        }

        return false;
    }
}
