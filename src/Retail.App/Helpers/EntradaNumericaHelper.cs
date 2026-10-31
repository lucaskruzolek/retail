using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Retail.App.Helpers;

/// <summary>
/// Restringe un <c>TextBox</c> a números: <see cref="SoloDigitosProperty"/> para cantidades enteras no negativas (el
/// stock) y <see cref="SoloDecimalProperty"/> para importes y porcentajes, con un único separador decimal. Filtra lo
/// tipeado, lo pegado y el espacio, que no pasa por <c>PreviewTextInput</c>. Es una ayuda de entrada: la validación
/// sigue en el ViewModel.
/// </summary>
public static class EntradaNumericaHelper
{
    public static readonly DependencyProperty SoloDigitosProperty =
        DependencyProperty.RegisterAttached(
            "SoloDigitos",
            typeof(bool),
            typeof(EntradaNumericaHelper),
            new PropertyMetadata(false, AlCambiarModo));

    /// <summary>
    /// El separador decimal es el de la cultura con la que el binding interpreta el número (el <c>Language</c> del
    /// cuadro): aceptar otro haría que "6,5" se leyera como 65. Tipear "." o "," inserta el separador correcto, así
    /// el punto del teclado numérico funciona con cualquier cultura.
    /// </summary>
    public static readonly DependencyProperty SoloDecimalProperty =
        DependencyProperty.RegisterAttached(
            "SoloDecimal",
            typeof(bool),
            typeof(EntradaNumericaHelper),
            new PropertyMetadata(false, AlCambiarModo));

    public static void SetSoloDigitos(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(SoloDigitosProperty, value);
    }

    public static bool GetSoloDigitos(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(SoloDigitosProperty);
    }

    public static void SetSoloDecimal(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(SoloDecimalProperty, value);
    }

    public static bool GetSoloDecimal(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(SoloDecimalProperty);
    }

    /// <summary>
    /// El texto tiene al menos un carácter y todos son dígitos del 0 al 9 (no se aceptan dígitos de otros alfabetos).
    /// </summary>
    public static bool SonSoloDigitos(string? texto)
    {
        return !string.IsNullOrEmpty(texto) && texto.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// El texto es un decimal no negativo en construcción: dígitos con a lo sumo un separador, en cualquier posición
    /// ("12", "12,", ",5"), sin signo ni separador de miles.
    /// </summary>
    public static bool EsDecimalEnConstruccion(string? texto, string separador)
    {
        if (string.IsNullOrEmpty(texto) || string.IsNullOrEmpty(separador))
        {
            return false;
        }

        var partes = texto.Split(separador);
        return partes.Length <= 2 && partes.All(parte => parte.All(char.IsAsciiDigit));
    }

    private static void AlCambiarModo(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox cuadro)
        {
            return;
        }

        cuadro.PreviewTextInput -= AlEscribir;
        cuadro.PreviewKeyDown -= AlPresionarTecla;
        DataObject.RemovePastingHandler(cuadro, AlPegar);

        if (GetSoloDigitos(cuadro) || GetSoloDecimal(cuadro))
        {
            cuadro.PreviewTextInput += AlEscribir;
            cuadro.PreviewKeyDown += AlPresionarTecla;
            DataObject.AddPastingHandler(cuadro, AlPegar);
            InputMethod.SetIsInputMethodEnabled(cuadro, false);
        }
    }

    private static void AlEscribir(object sender, TextCompositionEventArgs e)
    {
        var cuadro = (TextBox)sender;

        if (!GetSoloDecimal(cuadro))
        {
            if (!SonSoloDigitos(e.Text))
            {
                e.Handled = true;
            }

            return;
        }

        var separador = SeparadorDecimal(cuadro);
        var tipeado = e.Text is "." or "," ? separador : e.Text;

        if (!EsDecimalEnConstruccion(TextoResultante(cuadro, tipeado), separador))
        {
            e.Handled = true;
            return;
        }

        if (tipeado != e.Text)
        {
            // Se tipeó el otro separador: se inserta el de la cultura en su lugar
            var inicio = cuadro.SelectionStart;
            cuadro.SelectedText = tipeado;
            cuadro.CaretIndex = inicio + tipeado.Length;
            e.Handled = true;
        }
    }

    private static void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void AlPegar(object sender, DataObjectPastingEventArgs e)
    {
        var cuadro = (TextBox)sender;
        var texto = e.DataObject.GetDataPresent(DataFormats.UnicodeText)
            ? e.DataObject.GetData(DataFormats.UnicodeText) as string
            : null;

        var valido = GetSoloDecimal(cuadro)
            ? texto != null && EsDecimalEnConstruccion(TextoResultante(cuadro, texto), SeparadorDecimal(cuadro))
            : SonSoloDigitos(texto);

        if (!valido)
        {
            e.CancelCommand();
        }
    }

    /// <summary>
    /// Cómo quedaría el texto si se insertara <paramref name="insercion"/> reemplazando la selección actual.
    /// </summary>
    private static string TextoResultante(TextBox cuadro, string insercion)
    {
        return cuadro.Text
            .Remove(cuadro.SelectionStart, cuadro.SelectionLength)
            .Insert(cuadro.SelectionStart, insercion);
    }

    private static string SeparadorDecimal(TextBox cuadro)
    {
        return cuadro.Language.GetSpecificCulture().NumberFormat.NumberDecimalSeparator;
    }
}
