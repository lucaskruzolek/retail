using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Retail.App.Converters;
using Retail.App.Helpers;
using Retail.App.UnitTests.Helpers;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Componentes de las páginas de listado (<c>Views/Components</c>): encabezado de página, barra de búsqueda, barra de
/// acciones masivas y el botón "⋮" que abre un menú.
/// </summary>
public class ComponentesListadoTests
{
    private static void Distribuir(FrameworkElement elemento)
    {
        // Un control creado por código no aplica su estilo implícito hasta inicializarse; el parser de XAML llama a EndInit
        if (!elemento.IsInitialized)
        {
            elemento.BeginInit();
            elemento.EndInit();
        }

        elemento.Measure(new Size(1000, 400));
        elemento.Arrange(new Rect(0, 0, 1000, 400));
        elemento.UpdateLayout();
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

    [Fact]
    public void PageHeader_ConTituloSubtituloYAcciones_MuestraTodo()
    {
        string[] textos = [];
        bool accionEnElArbol = false;

        WpfTestHelper.Run(() =>
        {
            var boton = new Button { Content = "Nuevo" };
            var encabezado = new PageHeader { Titulo = "Catálogo", Subtitulo = "Padrón central", Content = boton };
            Distribuir(encabezado);
            textos = Descendientes<TextBlock>(encabezado).Select(t => t.Text).ToArray();
            accionEnElArbol = Descendientes<Button>(encabezado).Contains(boton);
        });

        textos.Should().BeEquivalentTo(["Nuevo", "Catálogo", "Padrón central"], "solo el botón, el título y el subtítulo: sin insignias");
        accionEnElArbol.Should().BeTrue("las acciones son el contenido del encabezado");
    }

    [Fact]
    public void PageHeader_AccionesDentroDeUnaPagina_HeredanElDataContextDeLaPagina()
    {
        object? contextoDelBoton = null;
        var viewModel = new object();

        WpfTestHelper.Run(() =>
        {
            var boton = new Button { Content = "Nuevo" };
            var pagina = new Grid { DataContext = viewModel };
            pagina.Children.Add(new PageHeader { Titulo = "Página", Content = boton });
            Distribuir(pagina);
            contextoDelBoton = boton.DataContext;
        });

        contextoDelBoton.Should().BeSameAs(viewModel, "es la razón de que PageHeader sea un control con contenido");
    }

    [Theory]
    [InlineData(0, "0 resultados")]
    [InlineData(1, "1 resultado")]
    [InlineData(248, "248 resultados")]
    [InlineData(1234, "1.234 resultados")]
    public void ListToolbar_TotalDeResultados_SeMuestraEnEsAr(int total, string esperado)
    {
        string? texto = null;

        WpfTestHelper.Run(() =>
        {
            var barra = new ListToolbar { TotalResultados = total };
            Distribuir(barra);
            texto = barra.TextoContador;
        });

        texto.Should().Be(esperado);
    }

    [Fact]
    public void ListToolbar_ComandoLimpiarYTexto_SeEnlazanALosControlesInternos()
    {
        bool limpiado = false;
        bool botonHabilitado = false;
        string? textoDeLaCaja = null;

        WpfTestHelper.Run(() =>
        {
            var comando = new RelayCommand(() => limpiado = true);
            var barra = new ListToolbar { ComandoLimpiar = comando, Texto = "cuaderno" };
            Distribuir(barra);
            var boton = Descendientes<Button>(barra).First(b => ReferenceEquals(b.Command, comando));
            botonHabilitado = boton.IsEnabled;
            textoDeLaCaja = Descendientes<TextBox>(barra).First().Text;
            boton.Command.Execute(null);
        });

        botonHabilitado.Should().BeTrue();
        textoDeLaCaja.Should().Be("cuaderno");
        limpiado.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, "1 seleccionado")]
    [InlineData(3, "3 seleccionados")]
    public void BulkActionBar_CantidadSeleccionada_ArmaElTexto(int cantidad, string esperado)
    {
        string? texto = null;
        bool aparece = false;

        WpfTestHelper.Run(() =>
        {
            var barra = new BulkActionBar { CantidadSeleccionados = cantidad, Content = new Button { Content = "Pedir" } };
            Distribuir(barra);
            texto = barra.TextoSeleccion;
            aparece = Descendientes<TextBlock>(barra).Any(t => t.Text == esperado);
        });

        texto.Should().Be(esperado);
        aparece.Should().BeTrue("la plantilla muestra el texto calculado");
    }

