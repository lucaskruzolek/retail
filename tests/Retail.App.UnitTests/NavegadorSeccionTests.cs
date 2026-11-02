using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FluentAssertions;
using Retail.App.Navegacion;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// <see cref="NavegadorSeccion"/>: ofrece las páginas de la sección y navega solo cuando el operador hace clic en una opción;
/// marcar la opción por código (al cargar o al cambiar la página abierta) nunca debe navegar.
/// </summary>
public class NavegadorSeccionTests
{
    // Doble escrito a mano: lleva la cuenta de las llamadas a IrA y, como el shell, actualiza la página abierta
    private sealed class NavegacionDoble : INavegacionSecciones
    {
        private readonly IReadOnlyList<PaginaNavegacion> _paginas;

        public NavegacionDoble(IReadOnlyList<PaginaNavegacion> paginas, string? paginaActualId)
        {
            _paginas = paginas;
            PaginaActualId = paginaActualId;
        }

        public string? PaginaActualId { get; set; }

        public event Action? PaginaActualCambio;

        public int LlamadasAIrA { get; private set; }

        public string? UltimoIrA { get; private set; }

        public IReadOnlyList<PaginaNavegacion> PaginasVisibles(string seccionId)
        {
            return _paginas;
        }

        public void IrA(string paginaId)
        {
            LlamadasAIrA++;
            UltimoIrA = paginaId;
            PaginaActualId = paginaId;
            PaginaActualCambio?.Invoke();
        }

        public void AvisarCambio()
        {
            PaginaActualCambio?.Invoke();
        }
    }

    private static IReadOnlyList<PaginaNavegacion> PaginasDeCompras()
    {
        return RegistroNavegacion.BuscarSeccion("compras")!.Paginas;
    }

    // Aloja el navegador en una ventana fuera de pantalla y espera a que se dispare Loaded
    private static void ConNavegador(INavegacionSecciones? navegacion, Action<NavegadorSeccion> prueba)
    {
        WpfTestHelper.Run(() =>
        {
            // Cerrar la última ventana apagaría la Application compartida de la suite
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var navegador = new NavegadorSeccion { SeccionId = "compras", Navegacion = navegacion };
            var ventana = new Window
            {
                Content = navegador,
                Left = -10000,
                Top = -10000,
                Width = 600,
                Height = 200,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };
            ventana.Show();
            ventana.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
            ventana.UpdateLayout();

            try
            {
                prueba(navegador);
            }
            finally
            {
                ventana.Close();
            }
        });
    }

    private static List<RadioButton> Opciones(DependencyObject raiz)
    {
        var encontradas = new List<RadioButton>();

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            DependencyObject hijo = VisualTreeHelper.GetChild(raiz, i);

            if (hijo is RadioButton opcion)
            {
                encontradas.Add(opcion);
            }
            else
            {
                encontradas.AddRange(Opciones(hijo));
            }
        }

        return encontradas;
    }

    // Lo mismo que hace WPF ante un clic del mouse o la barra espaciadora: marca la opción y dispara Click
    private static void HacerClic(RadioButton opcion)
    {
        MethodInfo alHacerClic = typeof(RadioButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
        alHacerClic.Invoke(opcion, null);
    }

    private static string? Marcada(NavegadorSeccion navegador)
    {
        return Opciones(navegador).SingleOrDefault(o => o.IsChecked == true)?.Tag as string;
    }

    [Fact]
    public void NavegadorSeccion_ConTresPaginas_MuestraTresOpcionesYMarcaLaActual()
    {
        var navegacion = new NavegacionDoble(PaginasDeCompras(), "ingresos");

        ConNavegador(navegacion, navegador =>
        {
            navegador.Visibility.Should().Be(Visibility.Visible);
            Opciones(navegador).Select(o => o.Content).Should().Equal("Pedidos", "Ingresos", "Proveedores");
            Marcada(navegador).Should().Be("ingresos");
        });
    }

    [Fact]
    public void NavegadorSeccion_ConUnaSolaPagina_QuedaColapsado()
    {
        var navegacion = new NavegacionDoble([PaginasDeCompras()[0]], "pedidos");

        ConNavegador(navegacion, navegador =>
        {
            navegador.Visibility.Should().Be(Visibility.Collapsed);
            Opciones(navegador).Should().BeEmpty();
        });
    }

    [Fact]
    public void NavegadorSeccion_AlCargarse_NoLlamaAIrA()
    {
        var navegacion = new NavegacionDoble(PaginasDeCompras(), "ingresos");

        ConNavegador(navegacion, _ =>
        {
            navegacion.LlamadasAIrA.Should().Be(0, "marcar la opción actual al cargar no es una navegación del operador");
        });
    }

    [Fact]
    public void NavegadorSeccion_ClicEnOtraOpcion_LlamaAIrAUnaVezConSuId()
    {
        var navegacion = new NavegacionDoble(PaginasDeCompras(), "ingresos");

        ConNavegador(navegacion, navegador =>
        {
            RadioButton proveedores = Opciones(navegador).Single(o => (string)o.Tag == "proveedores");

            HacerClic(proveedores);

            navegacion.LlamadasAIrA.Should().Be(1);
            navegacion.UltimoIrA.Should().Be("proveedores");
            Marcada(navegador).Should().Be("proveedores");
        });
    }

    [Fact]
    public void NavegadorSeccion_ClicEnLaOpcionActiva_NoNavega()
    {
        var navegacion = new NavegacionDoble(PaginasDeCompras(), "ingresos");

        ConNavegador(navegacion, navegador =>
        {
            RadioButton ingresos = Opciones(navegador).Single(o => (string)o.Tag == "ingresos");

            HacerClic(ingresos);

            navegacion.LlamadasAIrA.Should().Be(0);
            Marcada(navegador).Should().Be("ingresos");
        });
    }

    [Fact]
    public void NavegadorSeccion_AlCambiarLaPaginaActual_MueveLaMarcaSinLlamarAIrA()
    {
        var navegacion = new NavegacionDoble(PaginasDeCompras(), "ingresos");

        ConNavegador(navegacion, navegador =>
        {
            navegacion.PaginaActualId = "pedidos";
            navegacion.AvisarCambio();

            Marcada(navegador).Should().Be("pedidos");
            navegacion.LlamadasAIrA.Should().Be(0, "reaccionar al cambio de página no debe volver a navegar");
        });
    }

    [Fact]
    public void NavegadorSeccion_SinNavegacionNiContenedor_QuedaColapsadoSinFallar()
    {
        ConNavegador(null, navegador =>
        {
            navegador.Visibility.Should().Be(Visibility.Collapsed);
            Opciones(navegador).Should().BeEmpty();
        });
    }

    [Fact]
    public void NavegadorSeccion_ConAtajo_ElTooltipLoIncluye()
    {
        var navegacion = new NavegacionDoble(PaginasDeCompras().Concat(RegistroNavegacion.BuscarSeccion("ventas")!.Paginas).ToList(), "ingresos");

        ConNavegador(navegacion, navegador =>
        {
            List<RadioButton> opciones = Opciones(navegador);

            opciones.Single(o => (string)o.Tag == "caja").ToolTip.Should().Be("Caja y turnos (F5)");
            opciones.Single(o => (string)o.Tag == "ingresos").ToolTip.Should().Be("Ingresos");
        });
    }
}
