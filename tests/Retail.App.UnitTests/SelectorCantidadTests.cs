using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Ventas;
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
    public void Tipear_DentroDeUnaGrilla_ActualizaLaCantidadDelItem()
    {
        // El binding de una celda forma parte del BindingGroup de la fila: si el control no confirma su valor, el cuadro
        // muestra lo tipeado y el ViewModel conserva el anterior (se vendía 1 unidad con 49 en pantalla).
        var cantidad = WpfTestHelper.Run(() =>
        {
            var item = new ItemVentaPosViewModel { Cantidad = 1, StockActual = 46 };
            var ventana = MostrarEnGrilla(item, "Mode=TwoWay");

            Descendientes<TextBox>(ventana).Single().Text = "49";

            ventana.Close();
            return item.Cantidad;
        });

        cantidad.Should().Be(49);
    }

    [Fact]
    public void BotonMas_DentroDeUnaGrilla_ActualizaLaCantidadDelItem()
    {
        var cantidad = WpfTestHelper.Run(() =>
        {
            var item = new ItemVentaPosViewModel { Cantidad = 1 };
            var ventana = MostrarEnGrilla(item, "Mode=TwoWay");

            Descendientes<Button>(ventana).Single(b => b.Name == "BotonMas").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            ventana.Close();
            return item.Cantidad;
        });

        cantidad.Should().Be(2);
    }

    [Fact]
    public void Tipear_DentroDeUnaGrillaConBindingExplicito_DejaQueQuienLoUsaConfirme()
    {
        var cantidad = WpfTestHelper.Run(() =>
        {
            var item = new ItemVentaPosViewModel { Cantidad = 1 };
            var ventana = MostrarEnGrilla(item, "Mode=TwoWay, UpdateSourceTrigger=Explicit");

            Descendientes<TextBox>(ventana).Single().Text = "49";

            ventana.Close();
            return item.Cantidad;
        });

        cantidad.Should().Be(1);
    }

    // Misma forma que la columna de cantidad de PosView.xaml: plantilla de celda, sin modo de edición de la grilla.
    private static Window MostrarEnGrilla(ItemVentaPosViewModel item, string opcionesDelBinding)
    {
        var columna = (DataGridTemplateColumn)XamlReader.Parse(
            "<DataGridTemplateColumn xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "xmlns:c='clr-namespace:Retail.App.Views.Components;assembly=Retail.App'>" +
            "<DataGridTemplateColumn.CellTemplate><DataTemplate>" +
            $"<c:SelectorCantidad Valor='{{Binding Cantidad, {opcionesDelBinding}}}' Minimo='1' />" +
            "</DataTemplate></DataGridTemplateColumn.CellTemplate></DataGridTemplateColumn>");

        var grilla = new DataGrid { AutoGenerateColumns = false, ItemsSource = new[] { item } };
        grilla.Columns.Add(columna);

        System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var ventana = new Window { Content = grilla, Width = 400, Height = 200, ShowInTaskbar = false, Left = -3000, Top = -3000 };
        ventana.Show();
        ventana.UpdateLayout();
        return ventana;
    }

    private static IEnumerable<T> Descendientes<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var hijo = VisualTreeHelper.GetChild(raiz, i);
            if (hijo is T encontrado)
            {
                yield return encontrado;
            }

            foreach (var nieto in Descendientes<T>(hijo))
            {
                yield return nieto;
            }
        }
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