    [Fact]
    public void PageHeader_SinSubtitulo_ColapsaSuLinea()
    {
        Visibility subtitulo = Visibility.Visible;

        WpfTestHelper.Run(() =>
        {
            var encabezado = new PageHeader { Titulo = "Catálogo" };
            Distribuir(encabezado);
            subtitulo = Descendientes<TextBlock>(encabezado).Single(t => t.Name == "TextoSubtitulo").Visibility;
        });

        subtitulo.Should().Be(Visibility.Collapsed, "sin subtítulo no queda una línea vacía y el título se centra con las acciones");
    }

    [Fact]
    public void ListToolbar_ConSeleccion_LasAccionesReemplazanALimpiarFiltrosYAlContador()
    {
        bool contadorVisible = true;
        bool accionesVisibles = false;
        bool limpiarVisible = false;
        object? contextoDeLasAcciones = null;
        var viewModel = new object();

        WpfTestHelper.Run(() =>
        {
            var limpiar = new RelayCommand(() => { });
            var acciones = new BulkActionBar { CantidadSeleccionados = 2 };
            var barra = new ListToolbar { TotalResultados = 10, ComandoLimpiar = limpiar, AccionesDeSeleccion = acciones, HaySeleccion = true };
            var pagina = new Grid { DataContext = viewModel };
            pagina.Children.Add(barra);
            Distribuir(pagina);
            // Fuera de una ventana IsVisible es siempre falso: se mira la Visibility de cada elemento
            contadorVisible = ((FrameworkElement)barra.FindName("PanelNormal")).Visibility == Visibility.Visible;
            accionesVisibles = ((FrameworkElement)barra.FindName("PanelSeleccion")).Visibility == Visibility.Visible
                && Descendientes<BulkActionBar>(barra).Contains(acciones);
            var botonLimpiar = Descendientes<Button>(barra).Single(b => ReferenceEquals(b.Command, limpiar));
            limpiarVisible = botonLimpiar.Visibility == Visibility.Visible
                && ((FrameworkElement)botonLimpiar.Parent).Visibility == Visibility.Visible;
            contextoDeLasAcciones = acciones.DataContext;
        });

        contadorVisible.Should().BeFalse();
        accionesVisibles.Should().BeTrue();
        limpiarVisible.Should().BeFalse("el badge \"N seleccionados ✕\" ocupa el lugar de \"Limpiar filtros\"");
        contextoDeLasAcciones.Should().BeSameAs(viewModel, "así los botones de las acciones se enlazan a los comandos de la página");
    }

    [Fact]
    public void BulkActionBar_CancelarSeleccion_EjecutaElComando()
    {
        bool cancelado = false;

        WpfTestHelper.Run(() =>
        {
            var barra = new BulkActionBar { CantidadSeleccionados = 2, ComandoCancelar = new RelayCommand(() => cancelado = true) };
            Distribuir(barra);
            var cancelar = Descendientes<Button>(barra).Single(b => b.Command is not null);
            cancelar.Command.Execute(null);
        });

        cancelado.Should().BeTrue();
    }

    [Fact]
    public void MenuDesplegableHelper_Ubicacion_QuedaDebajoDelBotonConLosBordesDerechosAlineados()
    {
        var ubicaciones = MenuDesplegableHelper.UbicarDebajoAlineadoALaDerecha(new Size(200, 120), new Size(32, 32), new Point(0, 0));

        ubicaciones.Should().ContainSingle();
        ubicaciones[0].Point.X.Should().Be(-168, "el borde derecho del menú (x + 200) coincide con el del botón (32)");
        ubicaciones[0].Point.Y.Should().Be(32, "el menú arranca justo debajo del botón");
    }

