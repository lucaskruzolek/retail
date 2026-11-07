using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Ventas;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Modal de cobro del mostrador (RF-09, RF-20). El camino rápido es solo teclado: F12 en el POS abre el modal con el
/// foco en el medio de pago; ← → lo cambian; Enter (o Tab) pasa al monto; Enter (o F12) confirma el pago. Si el monto
/// no alcanza, el pago queda registrado y el foco vuelve al medio para cobrar el resto.
/// </summary>
public partial class CobroModalDialog : FluentWindow
{
    public CobroModalViewModel ViewModel { get; }

    public CobroModalDialog(CobroModalViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        Loaded += (_, _) => EnfocarMedioSeleccionado();
    }

    /// <summary>
    /// En WPF las flechas sobre un grupo de RadioButton solo mueven el foco, no marcan la opción. Acá las flechas le
    /// piden el cambio al ViewModel (que saltea los medios deshabilitados) y el foco sigue a la opción marcada.
    /// </summary>
    private void OnSelectorMediosPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                ViewModel.SeleccionarMedioAnterior();
                EnfocarMedioSeleccionado();
                e.Handled = true;
                break;
            case Key.Right:
                ViewModel.SeleccionarMedioSiguiente();
                EnfocarMedioSeleccionado();
                e.Handled = true;
                break;
            case Key.Enter:
                // Una tecla mantenida que se repite no debe pasar de largo hasta confirmar el pago
                if (!e.IsRepeat)
                {
                    TxtMonto.Focus();
                }

                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Al entrar al monto (con Enter, Tab o un clic) se selecciona el número: lo que se tipea lo reemplaza.
    /// </summary>
    private void OnTxtMontoGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        TxtMonto.SelectAll();
    }

    private void OnTxtMontoPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (!e.IsRepeat)
            {
                ConfirmarPagoActual();
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// F12 confirma desde cualquier lugar del modal, igual que en el POS. Se ignora la repetición: el F12 que abrió el
    /// modal, si queda presionado, no debe cobrar por sí solo.
    /// </summary>
    private void OnVentanaPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            if (!e.IsRepeat)
            {
                ConfirmarPagoActual();
            }

            e.Handled = true;
        }
    }

    private void OnConfirmarClick(object sender, RoutedEventArgs e)
    {
        ConfirmarPagoActual();
    }

    private void ConfirmarPagoActual()
    {
        // Con la tecla, el último dígito ya está en el ViewModel (PropertyChanged); si el texto no es un número, el
        // binding no lo pudo guardar y el ViewModel conserva el monto anterior: no se debe cobrar ese.
        if (Validation.GetHasError(TxtMonto))
        {
            ViewModel.MensajeError = "Ingrese un monto válido.";
            TxtMonto.Focus();
            TxtMonto.SelectAll();
            return;
        }

        ViewModel.ConfirmarPagoActual();

        if (ViewModel.OperacionConfirmada)
        {
            DialogResult = true;
            Close();
            return;
        }

        if (ViewModel.TieneError)
        {
            TxtMonto.Focus();
            TxtMonto.SelectAll();
            return;
        }

        // Pago parcial registrado: se vuelve al medio, ya propuesto otro, con lo que falta cargado
        EnfocarMedioSeleccionado();
    }

    private void EnfocarMedioSeleccionado()
    {
        foreach (RadioButton opcion in OpcionesMedioPago.Children.OfType<RadioButton>())
        {
            if (opcion.IsChecked == true)
            {
                opcion.Focus();
                return;
            }
        }
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
