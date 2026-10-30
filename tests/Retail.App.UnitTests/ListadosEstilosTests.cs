using System.Windows;
using System.Windows.Controls;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Estilos compartidos de las páginas de listado (<c>Styles/Listados.xaml</c>) y formato es-AR de las celdas de importe.
/// </summary>
public class ListadosEstilosTests
{
    private sealed record FilaPrueba(decimal Importe, decimal Porcentaje);

    private static string TextoDeCelda(string estilo, string formato, FilaPrueba fila)
    {
        return WpfTestHelper.Run(() =>
        {
            var celda = new TextBlock
            {
                Style = (Style)System.Windows.Application.Current.FindResource(estilo),
                DataContext = fila
            };

            var propiedad = formato.Contains('%') ? nameof(FilaPrueba.Porcentaje) : nameof(FilaPrueba.Importe);
            celda.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(propiedad) { StringFormat = formato });
            celda.Measure(new Size(300, 100));
            celda.Arrange(new Rect(0, 0, 300, 100));
            celda.UpdateLayout();
            return celda.Text;
        });
    }

    [Fact]
    public void TextBlockCurrencyCell_ImporteConFormatoMoneda_SeMuestraEnEsAr()
    {
        var texto = TextoDeCelda("TextBlockCurrencyCell", "{0:C2}", new FilaPrueba(4000m, 40m));

        texto.Should().StartWith("$");
        texto.Should().Contain("4.000,00");
    }

    [Fact]
    public void TextBlockPriceCell_ImporteConFormatoMoneda_SeMuestraEnEsArYEnNegrita()
    {
        var texto = TextoDeCelda("TextBlockPriceCell", "{0:C2}", new FilaPrueba(7932.4m, 40m));

        texto.Should().Contain("7.932,40");
        WpfTestHelper.Run(() => ((Style)System.Windows.Application.Current.FindResource("TextBlockPriceCell")).Setters
            .OfType<Setter>().Any(s => s.Property == TextBlock.FontWeightProperty && Equals(s.Value, FontWeights.Bold)))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(40, "40%")]
    [InlineData(33.33, "33,33%")]
    public void TextBlockCurrencyCell_PorcentajeSinCerosSobrantes_SeMuestraEnEsAr(double porcentaje, string esperado)
    {
        var texto = TextoDeCelda("TextBlockCurrencyCell", "{0:0.##}%", new FilaPrueba(0m, (decimal)porcentaje));

        texto.Should().Be(esperado);
    }

    [Fact]
    public void ControlesDeListado_ConLosEstilosCompartidos_ResuelvenSusRecursosYTerminanDondeTerminanLasFilas()
    {
        double altoGrilla = 0;

        WpfTestHelper.Run(() =>
        {
            var recursos = System.Windows.Application.Current.Resources;
            var grilla = new DataGrid
            {
                Style = (Style)recursos["DataGridListadoStyle"],
                AutoGenerateColumns = false,
                ItemsSource = new[] { new FilaPrueba(1m, 1m), new FilaPrueba(2m, 2m) }
            };
            grilla.Columns.Add(new DataGridTextColumn
            {
                Header = "Importe",
                Binding = new System.Windows.Data.Binding(nameof(FilaPrueba.Importe)),
                HeaderStyle = (Style)recursos["DataGridColumnHeaderRightListadoStyle"],
                ElementStyle = (Style)recursos["TextBlockCurrencyCell"]
            });

            var filtro = new HeaderedContentControl
            {
                Header = "Rubro",
                Style = (Style)recursos["FilterFieldStyle"],
                Content = new ComboBox { Style = (Style)recursos["ComboBoxFiltroStyle"] }
            };

            var panel = new StackPanel();
            panel.Children.Add(filtro);
            panel.Children.Add(new Button { Content = "Limpiar filtros", Style = (Style)recursos["GhostNeutralButtonStyle"] });

            var contenedor = new Grid { Width = 800, Height = 600 };
            contenedor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contenedor.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contenedor.Children.Add(panel);
            Grid.SetRow(grilla, 1);
            contenedor.Children.Add(grilla);

            contenedor.Measure(new Size(800, 600));
            contenedor.Arrange(new Rect(0, 0, 800, 600));
            contenedor.UpdateLayout();
            altoGrilla = grilla.ActualHeight;
        });

        altoGrilla.Should().BeGreaterThan(0).And.BeLessThan(300, "la grilla de listado no rellena el alto sobrante");
    }
}
