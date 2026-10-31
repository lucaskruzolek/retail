using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using FluentAssertions;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// <see cref="EncabezadoDialogo"/>: la banda de título de los diálogos. Se verifica que la plantilla se resuelva, que el
/// botón de maximizar dependa de <see cref="EncabezadoDialogo.PermitirMaximizar"/> y que Esc y la ✕ cierren la ventana
/// (Esc solo si ningún control la atendió antes).
/// </summary>
public class EncabezadoDialogoTests
{
    // El encabezado alojado en una ventana fuera de pantalla, para que tenga plantilla, layout y Loaded reales
    private static void ConEncabezado(bool permitirMaximizar, Action<EncabezadoDialogo, Window> prueba)
    {
        WpfTestHelper.Run(() =>
        {
            // Cerrar la última ventana apagaría la Application compartida de la suite
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var encabezado = new EncabezadoDialogo { Titulo = "Nuevo artículo", PermitirMaximizar = permitirMaximizar };
            var ventana = new Window
            {
                Content = new StackPanel { Children = { encabezado } },
                Left = -10000,
                Top = -10000,
                Width = 600,
                Height = 200,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };
            ventana.Show();
            ventana.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            try
            {
                prueba(encabezado, ventana);
            }
            finally
            {
                ventana.Close();
            }
        });
    }

    private static Button Boton(EncabezadoDialogo encabezado, string nombre)
    {
        return (Button)encabezado.Template.FindName(nombre, encabezado);
    }

    private static KeyEventArgs Escape(Window ventana)
    {
        return new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(ventana), 0, Key.Escape)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
    }

    [Fact]
    public void EncabezadoDialogo_Instanciado_ResuelveLaPlantillaConLaAlturaDeLaBanda()
    {
        ConEncabezado(permitirMaximizar: false, (encabezado, _) =>
        {
            var altoBanda = (double)System.Windows.Application.Current.FindResource("DialogTitleBarHeight");

            encabezado.ActualHeight.Should().Be(altoBanda);
            Boton(encabezado, "PART_Cerrar").IsVisible.Should().BeTrue();
            Boton(encabezado, "PART_Minimizar").IsVisible.Should().BeTrue();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EncabezadoDialogo_PermitirMaximizar_MuestraOOcultaElBoton(bool permitir)
    {
        ConEncabezado(permitir, (encabezado, _) =>
        {
            Boton(encabezado, "PART_Maximizar").IsVisible.Should().Be(permitir);
        });
    }

    [Fact]
    public void EncabezadoDialogo_BotonesDeVentana_NoTomanElFocoDelTeclado()
    {
        ConEncabezado(permitirMaximizar: true, (encabezado, _) =>
        {
            Boton(encabezado, "PART_Cerrar").Focusable.Should().BeFalse("Esc cierra el diálogo; la ✕ no es una parada de Tab");
        });
    }

    [Fact]
    public void Escape_SinQueNingunControlLaAtienda_CierraLaVentana()
    {
        bool cerrada = false;

        ConEncabezado(permitirMaximizar: false, (encabezado, ventana) =>
        {
            ventana.Closed += (_, _) => cerrada = true;

            encabezado.RaiseEvent(Escape(ventana));
        });

        cerrada.Should().BeTrue();
    }

    [Fact]
    public void Escape_AtendidaPorUnControlDelDialogo_NoCierraLaVentana()
    {
        bool cerradaPorEscape = false;

        ConEncabezado(permitirMaximizar: false, (encabezado, ventana) =>
        {
            // Como un desplegable abierto, que usa Esc para cerrarse a sí mismo
            encabezado.KeyDown += (_, e) => e.Handled = true;
            ventana.Closing += (_, _) => cerradaPorEscape = true;

            encabezado.RaiseEvent(Escape(ventana));
            cerradaPorEscape.Should().BeFalse();
        });
    }

    [Fact]
    public void Escape_ConUnBotonCancelarEnElDialogo_LoDejaAWpfYNoCierraPorSuCuenta()
    {
        bool cerradaPorElEncabezado = false;

        ConEncabezado(permitirMaximizar: false, (encabezado, ventana) =>
        {
            // El Cancelar del diálogo (deshabilitado, como mientras el diálogo trabaja): Esc no debe cerrar igual
            ((StackPanel)ventana.Content).Children.Add(new Button { Content = "Cancelar", IsCancel = true, IsEnabled = false });
            ventana.UpdateLayout();
            ventana.Closing += (_, _) => cerradaPorElEncabezado = true;

            var tecla = Escape(ventana);
            encabezado.RaiseEvent(tecla);

            tecla.Handled.Should().BeFalse("la tecla queda para el botón IsCancel, que WPF ejecuta si está habilitado");
            cerradaPorElEncabezado.Should().BeFalse();
        });
    }

    [Fact]
    public void BotonCerrar_AlHacerClic_CierraLaVentana()
    {
        bool cerrada = false;

        ConEncabezado(permitirMaximizar: false, (encabezado, ventana) =>
        {
            ventana.Closed += (_, _) => cerrada = true;

            Boton(encabezado, "PART_Cerrar").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });

        cerrada.Should().BeTrue();
    }
}
