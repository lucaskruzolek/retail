using System.Windows;
using System.Windows.Threading;
using Retail.App.ViewModels.Pedidos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Modal de pedido (A-17): el usuario revisa el lote agrupado por proveedor, ajusta cantidades y confirma, o elige
/// incorporar primero los ítems sin incorporar. El guardado lo hace quien abre el modal con
/// <see cref="PedirArticulosViewModel.ObtenerRespuesta"/>.
/// </summary>
public partial class PedirArticulosDialog : FluentWindow
{
    public PedirArticulosViewModel ViewModel { get; }

    public PedirArticulosDialog(PedirArticulosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();
    }

    private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeConfirmar)
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void BtnIncorporarPrimero_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeIncorporarPrimero)
        {
            return;
        }

        ViewModel.SolicitarIncorporacion();
        DialogResult = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Pasa la fuente elegida al renglón cuando el combo terminó de procesar la selección (Track 4.3, K3). Al cambiar
    /// de fuente, el renglón cambia de grupo y su contenedor, con este combo adentro, se destruye: hacerlo durante el
    /// propio evento deja al combo con la selección a medio actualizar (InvalidOperationException). Por eso el binding
    /// es Explicit y se confirma en el siguiente ciclo del Dispatcher, como difiere su reformateo SelectorFechaCorta.
    /// La prioridad Normal corre antes que la entrada del usuario: un clic en "Agregar al pedido" ya ve la fuente nueva.
    /// </summary>
    private void ComboFuente_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBox combo
            || combo.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty) is not { } binding)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Normal, binding.UpdateSource);
    }
}
