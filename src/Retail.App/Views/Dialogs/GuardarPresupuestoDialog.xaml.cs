using System.Windows;
using Retail.App.ViewModels.Presupuestos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// "Guardar como presupuesto" del Punto de venta (RF-11), en dos estados: datos (título interno y vigencia) y guardado
/// (imprimir, exportar a PDF y cerrar).
/// </summary>
public partial class GuardarPresupuestoDialog : FluentWindow
{
    public GuardarPresupuestoViewModel ViewModel { get; }

    public GuardarPresupuestoDialog(GuardarPresupuestoViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        // El cajero empieza escribiendo el título (opcional) o confirma con Enter
        Loaded += (_, _) => TxtTitulo.Focus();
    }

    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
