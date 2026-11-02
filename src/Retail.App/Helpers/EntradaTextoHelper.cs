using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Retail.App.Helpers;

/// <summary>
/// Clases de contenido que un cuadro de texto admite mientras se escribe.
/// </summary>
public enum TipoEntradaTexto
{
    /// <summary>Sin restricción (valor por defecto: el helper no interviene).</summary>
    Ninguno,

    /// <summary>Solo dígitos del 0 al 9.</summary>
    Entero,

    /// <summary>Dígitos con a lo sumo un separador decimal; sin signo (importes no negativos).</summary>
    NumeroDecimal,

    /// <summary>Dígitos, espacio, guion, paréntesis y "+".</summary>
    Telefono,

    /// <summary>
    /// Letras y dígitos ASCII más guion, punto y espacio. Es lo que <c>ReglasDocumento.Normalizar</c> acepta para
    /// DNI, CUIT, CUIL (dígitos con "-" o ".") y pasaporte (alfanumérico).
    /// </summary>
    Documento
}

/// <summary>
/// Propiedad adjunta <see cref="TipoProperty"/> que impide escribir (o pegar) en un <c>TextBox</c> caracteres que su
/// campo no admite. Evalúa el texto RESULTANTE de la edición (considerando selección y cursor), no solo el carácter
/// tipeado. Para <see cref="TipoEntradaTexto.Entero"/> y <see cref="TipoEntradaTexto.NumeroDecimal"/> reutiliza las
/// reglas de <see cref="EntradaNumericaHelper"/>. Es una ayuda de entrada: la validación sigue en la capa de
/// aplicación.
/// </summary>
public static class EntradaTextoHelper
{
    private const string SeparadorPorDefecto = ",";

    public static readonly DependencyProperty TipoProperty =
        DependencyProperty.RegisterAttached(
            "Tipo",
            typeof(TipoEntradaTexto),
            typeof(EntradaTextoHelper),
            new PropertyMetadata(TipoEntradaTexto.Ninguno, AlCambiarTipo));

    public static void SetTipo(DependencyObject element, TipoEntradaTexto value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(TipoProperty, value);
    }

    public static TipoEntradaTexto GetTipo(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (TipoEntradaTexto)element.GetValue(TipoProperty);
    }

    /// <summary>
    /// El texto (ya resultante de la edición) cumple las reglas del tipo. El vacío es válido: es lo que queda al
    /// borrar. <paramref name="separadorDecimal"/> solo importa para <see cref="TipoEntradaTexto.NumeroDecimal"/>.
    /// </summary>
    public static bool EsTextoValido(TipoEntradaTexto tipo, string? texto, string separadorDecimal = SeparadorPorDefecto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return true;
        }

        return tipo switch
        {
            TipoEntradaTexto.Entero => EntradaNumericaHelper.SonSoloDigitos(texto),
            TipoEntradaTexto.NumeroDecimal => EntradaNumericaHelper.EsDecimalEnConstruccion(texto, separadorDecimal),
            TipoEntradaTexto.Telefono => texto.All(c => char.IsAsciiDigit(c) || c is ' ' or '-' or '(' or ')' or '+'),
            TipoEntradaTexto.Documento => texto.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '.'),
            _ => true
        };
    }

    private static void AlCambiarTipo(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox cuadro)
        {
            return;
        }

        cuadro.PreviewTextInput -= AlEscribir;
        cuadro.PreviewKeyDown -= AlPresionarTecla;
        DataObject.RemovePastingHandler(cuadro, AlPegar);

        if (GetTipo(cuadro) != TipoEntradaTexto.Ninguno)
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
        var tipo = GetTipo(cuadro);
        var separador = SeparadorDecimal(cuadro);

        // En un decimal, tanto "." como "," insertan el separador de la cultura (el punto del teclado numérico)
        var tipeado = tipo == TipoEntradaTexto.NumeroDecimal && e.Text is "." or ","
            ? separador
            : e.Text;

        if (!EsTextoValido(tipo, TextoResultante(cuadro, tipeado), separador))
        {
            e.Handled = true;
            return;
        }

        if (tipeado != e.Text)
        {
            var inicio = cuadro.SelectionStart;
            cuadro.SelectedText = tipeado;
            cuadro.CaretIndex = inicio + tipeado.Length;
            e.Handled = true;
        }
    }

    // La barra espaciadora no genera PreviewTextInput, así que se bloquea aparte en los tipos sin espacios
    private static void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        var tipo = GetTipo((TextBox)sender);
        var admiteEspacio = tipo is TipoEntradaTexto.Telefono or TipoEntradaTexto.Documento;

        if (e.Key == Key.Space && !admiteEspacio)
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

        if (texto == null || !EsTextoValido(GetTipo(cuadro), TextoResultante(cuadro, texto), SeparadorDecimal(cuadro)))
        {
            e.CancelCommand();
        }
    }

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
