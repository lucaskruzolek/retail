using System.Windows;
using Retail.App.ViewModels.Listas;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// "Guardar como → Lista" del Punto de venta (L-06, L-16): el nombre de la lista nueva o, si el ticket vino de una lista,
/// la opción de actualizarla. Se cierra cuando el ViewModel guardó; los errores se muestran adentro.
/// </summary>
public partial class GuardarListaDialog : FluentWindow
{
    public GuardarListaViewModel ViewModel { get; }

    public GuardarListaDialog(GuardarListaViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        ViewModel.CierreSolicitado += (_, _) => Close();

        // Al elegir "lista nueva" aparece el nombre: el cajero puede escribirlo sin tocar el mouse
        OpcionNueva.Checked += (_, _) => Dispatcher.BeginInvoke(() => TxtNombre.Focus());

        // Con una lista vinculada el foco queda en "Actualizar" (la opción por defecto); sin ella, en el nombre.
        Loaded += (_, _) =>
        {
            if (ViewModel.TieneListaVinculada)
            {
                OpcionActualizar.Focus();
            }
            else
            {
                TxtNombre.Focus();
            }
        };
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
