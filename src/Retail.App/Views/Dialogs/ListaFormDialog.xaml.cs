using System.Windows;
using Retail.App.ViewModels.Listas;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Editor de una lista de artículos (IU-14): nombre, buscador de artículos, renglones con cantidad y total de hoy.
/// Las teclas del buscador las atiende el componente <c>EditorRenglones</c>; el diálogo se cierra cuando el ViewModel guardó la lista.
/// </summary>
public partial class ListaFormDialog : FluentWindow
{
    public ListaFormViewModel ViewModel { get; }

    public ListaFormDialog(ListaFormViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        ViewModel.CierreSolicitado += (_, _) => Close();

        // Una lista nueva empieza por el nombre; al editar, por el buscador (lo habitual es agregar artículos)
        Loaded += (_, _) =>
        {
            if (ViewModel.EsNueva)
            {
                TxtNombre.Focus();
            }
            else
            {
                EditorArticulos.EnfocarBuscador();
            }
        };
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
