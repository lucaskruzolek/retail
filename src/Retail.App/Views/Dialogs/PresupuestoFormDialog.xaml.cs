using System.Windows;
using Retail.App.ViewModels.Presupuestos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Editor de un presupuesto (IU-13, módulo 5.4): "Nuevo presupuesto" y "Corregir". Datos (título interno, cliente,
/// vigencia, descuento), buscador de artículos con renglones y totales; después de guardar ofrece imprimir y exportar a PDF.
/// Las teclas del buscador las atiende el componente <c>EditorRenglones</c>.
/// </summary>
public partial class PresupuestoFormDialog : FluentWindow
{
    public PresupuestoFormViewModel ViewModel { get; }

    public PresupuestoFormDialog(PresupuestoFormViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        // Un presupuesto nuevo empieza por el buscador (lo habitual es cargar artículos); al corregir también, y primero se
        // resuelve el cliente del original (R-2).
        Loaded += async (_, _) =>
        {
            EditorArticulos.EnfocarBuscador();
            await ViewModel.InicializarAsync();
        };
    }

    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
