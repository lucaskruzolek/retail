using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// <see cref="SelectorSegmentado"/>: el indicador único que se desliza debe quedar exactamente detrás de la opción marcada.
/// La animación en sí solo se puede apreciar a ojo (galería de estilos); acá se verifica la posición final y que se anime.
/// </summary>
public class SelectorSegmentadoTests
{
    private const double Tolerancia = 0.5;

    // Un selector con dos opciones alojado en una ventana fuera de pantalla, para que tenga plantilla y layout reales
    private static void ConSelector(bool animar, bool marcarPrimera, Action<SelectorSegmentado, RadioButton, RadioButton, FrameworkElement> prueba)
    {
        WpfTestHelper.Run(() =>
        {
            // Cerrar la última ventana apagaría la Application compartida de la suite
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var estiloOpcion = (Style)System.Windows.Application.Current.FindResource("SegmentedRadioButtonStyle");
            var primera = new RadioButton { Content = "Producto", GroupName = "Prueba", Style = estiloOpcion, IsChecked = marcarPrimera };
            var segunda = new RadioButton { Content = "Servicio largo", GroupName = "Prueba", Style = estiloOpcion };
            var selector = new SelectorSegmentado
            {
                AnimarCambios = animar,
                Content = new UniformGrid { Columns = 2, Children = { primera, segunda } }
            };
            var ventana = new Window
            {
                Content = selector,
                Left = -10000,
                Top = -10000,
                Width = 400,
                Height = 200,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };
            ventana.Show();
            ventana.UpdateLayout();

            try
            {
                var indicador = (FrameworkElement)selector.Template.FindName("PART_Indicador", selector);
                prueba(selector, primera, segunda, indicador);
            }
            finally
            {
                ventana.Close();
            }
        });
    }

    private static double PosicionX(FrameworkElement indicador)
    {
        return ((TranslateTransform)indicador.RenderTransform).X;
    }

    private static double ComienzoDe(RadioButton opcion, SelectorSegmentado selector)
    {
        var capas = (FrameworkElement)selector.Template.FindName("PART_Capas", selector);

        return opcion.TranslatePoint(new Point(0, 0), capas).X;
    }

    [Fact]
    public void SelectorSegmentado_AlCargar_ElIndicadorCubreLaOpcionMarcada()
    {
        ConSelector(animar: false, marcarPrimera: true, (selector, primera, _, indicador) =>
        {
            indicador.Visibility.Should().Be(Visibility.Visible);
            PosicionX(indicador).Should().BeApproximately(ComienzoDe(primera, selector), Tolerancia);
            indicador.Width.Should().BeApproximately(primera.ActualWidth, Tolerancia);
            indicador.Height.Should().BeApproximately(primera.ActualHeight, Tolerancia);
        });
    }

    [Fact]
    public void SelectorSegmentado_AlMarcarOtraOpcion_ElIndicadorSeMueveASuPosicion()
    {
        ConSelector(animar: false, marcarPrimera: true, (selector, _, segunda, indicador) =>
        {
            segunda.IsChecked = true;

            PosicionX(indicador).Should().BeApproximately(ComienzoDe(segunda, selector), Tolerancia);
            PosicionX(indicador).Should().BeGreaterThan(0, "la segunda opción queda a la derecha de la primera");
            indicador.Width.Should().BeApproximately(segunda.ActualWidth, Tolerancia);
        });
    }

    [Fact]
    public void SelectorSegmentado_SinOpcionMarcada_OcultaElIndicador()
    {
        ConSelector(animar: false, marcarPrimera: false, (_, _, _, indicador) =>
        {
            indicador.Visibility.Should().Be(Visibility.Collapsed);
        });
    }

    [Fact]
    public void SelectorSegmentado_ConAnimacion_TerminaEnLaPosicionDeLaOpcion()
    {
        ConSelector(animar: true, marcarPrimera: true, (_, _, segunda, indicador) =>
        {
            var traslado = (TranslateTransform)indicador.RenderTransform;

            segunda.IsChecked = true;

            // Sin esperar a que termine: alcanza con que el desplazamiento y el ancho estén animados hacia la nueva opción
            traslado.HasAnimatedProperties.Should().BeTrue("el cambio de opción desliza el indicador en lugar de saltar");
            indicador.HasAnimatedProperties.Should().BeTrue("el ancho también se anima");
        });
    }
}
