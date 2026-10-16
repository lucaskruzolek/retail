using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

public class SelectorFechaCortaTests
{
    [Fact]
    public void SelectedDate_AlElegirUnaFecha_LaCajaMuestraDiaMesAnioConCeros()
    {
        // Arrange + Act
        var texto = WpfTestHelper.Run(() =>
        {
            // Cerrar la ventana de la prueba no debe apagar la Application compartida por toda la suite.
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var selector = new SelectorFechaCorta { Language = System.Windows.Markup.XmlLanguage.GetLanguage("es-AR") };
            var ventana = new Window { Content = selector, Width = 300, Height = 100, ShowInTaskbar = false, Left = -3000, Top = -3000 };
            ventana.Show();
            selector.ApplyTemplate();
            selector.SelectedDate = new DateTime(2026, 10, 1);

            // El formato se reaplica de forma diferida: se vacía la cola del Dispatcher.
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            var caja = (DatePickerTextBox)selector.Template.FindName("PART_TextBox", selector);
            var resultado = caja.Text;
            ventana.Close();
            return resultado;
        });

        // Assert
        texto.Should().Be("01/10/2026");
    }
}