    [Fact]
    public void AMayusculasConverter_Texto_SeMuestraEnMayusculasYElNuloQuedaVacio()
    {
        var conversor = new AMayusculasConverter();
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-AR");

        conversor.Convert("cuad. Tapa blanda koby/data", typeof(string), null, cultura).Should().Be("CUAD. TAPA BLANDA KOBY/DATA");
        conversor.Convert("pequeñas", typeof(string), null, cultura).Should().Be("PEQUEÑAS");
        conversor.Convert(null, typeof(string), null, cultura).Should().Be(string.Empty);
        FluentActions.Invoking(() => conversor.ConvertBack("X", typeof(string), null, cultura)).Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void EsquinasHelper_ConRadioCero_QuitaElRedondeoYElRecorte()
    {
        CornerRadius radio = new(8);
        bool recortada = true;

        WpfTestHelper.Run(() =>
        {
            var grilla = new DataGrid
            {
                Style = (Style)System.Windows.Application.Current.FindResource("DataGridListadoStyle"),
                AutoGenerateColumns = false
            };
            EsquinasHelper.SetEsquinas(grilla, new CornerRadius(0));
            var contenedor = new Grid { Width = 400, Height = 200 };
            contenedor.Children.Add(grilla);
            Distribuir(contenedor);

            radio = Descendientes<Border>(grilla).First().CornerRadius;
            recortada = Descendientes<ScrollViewer>(grilla).First().Clip is not null;
        });

        radio.Should().Be(new CornerRadius(0), "una grilla pegada a los bordes de su tarjeta no se redondea");
        recortada.Should().BeFalse();
    }

    [Fact]
    public void PaginationBar_ComboDeFilas_MideComoLosBotonesYMuestraElNumeroCompleto()
    {
        double alturaBordeVisible = 0;
        double alturaBoton = 0;
        string? textoMostrado = null;
        double anchoDelTexto = 0;

        WpfTestHelper.Run(() =>
        {
            var barra = new PaginationBar { DataContext = new PaginacionDePrueba() };
            Distribuir(barra);
            var combo = Descendientes<ComboBox>(barra).Single();
            alturaBordeVisible = Descendientes<Border>(combo).First(b => b.Name == "ContentBorder").ActualHeight;
            alturaBoton = Descendientes<Button>(barra).First().ActualHeight;
            var texto = Descendientes<TextBlock>(combo).First(t => t.Text == "10");
            textoMostrado = texto.Text;
            anchoDelTexto = texto.ActualWidth;
        });

        alturaBordeVisible.Should().BeApproximately(alturaBoton, 0.5, "el combo se ve tan alto como los botones de al lado");
        textoMostrado.Should().Be("10");
        anchoDelTexto.Should().BeGreaterThan(10, "el contenedor del número no puede colapsar a 0 de ancho");
    }

    private sealed class PaginacionDePrueba
    {
        public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = [10, 20, 50];

        public int TamanoPagina { get; set; } = 10;
    }

    [Fact]
    public void CheckBoxCompactStyle_EnTodosSusEstados_MideDieciseisYMuestraSuMarca()
    {
        double ancho = 0;
        double alto = 0;
        Visibility marcaMarcada = Visibility.Collapsed;
        Visibility guionIntermedio = Visibility.Collapsed;
        Visibility guionMarcada = Visibility.Visible;

        WpfTestHelper.Run(() =>
        {
            var estilo = (Style)System.Windows.Application.Current.FindResource("CheckBoxCompactStyle");
            var casilla = new CheckBox { Style = estilo, IsChecked = true };
            Distribuir(casilla);
            ancho = casilla.ActualWidth;
            alto = casilla.ActualHeight;
            marcaMarcada = Descendientes<System.Windows.Shapes.Path>(casilla).Single().Visibility;
            guionMarcada = Descendientes<System.Windows.Shapes.Rectangle>(casilla).Single().Visibility;

            var intermedia = new CheckBox { Style = estilo, IsChecked = null, IsThreeState = true };
            Distribuir(intermedia);
            guionIntermedio = Descendientes<System.Windows.Shapes.Rectangle>(intermedia).Single().Visibility;
        });

        ancho.Should().Be(16);
        alto.Should().Be(16);
        marcaMarcada.Should().Be(Visibility.Visible);
        guionMarcada.Should().Be(Visibility.Collapsed);
        guionIntermedio.Should().Be(Visibility.Visible);
    }

    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void PaginationBar_MostrarResumen_ControlaElTextoDeRegistros(bool mostrar, Visibility esperado)
    {
        Visibility visibilidad = Visibility.Hidden;

        WpfTestHelper.Run(() =>
        {
            var barra = new PaginationBar { MostrarResumen = mostrar };
            Distribuir(barra);
            var resumen = Descendientes<TextBlock>(barra).First(t => DockPanel.GetDock(t) == Dock.Left);
            visibilidad = resumen.Visibility;
        });

        visibilidad.Should().Be(esperado);
    }
}
