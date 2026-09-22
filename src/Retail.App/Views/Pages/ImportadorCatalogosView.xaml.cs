using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Proveedores;

namespace Retail.App.Views.Pages;

/// <summary>
/// Code-behind de la vista del Catálogo de Proveedores e Importación Masiva (Etapa 2.2).
/// Conecta el ViewModel por inyección de dependencias, gestiona la selección interactiva de DataGrid y atajos de teclado.
/// </summary>
public partial class ImportadorCatalogosView : UserControl
{
    public ImportadorCatalogosViewModel ViewModel { get; }

    public ImportadorCatalogosView(ImportadorCatalogosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            if (ViewModel.ProveedoresDisponibles.Count == 0)
            {
                await ViewModel.CargarProveedoresDisponiblesAsync();
            }
        };

        foreach (var binding in InputBindings.OfType<System.Windows.Input.KeyBinding>())
        {
            if (binding.Key == System.Windows.Input.Key.F9)
            {
                binding.Command = ViewModel.ImportarPlanillaCommand;
            }
        }
    }

    /// <summary>
    /// Notifica reactivamente al ViewModel si existen elementos seleccionados para habilitar/deshabilitar el botón de incorporación.
    /// </summary>
    private void GrillaCatalogo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ImportadorCatalogosViewModel vm)
        {
            vm.TieneItemsSeleccionados = vm.ItemsCatalogo.Any(i => i.EstaSeleccionado) || GrillaCatalogo.SelectedItems.Count > 0;
        }
    }

    /// <summary>
    /// Reenvía los ítems seleccionados en la grilla al comando IncorporarSeleccionadosCommand del ViewModel.
    /// Prioriza los elementos tildados en las casillas de verificación (checklists) y, si no hay ninguno marcado,
    /// toma los registros seleccionados en la fila del DataGrid.
    /// </summary>
    private void BtnIncorporar_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportadorCatalogosViewModel vm)
        {
            var seleccionados = vm.ItemsCatalogo
                .Where(i => i.EstaSeleccionado)
                .ToList();

            if (seleccionados.Count == 0)
            {
                seleccionados = GrillaCatalogo.SelectedItems
                    .Cast<CatalogoProveedorDto>()
                    .ToList();
            }

            vm.IncorporarSeleccionadosCommand.Execute(seleccionados);
        }
    }

    /// <summary>
    /// Evita que el clic en una fila del DataGrid la seleccione, a menos que el clic se origine en el CheckBox o en un botón de acción.
    /// </summary>
    private void DataGridRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            var isCheckBox = FindVisualParent<CheckBox>(dep) != null;
            var isButton = FindVisualParent<Button>(dep) != null;

            if (!isCheckBox && !isButton)
            {
                e.Handled = true;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject is null)
        {
            return null;
        }

        return parentObject is T parent ? parent : FindVisualParent<T>(parentObject);
    }
}
