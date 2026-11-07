using System.Windows.Controls;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

public class SelectorCantidadTests
{
    private static (TextBox Cuadro, Button Menos, Button Mas) Partes(SelectorCantidad selector)
    {
        return ((TextBox)selector.FindName("Cuadro"), (Button)selector.FindName("BotonMenos"), (Button)selector.FindName("BotonMas"));
    }

    [Fact]
    public void BotonesMasYMenos_DesdeElMinimo_SumanRestanYDeshabilitanMenosEnElMinimo()
    {
        var resultado = WpfTestHelper.Run(() =>
        {
            var selector = new SelectorCantidad { Minimo = 1, Valor = 1 };
            var (cuadro, menos, mas) = Partes(selector);
            var menosEnElMinimo = menos.IsEnabled;

            mas.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            mas.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            var trasSumar = (selector.Valor, cuadro.Text, menos.IsEnabled);

            menos.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));

            return (menosEnElMinimo, trasSumar, selector.Valor, cuadro.Text);
        });

        resultado.menosEnElMinimo.Should().BeFalse("no se puede pedir menos de una unidad");
        resultado.trasSumar.Should().Be((3, "3", true));
        resultado.Valor.Should().Be(2);
        resultado.Text.Should().Be("2");
    }

    [Fact]
    public void Escribir_UnNumero_ActualizaElValorEnElMomento()
    {
        var valor = WpfTestHelper.Run(() =>
        {
            var selector = new SelectorCantidad { Valor = 1 };
            Partes(selector).Cuadro.Text = "12";
            return selector.Valor;
        });

        valor.Should().Be(12);
    }

    [Fact]
    public void Escribir_FueraDeRango_RecortaYElCuadroMuestraElValorReal()
    {
        var (valor, texto) = WpfTestHelper.Run(() =>
        {
            var selector = new SelectorCantidad { Minimo = 1, Maximo = 50, Valor = 1 };
            var cuadro = Partes(selector).Cuadro;
            cuadro.Text = "80";
            return (selector.Valor, cuadro.Text);
        });

        valor.Should().Be(50);
        texto.Should().Be("50", "lo que se ve es siempre lo que se confirma");
    }

    [Fact]
    public void Borrar_ElCuadro_ValeElMinimo()
    {
        var valor = WpfTestHelper.Run(() =>
        {
            var selector = new SelectorCantidad { Minimo = 1, Valor = 7 };
            Partes(selector).Cuadro.Text = string.Empty;
            return selector.Valor;
        });

        valor.Should().Be(1);
    }

    [Fact]
    public void Valor_AlCambiarDesdeElViewModel_ElCuadroLoMuestra()
    {
        var texto = WpfTestHelper.Run(() =>
        {
            var selector = new SelectorCantidad { Valor = 1 };
            selector.Valor = 25;
            return Partes(selector).Cuadro.Text;
        });

        texto.Should().Be("25");
    }

    [Fact]
    public void Escape_TrasEscribir_DeshaceHastaElValorConElQueEntroElFoco()
    {
        var (valor, texto) = WpfTestHelper.Run(() =>
        {
            System.Windows.Application.Current.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            var selector = new SelectorCantidad { Valor = 4 };
            var ventana = new System.Windows.Window { Content = selector, Width = 200, Height = 80, ShowInTaskbar = false, Left = -3000, Top = -3000 };
            ventana.Show();
            var cuadro = Partes(selector).Cuadro;
            cuadro.Focus();
            cuadro.Text = "40";

            var fuente = System.Windows.PresentationSource.FromVisual(cuadro)!;
            cuadro.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, fuente, 0, System.Windows.Input.Key.Escape)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
            });

            var resultado = (selector.Valor, cuadro.Text);
            ventana.Close();
            return resultado;
        });

        valor.Should().Be(4);
        texto.Should().Be("4");
    }

    [Fact]
    public void Valor_MenorAlMinimo_SeAcotaAlMinimo()
    {
        var valor = WpfTestHelper.Run(() =>
        {
            var selector = new SelectorCantidad { Minimo = 1 };
            selector.Valor = 0;
            return selector.Valor;
        });

        valor.Should().Be(1);
    }
}
