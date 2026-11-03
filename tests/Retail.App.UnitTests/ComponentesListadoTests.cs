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
    public void PageHeader_ConTituloDescripcionYAcciones_MuestraTodo()
    {
        string[] textos = [];
        bool accionEnElArbol = false;
        Visibility icono = Visibility.Collapsed;
        object? tooltip = null;

        WpfTestHelper.Run(() =>
        {
            var boton = new Button { Content = "Nuevo" };
            var encabezado = new PageHeader { Titulo = "Catálogo", Descripcion = "Padrón central", Content = boton };
            Distribuir(encabezado);
            textos = Descendientes<TextBlock>(encabezado).Select(t => t.Text).ToArray();
            accionEnElArbol = Descendientes<Button>(encabezado).Contains(boton);
            var ayuda = Descendientes<IconoInformacion>(encabezado).Single();
            icono = ayuda.Visibility;
            tooltip = ayuda.Texto;
        });

        textos.Should().BeEquivalentTo(["Nuevo", "Catálogo"], "solo el botón y el título: la descripción no ocupa una línea");
        accionEnElArbol.Should().BeTrue("las acciones son el contenido del encabezado");
        icono.Should().Be(Visibility.Visible);
        tooltip.Should().Be("Padrón central", "la descripción de la página vive en el tooltip del ícono");
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
    public void PageHeader_SinDescripcion_OcultaElIcono()
    {
        Visibility icono = Visibility.Visible;

        WpfTestHelper.Run(() =>
        {
            var encabezado = new PageHeader { Titulo = "Catálogo" };
            Distribuir(encabezado);
            icono = Descendientes<IconoInformacion>(encabezado).Single().Visibility;
        });

        icono.Should().Be(Visibility.Collapsed, "sin descripción no hay ícono que no diga nada");
    }

    [Fact]
    public void ListToolbar_ConAccionesMasivas_LasMuestraSiempreJuntoAlBuscadorYALimpiarFiltros()
    {
        bool accionesVisibles = false;
        bool limpiarVisible = false;
        object? contextoDeLasAcciones = null;
        var viewModel = new object();

        WpfTestHelper.Run(() =>
        {
            var limpiar = new RelayCommand(() => { });
            var acciones = new BulkActionBar { CantidadSeleccionados = 0 };
            var barra = new ListToolbar { ComandoLimpiar = limpiar, AccionesDeSeleccion = acciones };
            var pagina = new Grid { DataContext = viewModel };
            pagina.Children.Add(barra);
            Distribuir(pagina);
            // Fuera de una ventana IsVisible es siempre falso: se mira la Visibility de cada elemento
            accionesVisibles = ((FrameworkElement)barra.FindName("PanelSeleccion")).Visibility == Visibility.Visible
                && Descendientes<BulkActionBar>(barra).Contains(acciones);
            var botonLimpiar = Descendientes<Button>(barra).Single(b => ReferenceEquals(b.Command, limpiar));
            limpiarVisible = botonLimpiar.Visibility == Visibility.Visible
                && ((FrameworkElement)botonLimpiar.Parent).Visibility == Visibility.Visible;
            contextoDeLasAcciones = acciones.DataContext;
        });

        accionesVisibles.Should().BeTrue("las acciones masivas están a la vista aunque no haya filas marcadas");
        limpiarVisible.Should().BeTrue("\"Limpiar filtros\" ya no cede su lugar a las acciones masivas");
        contextoDeLasAcciones.Should().BeSameAs(viewModel, "así los botones de las acciones se enlazan a los comandos de la página");
    }

    [Theory]
    [InlineData(0, Visibility.Collapsed)]
    [InlineData(2, Visibility.Visible)]
    public void BulkActionBar_InsigniaDeCantidad_SoloApareceConFilasMarcadas(int cantidad, Visibility esperada)
    {
        Visibility insignia = Visibility.Visible;

        WpfTestHelper.Run(() =>
        {
            var barra = new BulkActionBar { CantidadSeleccionados = cantidad };
            Distribuir(barra);
            insignia = Descendientes<Border>(barra).Single(b => b.Name == "InsigniaSeleccion").Visibility;
        });

        insignia.Should().Be(esperada);
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

    [Theory]
    [InlineData("DataGridRetailStyle")]
    [InlineData("DataGridListadoStyle")]
    public void Grilla_SinEsquinasExplicitas_TieneElBordeRedondeadoDeOchoYSuContenidoRecortado(string estilo)
    {
        // El Tablero usa DataGridRetailStyle sin declarar Esquinas: el radio por defecto (8) debe llegar a la plantilla. Un
        // Binding de ruta a la propiedad adjunta fallaba ("property not found") y dejaba las esquinas cuadradas.
        CornerRadius radio = default;
        double radioDelRecorte = 0;
        bool recortada = false;

        WpfTestHelper.Run(() =>
        {
            var grilla = new DataGrid
            {
                Style = (Style)System.Windows.Application.Current.FindResource(estilo),
                AutoGenerateColumns = false
            };
            var contenedor = new Grid { Width = 400, Height = 200 };
            contenedor.Children.Add(grilla);
            Distribuir(contenedor);

            radio = Descendientes<Border>(grilla).First().CornerRadius;
            var desplazable = Descendientes<ScrollViewer>(grilla).First();
            radioDelRecorte = EsquinasHelper.GetRadioDelRecorte(desplazable);
            recortada = desplazable.Clip is not null;
        });

        radio.Should().Be(new CornerRadius(8));
        radioDelRecorte.Should().Be(8);
        recortada.Should().BeTrue("sin el recorte el fondo del encabezado se saldría de las esquinas");
    }

    [Fact]
    public void Grilla_ConEsquinasExplicitas_LasLlevaAlBordeYAlRecorte()
    {
        CornerRadius radio = default;
        double radioDelRecorte = 0;

        WpfTestHelper.Run(() =>
        {
            var grilla = new DataGrid
            {
                Style = (Style)System.Windows.Application.Current.FindResource("DataGridListadoStyle"),
                AutoGenerateColumns = false
            };
            EsquinasHelper.SetEsquinas(grilla, new CornerRadius(7, 7, 0, 0));
            var contenedor = new Grid { Width = 400, Height = 200 };
            contenedor.Children.Add(grilla);
            Distribuir(contenedor);

            radio = Descendientes<Border>(grilla).First().CornerRadius;
            radioDelRecorte = EsquinasHelper.GetRadioDelRecorte(Descendientes<ScrollViewer>(grilla).First());
        });

        radio.Should().Be(new CornerRadius(7, 7, 0, 0));
        radioDelRecorte.Should().Be(7, "el recorte sigue la esquina superior izquierda");
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
}
