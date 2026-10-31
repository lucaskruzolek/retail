using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.Views.Components;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Interacción real con la grilla de Artículos: el doble clic en una fila abre la edición de ESA fila y el botón "+n"
/// de fuentes adicionales abre la edición de su artículo (Track 4.3, A-19). Las pruebas de ViewModel no ven el
/// <c>EventSetter</c> ni los bindings de la plantilla, por eso se monta la vista y se levantan los eventos.
/// </summary>
public class ArticulosViewInteraccionTests
{
    private sealed class Escenario
    {
        public required IArticuloDialogService Dialogos { get; init; }

        public required ArticulosView Vista { get; init; }

        public required ArticuloDto Primero { get; init; }

        public required ArticuloDto Segundo { get; init; }
    }

    private static Escenario Crear()
    {
        var inventario = Substitute.For<IInventarioService>();
        inventario.ListarCategoriasAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CategoriaDto>>([new() { IdCategoria = 1, NombreCategoria = "Escolar" }]));
        inventario.ListarMarcasAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<MarcaDto>>([new() { IdMarca = 1, NombreMarca = "Bic" }]));
        inventario.ObtenerFuentesDeArticuloAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FuenteArticuloDto>>([]));
        var dialogos = Substitute.For<IArticuloDialogService>();
        var viewModel = new ArticulosViewModel(inventario, dialogos);

        var primero = new ArticuloDto { IdArticulo = 1, Descripcion = "Cuaderno A4", CostoReposicion = 100m, PorcentajeGanancia = 50m, PrecioVenta = 150m, StockActual = 5, StockMinimo = 1, EsServicio = false };
        var segundo = new ArticuloDto
        {
            IdArticulo = 2,
            Descripcion = "Lapicera Azul",
            CostoReposicion = 10m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 15m,
            StockActual = 5,
            StockMinimo = 1,
            EsServicio = false,
            IdCatalogoProveedor = 40,
            ProveedorNombre = "Distribuidora Sur",
            CantidadFuentes = 3
        };
        viewModel.Articulos.Add(new ArticuloFila(primero));
        viewModel.Articulos.Add(new ArticuloFila(segundo));

        var vista = new ArticulosView(viewModel);
        vista.Measure(new Size(1280, 800));
        vista.Arrange(new Rect(0, 0, 1280, 800));
        vista.UpdateLayout();
        return new Escenario { Dialogos = dialogos, Vista = vista, Primero = primero, Segundo = segundo };
    }

    /// <summary>
    /// MouseDoubleClick es un evento directo: WPF lo levanta sobre la fila (el control) con el elemento pulsado como
    /// origen. Se reproduce igual: se levanta en la fila y <paramref name="origen"/> queda como OriginalSource.
    /// </summary>
    private static void DobleClic(UIElement fila, UIElement origen)
    {
        fila.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = Control.MouseDoubleClickEvent,
            Source = origen
        });
    }

    private static int LlamadasAlDialogo(Escenario e, ArticuloDto articulo)
    {
        return e.Dialogos.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(IArticuloDialogService.MostrarDialogoModificar)
                && ReferenceEquals(c.GetArguments()[0], articulo));
    }

    [Fact]
    public void DobleClic_SobreUnaCeldaDeTextoDeLaSegundaFila_AbreLaEdicionDeEsaFila()
    {
        int sobreSegundo = 0;
        int sobrePrimero = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var segundaFila = Descendientes<DataGridRow>(e.Vista).Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Segundo));
            var texto = Descendientes<TextBlock>(segundaFila).First(t => t.Text == "LAPICERA AZUL");

            DobleClic(segundaFila, texto);

            sobreSegundo = LlamadasAlDialogo(e, e.Segundo);
            sobrePrimero = LlamadasAlDialogo(e, e.Primero);
        });

        sobreSegundo.Should().Be(1, "el doble clic abre el artículo de la fila pulsada");
        sobrePrimero.Should().Be(0, "no el artículo seleccionado antes ni otra fila");
    }

    [Fact]
    public void DobleClic_SobreUnBotonDeLaFila_NoAbreLaEdicion()
    {
        int llamadas = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var segundaFila = Descendientes<DataGridRow>(e.Vista).Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Segundo));
            var boton = Descendientes<ButtonBase>(segundaFila).First();

            DobleClic(segundaFila, boton);

            llamadas = LlamadasAlDialogo(e, e.Segundo) + LlamadasAlDialogo(e, e.Primero);
        });

        llamadas.Should().Be(0, "un botón de la fila hace lo suyo y el doble clic no abre la edición");
    }

    [Fact]
    public void ChipFuentesAdicionales_ArticuloConAdicionales_InformaSinSerUnBoton()
    {
        string? texto = null;
        Visibility chipSegundo = Visibility.Collapsed;
        Visibility chipPrimero = Visibility.Visible;
        bool dentroDeUnBoton = true;
        int llamadasAlHacerDobleClicSobreElChip = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var filas = Descendientes<DataGridRow>(e.Vista).ToList();
            var segunda = filas.Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Segundo));
            var primera = filas.Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Primero));
            var chip = Descendientes<Border>(segunda).Single(b => b.Name == "ChipFuentesAdicionales");
            chipSegundo = chip.Visibility;
            chipPrimero = Descendientes<Border>(primera).Single(b => b.Name == "ChipFuentesAdicionales").Visibility;
            var textoDelChip = Descendientes<TextBlock>(chip).Single();
            texto = textoDelChip.Text;
            dentroDeUnBoton = Descendientes<Button>(segunda).Any(b => Descendientes<TextBlock>(b).Contains(textoDelChip));

            // Al no ser un botón, el doble clic sobre el chip abre la edición como en cualquier otra parte de la fila
            DobleClic(segunda, textoDelChip);
            llamadasAlHacerDobleClicSobreElChip = LlamadasAlDialogo(e, e.Segundo);
        });

        texto.Should().Be("+2");
        chipSegundo.Should().Be(Visibility.Visible);
        chipPrimero.Should().Be(Visibility.Collapsed, "sin fuentes adicionales no hay chip");
        dentroDeUnBoton.Should().BeFalse("el \"+n\" solo informa");
        llamadasAlHacerDobleClicSobreElChip.Should().Be(1);
    }

    [Fact]
    public void Grilla_NombreDelArticulo_SeMuestraEnMayusculasSinCambiarElDato()
    {
        string[] textos = [];

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var primeraFila = Descendientes<DataGridRow>(e.Vista).Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Primero));
            textos = Descendientes<TextBlock>(primeraFila).Select(t => t.Text).ToArray();
        });

        textos.Should().Contain("CUADERNO A4");
        textos.Should().NotContain("Cuaderno A4");
    }

    [Fact]
    public void ColumnaDeAcciones_MideLoQueElBotonMasElAire_YLaDescripcionAbsorbeElResto()
    {
        double accion = 0;
        double descripcion = 0;
        double fijas = 0;
        double total = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { }));
            e.Vista.Measure(new Size(1280, 800));
            e.Vista.Arrange(new Rect(0, 0, 1280, 800));
            e.Vista.UpdateLayout();

            var grilla = Descendientes<DataGrid>(e.Vista).Single();
            accion = grilla.Columns[^1].ActualWidth;
            descripcion = grilla.Columns.Single(c => (c.Header as string) == "Artículo").ActualWidth;
            fijas = grilla.Columns.Where(c => (c.Header as string) != "Artículo" && c.Visibility == Visibility.Visible).Sum(c => c.ActualWidth);
            total = grilla.ActualWidth;
        });

        accion.Should().Be(36, "el botón de 28 más 8 de aire hasta el borde; los 8 del otro lado los pone el relleno de Stock");
        (descripcion + fijas).Should().BeApproximately(total, 3, "la columna * ocupa todo lo que no usan las fijas (menos el borde de la grilla)");
        descripcion.Should().BeGreaterThan(200);
    }

    [Fact]
    public void BotonDeAcciones_TieneElMismoAireHaciaElStockQueHaciaElBordeDeLaGrilla()
    {
        double aireIzquierdo = 0;
        double aireDerecho = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var fila = Descendientes<DataGridRow>(e.Vista).Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Primero));
            var stock = Descendientes<StackPanel>(fila).Single(p => p.Name == "PanelStock");
            var boton = Descendientes<ToggleButton>(fila).Single(b => b.ContextMenu is not null);
            var celdaDelBoton = Descendientes<DataGridCell>(fila).Single(c => Descendientes<ToggleButton>(c).Contains(boton));

            double finDelStock = stock.TranslatePoint(new Point(stock.ActualWidth, 0), fila).X;
            double inicioDelBoton = boton.TranslatePoint(new Point(0, 0), fila).X;
            double finDelBoton = boton.TranslatePoint(new Point(boton.ActualWidth, 0), fila).X;
            double finDeLaCelda = celdaDelBoton.TranslatePoint(new Point(celdaDelBoton.ActualWidth, 0), fila).X;
            aireIzquierdo = inicioDelBoton - finDelStock;
            aireDerecho = finDeLaCelda - finDelBoton;
        });

        aireIzquierdo.Should().BeApproximately(8, 0.5, "el stock va a la derecha de su columna, con 8 de relleno");
        aireDerecho.Should().BeApproximately(8, 0.5, "el botón de 28 va a la izquierda de su columna de 36: el mismo aire que hacia el stock");
    }

    [Fact]
    public void Grilla_ConPocasFilas_OcupaTodoElAltoHastaLaPaginacion()
    {
        double altoDeLaGrilla = 0;
        double altoDeLasFilas = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var grilla = Descendientes<DataGrid>(e.Vista).Single();
            altoDeLaGrilla = grilla.ActualHeight;
            altoDeLasFilas = Descendientes<DataGridRow>(grilla).Sum(f => f.ActualHeight);
        });

        altoDeLaGrilla.Should().BeGreaterThan(altoDeLasFilas + 200, "con dos filas, el borde de la grilla igual llega hasta la paginación");
    }

    [Fact]
    public void Grilla_EsquinasRedondeadas_ElBordeYElRecorteSeAplican()
    {
        CornerRadius radio = default;
        bool recortada = false;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var grilla = Descendientes<DataGrid>(e.Vista).Single();
            radio = Descendientes<Border>(grilla).First().CornerRadius;
            recortada = Descendientes<ScrollViewer>(grilla).First().Clip is not null;
        });

        radio.Should().Be(new CornerRadius(7, 7, 0, 0), "arriba sigue la superficie de la tabla (radio 8 menos su borde); abajo continúa la paginación");
        recortada.Should().BeTrue("sin recorte, el fondo del encabezado se saldría de las esquinas");
    }

    [Fact]
    public void Grilla_ImportesDeLaFila_SeMuestranEnEsArConSimboloDePesos()
    {
        string[] textos = [];

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var primeraFila = Descendientes<DataGridRow>(e.Vista).Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Primero));
            textos = Descendientes<TextBlock>(primeraFila).Select(t => t.Text).ToArray();
        });

        var es = System.Globalization.CultureInfo.GetCultureInfo("es-AR");
        textos.Should().Contain(100m.ToString("C2", es), "el costo se formatea como moneda es-AR");
        textos.Should().Contain("50%", "el margen va con su unidad y sin ceros sobrantes");
    }

    [Fact]
    public void AccionesMasivas_AlMarcarFilas_ReemplazanAlContadorEnLaBarraDelBuscador()
    {
        bool masivasSinMarcas = true;
        bool contadorSinMarcas = false;
        bool masivasConMarcas = false;
        bool contadorConMarcas = true;
        bool busquedaConMarcas = false;
        bool limpiarConMarcas = false;
        bool heredaElViewModel = false;
        bool pedirEnlazado = false;
        bool paginacionSinResumen = false;
        string? texto = null;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var barra = Descendientes<ListToolbar>(e.Vista).Single();
            var masivas = (BulkActionBar)barra.AccionesDeSeleccion!;
            var panelNormal = (FrameworkElement)barra.FindName("PanelNormal");
            var panelSeleccion = (FrameworkElement)barra.FindName("PanelSeleccion");
            // Fuera de una ventana IsVisible es siempre falso: se mira la Visibility de cada panel
            masivasSinMarcas = panelSeleccion.Visibility == Visibility.Visible;
            contadorSinMarcas = panelNormal.Visibility == Visibility.Visible;
            paginacionSinResumen = !Descendientes<PaginationBar>(e.Vista).Single().MostrarResumen;

            e.Vista.ViewModel.SeleccionarTodos = true;
            e.Vista.UpdateLayout();
            masivasConMarcas = panelSeleccion.Visibility == Visibility.Visible && Descendientes<BulkActionBar>(barra).Contains(masivas);
            contadorConMarcas = panelNormal.Visibility == Visibility.Visible;
            busquedaConMarcas = Descendientes<Wpf.Ui.Controls.TextBox>(barra).Single().Visibility == Visibility.Visible;
            var limpiar = Descendientes<Button>(barra).Single(b => ReferenceEquals(b.Command, e.Vista.ViewModel.LimpiarFiltrosCommand));
            limpiarConMarcas = limpiar.Visibility == Visibility.Visible && ((FrameworkElement)limpiar.Parent).Visibility == Visibility.Visible;
            heredaElViewModel = ReferenceEquals(masivas.DataContext, e.Vista.ViewModel);
            pedirEnlazado = Descendientes<Button>(masivas).Any(b => ReferenceEquals(b.Command, e.Vista.ViewModel.PedirSeleccionadosCommand));
            texto = masivas.TextoSeleccion;
        });

        masivasSinMarcas.Should().BeFalse();
        contadorSinMarcas.Should().BeTrue();
        masivasConMarcas.Should().BeTrue("aparecen junto a las casillas que las activan");
        contadorConMarcas.Should().BeFalse("las acciones masivas ocupan el lugar de la carga y el contador");
        busquedaConMarcas.Should().BeTrue("la búsqueda no se tapa al marcar filas");
        limpiarConMarcas.Should().BeFalse("el badge \"N seleccionados ✕\" ocupa el lugar de \"Limpiar filtros\"");
        heredaElViewModel.Should().BeTrue("la barra no tiene padre lógico y hereda el DataContext por el árbol visual");
        pedirEnlazado.Should().BeTrue();
        paginacionSinResumen.Should().BeTrue("el contador de arriba ya informa la cantidad");
        texto.Should().Be("2 seleccionados", "la selección es de la página visible: no se informa \"de M\"");
    }

    [Fact]
    public void AccionesMasivas_EliminarALaVistaYMenuMas_SinLogicaQuedanDeshabilitadasYConIcono()
    {
        string[] opciones = [];
        bool todasDeshabilitadas = false;
        string[] iconos = [];
        bool pesoNormal = false;
        bool eliminarDeshabilitado = false;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var masivas = (BulkActionBar)Descendientes<ListToolbar>(e.Vista).Single().AccionesDeSeleccion!;
            var botones = LogicalTreeHelper.GetChildren((StackPanel)masivas.Content).OfType<ButtonBase>().ToList();
            var mas = botones.Single(b => b.ContextMenu is not null);
            var items = mas.ContextMenu!.Items.OfType<MenuItem>().ToList();
            opciones = items.Select(i => (string)i.Header).ToArray();
            todasDeshabilitadas = items.All(i => !i.IsEnabled);
            iconos = items.Select(i => ((MahApps.Metro.IconPacks.PackIconBoxIcons)i.Icon).Kind.ToString()).ToArray();
            pesoNormal = items.All(i => i.FontWeight == FontWeights.Normal);
            eliminarDeshabilitado = botones.Single(b => b.ToolTip is string t && t.Contains("dar de baja")).IsEnabled == false;
        });

        opciones.Should().Equal("Cambiar rubro", "Cambiar categoría");
        todasDeshabilitadas.Should().BeTrue("todavía no tienen lógica: se ven con su tooltip pero no se ejecutan");
        iconos.Should().Equal("RegularBookmarks", "RegularTag");
        pesoNormal.Should().BeTrue("los textos del desplegable van sin negrita");
        eliminarDeshabilitado.Should().BeTrue("\"Eliminar seleccionados\" queda a la vista, sin lógica todavía");
    }

    [Fact]
    public void Grilla_TinteDeSeleccion_SigueALaCasillaYNoALaFilaActual()
    {
        Color marcada = default;
        Color actualSinMarcar = default;
        Color tinte = default;
        Color fondo = default;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var grilla = Descendientes<DataGrid>(e.Vista).Single();
            var filas = Descendientes<DataGridRow>(e.Vista).ToList();
            var primera = filas.Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Primero));
            var segunda = filas.Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Segundo));

            ((ArticuloFila)primera.Item).EstaSeleccionado = true;
            grilla.SelectedItem = segunda.Item;
            e.Vista.UpdateLayout();

            marcada = ((SolidColorBrush)primera.Background).Color;
            actualSinMarcar = ((SolidColorBrush)segunda.Background).Color;
            tinte = ((SolidColorBrush)e.Vista.FindResource("PrimaryLightBrush")).Color;
            fondo = ((SolidColorBrush)e.Vista.FindResource("SurfaceCardBrush")).Color;
        });

        marcada.Should().Be(tinte, "la fila marcada es parte de la selección de las acciones masivas");
        actualSinMarcar.Should().Be(fondo, "un clic en una fila no cambia su aspecto: el gris es solo del hover");
    }

    [Fact]
    public void MenuDeLaFila_EditarArticulo_AbreLaEdicionDeEsaFila()
    {
        string[] opciones = [];
        int llamadas = 0;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var segundaFila = Descendientes<DataGridRow>(e.Vista).Single(f => ReferenceEquals((f.Item as ArticuloFila)?.Articulo, e.Segundo));
            var botonMenu = Descendientes<ToggleButton>(segundaFila).Single(b => b.ContextMenu is not null);
            var menu = botonMenu.ContextMenu!;
            menu.PlacementTarget = botonMenu;

            opciones = menu.Items.OfType<MenuItem>().Where(i => i.Visibility == Visibility.Visible).Select(i => (string)i.Header).ToArray();
            var editar = menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Editar artículo");
            editar.Command.Execute(editar.CommandParameter);

            llamadas = LlamadasAlDialogo(e, e.Segundo);
        });

        opciones.Should().Contain(["Editar artículo", "Dar de baja", "Crear presentación"]);
        opciones.Should().NotContain("Fraccionar", "solo las presentaciones derivadas se fraccionan");
        llamadas.Should().Be(1);
    }

    [Fact]
    public void Cabeceras_FiltrosDeColumna_SeEnlazanALosFiltrosDelViewModel()
    {
        // Las columnas no están en el árbol visual: si el binding del Header fallara, el filtro quedaría nulo en silencio
        string?[] titulos = [];
        bool enlazados = false;

        WpfTestHelper.Run(() =>
        {
            var e = Crear();
            var vm = (ArticulosViewModel)e.Vista.DataContext;
            var filtros = Descendientes<FiltroColumna>(e.Vista).ToList();

            titulos = filtros.Select(f => f.Filtro?.Titulo).ToArray();
            enlazados = filtros.Select(f => f.Filtro).SequenceEqual(
                [vm.FiltroCategoria, vm.FiltroMarca, vm.FiltroDistribuidor, vm.FiltroEstadoStock]);
        });

        titulos.Should().Equal("Categoría", "Marca", "Distribuidor", "Stock");
        enlazados.Should().BeTrue();
    }

    [Fact]
    public void FiltroColumna_ClicSobreElFondoDelPanel_NoLlegaALaCabecera()
    {
        // Un clic que subiera hasta la DataGridColumnHeader ordenaría la columna o empezaría a arrastrarla
        bool atendido = false;

        WpfTestHelper.Run(() =>
        {
            var filtro = Descendientes<FiltroColumna>(Crear().Vista).First();
            var tarjeta = (Border)filtro.FindName("Tarjeta");
            var clic = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent
            };

            tarjeta.RaiseEvent(clic);
            atendido = clic.Handled;
        });

        atendido.Should().BeTrue();
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
