using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// El indicador de <see cref="SelectorSegmentado"/> debe verse cuando la página que lo contiene se aloja en un
/// <see cref="Frame"/>, como en el shell, y al cambiar de opción.
/// </summary>
public class SelectorSegmentadoEnFrameTests
{
    [Fact]
    public void SelectorSegmentado_EnUnFrame_ElIndicadorSeMuestraAlEntrarYAlCambiarDeOpcion()
    {
        bool visibleAlEntrar = false;
        bool visibleAlCambiar = false;

        WpfTestHelper.Run(() =>
        {
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var estilo = (Style)System.Windows.Application.Current.FindResource("SegmentedRadioButtonStyle");
            var primera = new RadioButton { Content = "Copias", GroupName = "EnFrame", Style = estilo, IsChecked = true };
            var segunda = new RadioButton { Content = "Restauraciones", GroupName = "EnFrame", Style = estilo };
            var selector = new SelectorSegmentado { Content = new UniformGrid { Columns = 2, Children = { primera, segunda } } };
            var pagina = new UserControl { Content = selector };

            var marco = new Frame { NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden };
            var ventana = new Window { Content = marco, Left = -10000, Top = -10000, Width = 900, Height = 600, ShowActivated = false };
            ventana.Show();

            marco.Content = pagina;
            Vaciar();

            var indicador = (FrameworkElement)selector.Template.FindName("PART_Indicador", selector);
            visibleAlEntrar = indicador.Visibility == Visibility.Visible && indicador.Width > 0;

            segunda.IsChecked = true;
            Vaciar();
            visibleAlCambiar = indicador.Visibility == Visibility.Visible && indicador.Width > 0;

            ventana.Close();
        });

        visibleAlEntrar.Should().BeTrue("la opción marcada debe verse seleccionada al entrar a la página");
        visibleAlCambiar.Should().BeTrue("la nueva opción debe verse seleccionada al cambiar");
    }

    private static void Vaciar()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
