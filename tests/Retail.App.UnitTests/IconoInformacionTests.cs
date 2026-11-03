using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using MahApps.Metro.IconPacks;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>El ícono de información: chico, con una zona de impacto que cubre todo el glifo y un tooltip rápido.</summary>
public class IconoInformacionTests
{
    private static void Distribuir(FrameworkElement elemento)
    {
        // Como dentro de un StackPanel: con el tamaño que pide, no estirado
        elemento.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        elemento.Arrange(new Rect(elemento.DesiredSize));
        elemento.UpdateLayout();
    }

    [Fact]
    public void IconoInformacion_ConTexto_EsVisibleYElGlifoEsMasChicoQueSuZonaDeImpacto()
    {
        Visibility visibilidad = Visibility.Collapsed;
        double ancho = 0;
        double alto = 0;
        double glifo = 0;
        object? tooltip = null;

        WpfTestHelper.Run(() =>
        {
            var icono = new IconoInformacion { Texto = "Copias · Historial de copias" };
            Distribuir(icono);
            var area = (Border)icono.FindName("Area");
            visibilidad = icono.Visibility;
            ancho = area.ActualWidth;
            alto = area.ActualHeight;
            glifo = Descendientes<PackIconBoxIcons>(icono).Single().Width;
            tooltip = area.ToolTip;
        });

        visibilidad.Should().Be(Visibility.Visible);
        glifo.Should().Be(12, "el ícono es chico");
        ancho.Should().Be(20);
        alto.Should().Be(20);
        glifo.Should().BeLessThan(ancho, "la zona de impacto rodea al glifo");
        tooltip.Should().Be("Copias · Historial de copias");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(19, 19)]
    [InlineData(10, 10)]
    public void IconoInformacion_PuntoDentroDeLaZonaPeroFueraDeLosTrazos_RespondeAlMouse(double x, double y)
    {
        // (1,1) y (19,19) caen en las esquinas del cuadrado, a varios píxeles del glifo centrado de 12
        HitTestResult? resultado = null;

        WpfTestHelper.Run(() =>
        {
            var icono = new IconoInformacion { Texto = "Descripción" };
            Distribuir(icono);
            resultado = VisualTreeHelper.HitTest(icono, new Point(x, y));
        });

        resultado.Should().NotBeNull("el fondo transparente del área hace que todo el cuadrado cuente para el mouse");
    }

    [Fact]
    public void IconoInformacion_PuntoFueraDeLaZona_NoResponde()
    {
        HitTestResult? resultado = null;

        WpfTestHelper.Run(() =>
        {
            var icono = new IconoInformacion { Texto = "Descripción" };
            Distribuir(icono);
            resultado = VisualTreeHelper.HitTest(icono, new Point(25, 10));
        });

        resultado.Should().BeNull();
    }

    [Fact]
    public void IconoInformacion_Tooltip_AparecePronto()
    {
        int demora = 0;
        int duracion = 0;

        WpfTestHelper.Run(() =>
        {
            var icono = new IconoInformacion { Texto = "Descripción" };
            Distribuir(icono);
            var area = (Border)icono.FindName("Area");
            demora = ToolTipService.GetInitialShowDelay(area);
            duracion = ToolTipService.GetShowDuration(area);
        });

        demora.Should().Be(150, "el predeterminado de Windows (500 ms) hace que el ícono parezca no responder");
        duracion.Should().BeGreaterThan(10000, "una descripción larga necesita tiempo para leerse");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IconoInformacion_SinTexto_SeOculta(string? texto)
    {
        Visibility visibilidad = Visibility.Visible;

        WpfTestHelper.Run(() =>
        {
            var icono = new IconoInformacion { Texto = texto };
            Distribuir(icono);
            visibilidad = icono.Visibility;
        });

        visibilidad.Should().Be(Visibility.Collapsed);
    }

    private static IEnumerable<T> Descendientes<T>(DependencyObject raiz) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
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
}
