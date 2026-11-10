using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Retail.App.Views.Components;

/// <summary>
/// Cantidad entera con pasos: [−] [ n ] [+]. El valor nunca sale de [<see cref="Minimo"/>, <see cref="Maximo"/>] y lo que
/// muestra el cuadro es siempre lo que tiene el ViewModel: el cuadro solo admite dígitos, un número fuera de rango se
/// recorta y un cuadro vacío vale el mínimo (al salir se vuelve a escribir). Con el foco en el número, ↑ y ↓ suman y restan,
/// y Esc deshace lo escrito desde que entró el foco. Enter y Esc no se marcan como atendidos: siguen hasta la página o el
/// diálogo (el botón por defecto de un modal, volver al buscador en el POS).
/// </summary>
public partial class SelectorCantidad : UserControl
{
    public static readonly DependencyProperty ValorProperty =
        DependencyProperty.Register(
            nameof(Valor),
            typeof(int),
            typeof(SelectorCantidad),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AlCambiarValor, (d, valor) => Acotar(d, (int)valor)));

    public static readonly DependencyProperty MinimoProperty =
        DependencyProperty.Register(nameof(Minimo), typeof(int), typeof(SelectorCantidad), new PropertyMetadata(1, AlCambiarLimite));

    public static readonly DependencyProperty MaximoProperty =
        DependencyProperty.Register(nameof(Maximo), typeof(int), typeof(SelectorCantidad), new PropertyMetadata(99999, AlCambiarLimite));

    public static readonly DependencyProperty NombreAutomatizacionProperty =
        DependencyProperty.Register(nameof(NombreAutomatizacion), typeof(string), typeof(SelectorCantidad), new PropertyMetadata("Cantidad"));

    // Verdadero mientras el control escribe el cuadro: ese cambio de texto no es una edición del usuario.
    private bool _escribiendoCuadro;

    // El valor al entrar el foco al número: lo que Esc restituye.
    private int _valorAlEntrar;

    public SelectorCantidad()
    {
        InitializeComponent();

        Cuadro.TextChanged += AlCambiarTexto;
        Cuadro.PreviewKeyDown += AlPresionarTecla;
        Cuadro.GotKeyboardFocus += (_, _) =>
        {
            _valorAlEntrar = Valor;
            Cuadro.SelectAll();
        };
        Cuadro.PreviewMouseLeftButtonDown += AlPresionarConElMouse;
        Cuadro.LostKeyboardFocus += (_, _) => EscribirCuadro();
        Cuadro.IsKeyboardFocusedChanged += (_, _) => ActualizarBordeDeFoco();

        EscribirCuadro();
        ActualizarBotones();
    }

    public int Valor
    {
        get => (int)GetValue(ValorProperty);
        set => SetValue(ValorProperty, value);
    }

    public int Minimo
    {
        get => (int)GetValue(MinimoProperty);
        set => SetValue(MinimoProperty, value);
    }

    public int Maximo
    {
        get => (int)GetValue(MaximoProperty);
        set => SetValue(MaximoProperty, value);
    }

    /// <summary>Nombre que anuncian los lectores de pantalla para el número.</summary>
    public string NombreAutomatizacion
    {
        get => (string)GetValue(NombreAutomatizacionProperty);
        set => SetValue(NombreAutomatizacionProperty, value);
    }

    private static int Acotar(DependencyObject d, int valorBase)
    {
        var control = (SelectorCantidad)d;
        return Math.Clamp(valorBase, control.Minimo, Math.Max(control.Minimo, control.Maximo));
    }

    private static void AlCambiarValor(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SelectorCantidad)d;

        // Mientras el usuario escribe, el cuadro ya muestra lo que vale: no se le reescribe (moverle el cursor o completarle
        // un cuadro que acaba de borrar para tipear otro número). Al salir del cuadro se vuelve a escribir el valor.
        var leido = control.LeerCuadro();
        var editando = control.Cuadro.IsKeyboardFocused && (leido is null || leido == control.Valor);
        if (!editando)
        {
            control.EscribirCuadro();
        }

        control.ActualizarBotones();
    }

    private static void AlCambiarLimite(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SelectorCantidad)d;
        control.CoerceValue(ValorProperty);
        control.ActualizarBotones();
    }

    private void BotonMenos_Click(object sender, RoutedEventArgs e)
    {
        Sumar(-1);
    }

    private void BotonMas_Click(object sender, RoutedEventArgs e)
    {
        Sumar(1);
    }

    /// <summary>Un paso de − o + (botón o flecha): el cuadro siempre muestra el resultado, aunque estuviera vacío.</summary>
    private void Sumar(int paso)
    {
        Valor += paso;
        EscribirCuadro();
        ConfirmarValor();
    }

    /// <summary>
    /// Lleva al ViewModel el valor que eligió el usuario. Dentro de un <c>DataGrid</c>, el binding de una celda forma parte
    /// del <c>BindingGroup</c> de la fila y, sin un <c>UpdateSourceTrigger</c> explícito, retiene el valor hasta que la fila
    /// confirma su edición: el cuadro mostraba 49 y la venta salía con 1. Confirmarlo acá protege a todas las grillas que
    /// usan el control (POS, Compras, Pedidos). Quien declare <c>UpdateSourceTrigger=Explicit</c> decide cuándo confirmar.
    /// </summary>
    private void ConfirmarValor()
    {
        var expresion = GetBindingExpression(ValorProperty);
        if (expresion is not null && expresion.ParentBinding.UpdateSourceTrigger != UpdateSourceTrigger.Explicit)
        {
            expresion.UpdateSource();
        }
    }

    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                Sumar(1);
                Cuadro.SelectAll();
                e.Handled = true;
                break;

            case Key.Down:
                Sumar(-1);
                Cuadro.SelectAll();
                e.Handled = true;
                break;

            case Key.Escape:
                // Deshace lo escrito; no se marca atendida, para que la página o el diálogo también reaccionen
                Valor = _valorAlEntrar;
                EscribirCuadro();
                ConfirmarValor();
                break;
        }
    }

    /// <summary>
    /// El primer clic en el número le da el foco y selecciona todo (se escribe encima). Sin esto, dentro de una grilla el
    /// clic lo toma la fila y el número queda sin foco o con el cursor en medio.
    /// </summary>
    private void AlPresionarConElMouse(object sender, MouseButtonEventArgs e)
    {
        if (!Cuadro.IsKeyboardFocusWithin)
        {
            Cuadro.Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Cada edición llega al valor en el momento: un número fuera de rango se recorta (y el cuadro lo refleja) y un cuadro
    /// vacío vale el mínimo. Así nunca se confirma un valor distinto del que se ve.
    /// </summary>
    private void AlCambiarTexto(object sender, TextChangedEventArgs e)
    {
        if (_escribiendoCuadro)
        {
            return;
        }

        var leido = LeerCuadro();
        if (leido is null)
        {
            Valor = Minimo;
            ConfirmarValor();
            return;
        }

        Valor = leido.Value;
        if (Valor != leido.Value)
        {
            // Se recortó al rango: el cuadro muestra el valor real
            EscribirCuadro();
            Cuadro.CaretIndex = Cuadro.Text.Length;
        }

        ConfirmarValor();
    }

    private int? LeerCuadro()
    {
        return int.TryParse(Cuadro.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ? numero : null;
    }

    private void EscribirCuadro()
    {
        var texto = Valor.ToString(CultureInfo.InvariantCulture);
        if (Cuadro.Text == texto)
        {
            return;
        }

        _escribiendoCuadro = true;
        try
        {
            Cuadro.Text = texto;
        }
        finally
        {
            _escribiendoCuadro = false;
        }
    }

    private void ActualizarBotones()
    {
        BotonMenos.IsEnabled = Valor > Minimo;
        BotonMas.IsEnabled = Valor < Maximo;
    }

    private void ActualizarBordeDeFoco()
    {
        if (Cuadro.IsKeyboardFocused)
        {
            Marco.SetResourceReference(Border.BorderBrushProperty, "PrimaryBrush");
        }
        else
        {
            Marco.SetResourceReference(Border.BorderBrushProperty, "SurfaceBorderBrush");
        }
    }
}
