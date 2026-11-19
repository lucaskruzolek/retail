using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Presupuestos;

namespace Retail.App.Views.Pages;

/// <summary>
/// Consulta de presupuestos (IU-13). Solo conecta la grilla con el ViewModel: el doble clic y Enter abren el detalle.
/// </summary>
public partial class PresupuestosView : UserControl
{
    public PresupuestosViewModel ViewModel { get; }

    public PresupuestosView(PresupuestosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            // La vista se recrea al volver a la página: solo consulta la primera vez
            if (ViewModel.Presupuestos.Count == 0 && !ViewModel.IsBusy)
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
            && fila.Item is PresupuestoFila presupuesto
            && !EsBoton(origen))
        {
            ViewModel.VerDetalleCommand.Execute(presupuesto);
        }
    }

    private void OnGrillaPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // La grilla se queda con Enter para bajar de fila: se lo intercepta para abrir el detalle de la seleccionada
        if (e.Key == Key.Enter
            && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase
            && ViewModel.PresupuestoSeleccionado is { } seleccionado)
        {
            ViewModel.VerDetalleCommand.Execute(seleccionado);
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
