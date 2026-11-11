using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// <see cref="PageHeader"/> en dos filas: el título en la banda de ventana (56) y, debajo, las pestañas
/// (<see cref="PageHeader.Navegacion"/>) con las acciones de la página. Se verifica que la navegación herede el
/// DataContext de la página, que la segunda fila se colapse vacía y que la primera tenga el alto de la banda.
/// </summary>
public class PageHeaderTests
{
    private sealed record PaginaFalsa(string Texto);

    // El encabezado alojado en una ventana fuera de pantalla, para que tenga plantilla, layout y Loaded reales
    private static void ConEncabezado(Func<PageHeader> crear, object? dataContext, Action<PageHeader> prueba)
    {
        WpfTestHelper.Run(() =>
        {
            var encabezado = crear();

            // Cerrar la última ventana apagaría la Application compartida de la suite
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var ventana = new Window
            {
                Content = new StackPanel { DataContext = dataContext, Children = { encabezado } },
                Left = -10000,
                Top = -10000,
                Width = 600,
                Height = 300,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };
            ventana.Show();
            ventana.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            try
            {
                prueba(encabezado);
            }
            finally
            {
                ventana.Close();
            }
        });
    }

    [Fact]
    public void PageHeader_ConNavegacion_ElContenidoHeredaElDataContextDeLaPagina()
    {
        ConEncabezado(
            () =>
            {
                var texto = new TextBlock();
                texto.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(PaginaFalsa.Texto)));
                return new PageHeader { Titulo = "Tablero", Navegacion = texto };
            },
            new PaginaFalsa("Resumen"),
            cabecera => ((TextBlock)cabecera.Navegacion!).Text.Should().Be("Resumen"));
    }

    [Fact]
    public void PageHeader_SinNavegacionNiAcciones_ColapsaLaSegundaFila()
    {
        ConEncabezado(() => new PageHeader { Titulo = "Ajustes" }, null, cabecera =>
        {
            var segundaFila = (FrameworkElement)cabecera.Template.FindName("PART_FilaAcciones", cabecera);
            segundaFila.Visibility.Should().Be(Visibility.Collapsed);
        });
    }

    [Fact]
    public void PageHeader_ConNavegacion_MuestraLaSegundaFila()
    {
        ConEncabezado(() => new PageHeader { Titulo = "Tablero", Navegacion = new TextBlock { Text = "Pestañas" } }, null, cabecera =>
        {
            var segundaFila = (FrameworkElement)cabecera.Template.FindName("PART_FilaAcciones", cabecera);
            segundaFila.Visibility.Should().Be(Visibility.Visible);
        });
    }

    [Fact]
    public void PageHeader_FilaDelTitulo_TieneElAltoDeLaBandaDeVentana()
    {
        ConEncabezado(() => new PageHeader { Titulo = "Ajustes" }, null, cabecera =>
        {
            var filaTitulo = (FrameworkElement)cabecera.Template.FindName("PART_FilaTitulo", cabecera);
            var banda = (double)System.Windows.Application.Current.FindResource("DialogTitleBarHeight");
            filaTitulo.ActualHeight.Should().Be(banda);
        });
    }
}
