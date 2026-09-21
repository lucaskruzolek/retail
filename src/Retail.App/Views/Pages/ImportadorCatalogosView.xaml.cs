using System.Windows;
using System.Windows.Controls;

namespace Retail.App.Views.Pages;

/// <summary>
/// Code-behind de la vista del Importador de Catálogos de Proveedores (Etapa 2.2 - Fase 2).
/// El botón "Incorporar Seleccionados" requiere acceso a DataGrid.SelectedItems (no bindeable directamente en MVVM).
/// Este thin code-behind extrae la selección y la pasa al comando del ViewModel.
/// </summary>
public partial class ImportadorCatalogosView : UserControl
{
    public ImportadorCatalogosView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Reenvía los ítems seleccionados en la grilla al comando IncorporarSeleccionadosCommand del ViewModel.
    /// MVVM puro no permite bindear DataGrid.SelectedItems; el thin code-behind es el patrón correcto aquí.
    /// </summary>
    private void BtnIncorporar_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Proveedores.ImportadorCatalogosViewModel vm)
        {
            var seleccionados = GrillaCatalogo.SelectedItems
                .Cast<Application.DTOs.Proveedores.CatalogoProveedorDto>()
                .ToList();

            vm.IncorporarSeleccionadosCommand.Execute(seleccionados);
        }
    }
}
