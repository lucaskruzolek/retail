using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Pedidos;
using Retail.App.ViewModels.Proveedores;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Un control editable dentro de una celda de <see cref="DataGrid"/> queda en el BindingGroup de la fila: sin
/// <c>UpdateSourceTrigger</c> explícito, el valor no llega al ViewModel hasta que la fila confirma la edición, cosa que
/// nunca pasa en una grilla de solo lectura. Las pruebas de ViewModel no lo detectan porque asignan las propiedades
/// sin pasar por el binding: estas eligen la opción en el combo de la celda, como lo hace el usuario.
/// </summary>
public class EdicionEnCeldasDeGrillaTests
{
    [Fact]
    public void ComboCategoria_IncorporarArticulosModal_ElegirOtraOpcion_LlegaAlItem()
    {
        // Arrange
        object? elegida = null;
        object? enElItem = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new IncorporarArticulosModalViewModel(
                Substitute.For<IClasificacionService>(),
                Substitute.For<ITerminoClasificacionService>(),
                Substitute.For<IEquivalenciaClasificacionService>(),
                Substitute.For<IRubroService>(),
                Substitute.For<ICurrentUserSession>(),
                Substitute.For<ILogger<IncorporarArticulosModalViewModel>>());
            viewModel.Inicializar(
                new[]
                {
                    new CategoriaDto { IdCategoria = 1, NombreCategoria = "Cuadernos" },
                    new CategoriaDto { IdCategoria = 2, NombreCategoria = "Escritura" }
                },
                new[] { new MarcaDto { IdMarca = 1, NombreMarca = "Bic" } },
                new[]
                {
                    new CatalogoProveedorDto
                    {
                        Id = 1,
                        IdProveedor = 3,
                        CodigoProveedor = "BIC-01",
                        DescripcionProveedor = "Bolígrafo Azul",
                        CostoReposicion = 1000m
                    }
                });
            var dialog = new IncorporarArticulosModalDialog { DataContext = viewModel };
            var raiz = Montar(dialog, 1040, 680);
            var combo = BuscarCombo(raiz, "GrillaConSugerencias", viewModel.OpcionesCategoria);
            elegida = viewModel.OpcionesCategoria.Single(o => o.Id == 2);

            // Act
            combo.SelectedItem = elegida;
            enElItem = viewModel.Items[0].CategoriaSeleccionada;
        });

        // Assert
        enElItem.Should().Be(elegida, "la categoría elegida en la celda debe llegar al ViewModel de la fila");
    }

    [Fact]
    public void ComboCategoria_ReglasPropuestas_ElegirOtraOpcion_LlegaALaPropuesta()
    {
        // Arrange
        object? elegida = null;
        object? enLaPropuesta = null;

        WpfTestHelper.Run(() =>
        {
            var terminoService = Substitute.For<ITerminoClasificacionService>();
            terminoService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<TerminoDto>>([]));
            terminoService.ListarParesAsync(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ParDto>>([]));
            terminoService.ListarParesPropuestosAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ParPropuestoDto>>([]));
            var categoriaService = Substitute.For<ICategoriaService>();
            categoriaService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<CategoriaDto>>([]));
            var viewModel = new AdministrarClasificacionViewModel(
                Substitute.For<IRubroService>(),
                categoriaService,
                Substitute.For<IMarcaService>(),
                terminoService,
                Substitute.For<IClasificacionDialogService>(),
                Substitute.For<ICurrentUserSession>());
            var dialog = new AdministrarClasificacionDialog { DataContext = viewModel };

            // Primero la sección, el término ambiguo y la pestaña (sus cargas vacían las listas) y después la fila a
            // editar (M-71: los pares propuestos están dentro del término).
            var ambiguo = new TerminoDto { IdTermino = 1, Termino = "CUADERNO" };
            viewModel.MostrandoTerminos = true;
            viewModel.Terminos.Terminos.Add(ambiguo);
            viewModel.Terminos.TerminoSeleccionado = ambiguo;
            viewModel.Terminos.MostrandoPropuestos = true;

            var propuesta = new CategoriaDto { IdCategoria = 10, NombreCategoria = "Cuadernos", IdRubro = 1 };
            var otra = new CategoriaDto { IdCategoria = 11, NombreCategoria = "Carpetas", IdRubro = 1 };
            viewModel.Terminos.Propuestos.Categorias.Add(propuesta);
            viewModel.Terminos.Propuestos.Categorias.Add(otra);
            viewModel.Terminos.Propuestos.Propuestas.Add(new ParPropuestoItemViewModel(
                new ParPropuestoDto
                {
                    IdTermino = 1,
                    Termino = "CUADERNO",
                    SegundaPalabra = "TAPA",
                    IdCategoria = 10,
                    NombreCategoria = "Cuadernos",
                    Coincidencias = 8,
                    Total = 8,
                    Ejemplos = ["CUADERNO TAPA DURA 48H"]
                },
                propuesta));
            var raiz = Montar(dialog, 960, 640);
            var combo = BuscarCombo(raiz, grilla: null, viewModel.Terminos.Propuestos.Categorias);
            elegida = otra;

            // Act
            combo.SelectedItem = otra;
            enLaPropuesta = viewModel.Terminos.Propuestos.Propuestas[0].CategoriaElegida;
        });

        // Assert
        enLaPropuesta.Should().Be(elegida, "la categoría elegida en la celda debe llegar a la propuesta");
    }

    [Fact]
    public void ComboFuente_PedirArticulos_ElegirOtraFuente_MueveElRenglonAlGrupoDeSuProveedor()
    {
        // Arrange (Track 4.3, A-18, K3): el artículo se pide a Alfa y también se compra a Beta
        int? fuenteEnLaFila = null;
        string[] grupos = [];
        Exception? error = null;

        WpfTestHelper.Run(() =>
        {
            var alfa = new FuenteRenglonDto { IdCatalogoProveedor = 10, IdProveedor = 1, RazonSocialProveedor = "Alfa", CostoActual = 100m, EsPrincipal = true };
            var beta = new FuenteRenglonDto { IdCatalogoProveedor = 30, IdProveedor = 3, RazonSocialProveedor = "Beta", CostoActual = 80m };
            var viewModel = new PedirArticulosViewModel(new VistaPreviaPedidoDto
            {
                Destinos = new[]
                {
                    new DestinoPedidoDto { IdProveedor = 1, RazonSocialProveedor = "Alfa" },
                    new DestinoPedidoDto { IdProveedor = 3, RazonSocialProveedor = "Beta" }
                },
                Renglones = new[]
                {
                    new RenglonVistaPreviaDto { IdCatalogoProveedor = 10, IdProveedor = 1, CostoActual = 100m, Fuentes = [beta, alfa] }
                }
            });
            var fila = viewModel.Grupos[0].Renglones[0];
            var raiz = Montar(new PedirArticulosDialog(viewModel), 580, 660);
            var combo = Descendientes<ComboBox>(raiz).First(c => ReferenceEquals(c.ItemsSource, fila.Opciones));

            // Act: como un clic real (ComboBoxItem.OnMouseLeftButtonUp → NotifyComboBoxItemMouseUp). Asignar
            // SelectedItem por código no pasa por SelectionChanger.End() y no detectaba la reentrancia: mover el renglón
            // destruía el combo mientras este terminaba de procesar el clic.
            try
            {
                var opcionBeta = fila.Opciones.Single(o => o.Fuente.IdCatalogoProveedor == 30);
                var item = AbrirYObtenerOpcion(combo, opcionBeta);
                item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonUpEvent
                });
                ProcesarPendientesDelDispatcher();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            fuenteEnLaFila = fila.Fuente.IdCatalogoProveedor;
            grupos = viewModel.Grupos.Select(g => g.RazonSocialProveedor).ToArray();
        });

        // Assert
        error.Should().BeNull("elegir otra fuente con el mouse no debe romper el combo");
        fuenteEnLaFila.Should().Be(30, "la fuente elegida en el combo debe llegar al renglón");
        grupos.Should().Equal("Beta");
    }

    [Fact]
    public void VincularArticulo_AlAbrir_LaGrillaMuestraLosResultadosYLaCasillaLlegaAlViewModel()
    {
        // Arrange: la grilla y el buscador enlazaban propiedades que el ViewModel no tiene, y se vinculaba un
        // artículo que el usuario nunca veía (corregido en Track 4.3, L3b).
        int filasEnLaGrilla = 0;
        string? textoDelBuscador = null;
        bool comoPrincipal = false;

        WpfTestHelper.Run(() =>
        {
            var inventario = Substitute.For<IInventarioService>();
            inventario.BuscarArticulosParaVentaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<ArticuloVentaDto>>(
                [
                    new ArticuloVentaDto { IdArticulo = 1, Descripcion = "Cuaderno A4", PrecioVenta = 1500m, StockActual = 3, EsServicio = false },
                    new ArticuloVentaDto { IdArticulo = 2, Descripcion = "Cuaderno A5", PrecioVenta = 1200m, StockActual = 5, EsServicio = false }
                ]));
            var viewModel = new VincularArticuloModalViewModel(inventario);
            viewModel.InicializarAsync(new CatalogoProveedorDto
            {
                Id = 30,
                IdProveedor = 3,
                CodigoProveedor = "MN-A4",
                DescripcionProveedor = "Cuaderno A4 (Norte)",
                CostoReposicion = 900m
            }).GetAwaiter().GetResult();
            viewModel.TextoBusqueda = "Cuaderno";
            viewModel.BuscarArticulosAsync().GetAwaiter().GetResult();
            var raiz = Montar(new VincularArticuloModalDialog { DataContext = viewModel }, 580, 600);

            // Act
            Descendientes<CheckBox>(raiz).Single().IsChecked = true;
            filasEnLaGrilla = Descendientes<DataGrid>(raiz).Single().Items.Count;
            textoDelBuscador = Descendientes<TextBox>(raiz).First().Text;
            comoPrincipal = viewModel.ComoPrincipal;
        });

        // Assert
        filasEnLaGrilla.Should().Be(2, "el usuario tiene que ver entre qué artículos elige");
        textoDelBuscador.Should().Be("Cuaderno");
        comoPrincipal.Should().BeTrue();
    }

    [Fact]
    public void FuentesDelFormulario_ClicEnLaPrincipalYEnQuitar_LleganAlViewModelSinRomperLaLista()
    {
        // Arrange (Track 4.3, A-19): Sur (1000) es la principal y Norte (950) una adicional
        int? principal = null;
        decimal costo = 0m;
        bool radioDeSurMarcado = true;
        int[] fuentesRestantes = [];
        Exception? error = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new ArticuloFormViewModel();
            viewModel.ConfigurarEdicion(
                new ArticuloDto { IdArticulo = 7, Descripcion = "Marcador", CostoReposicion = 1000m, PorcentajeGanancia = 40m, PrecioVenta = 1400m, StockActual = 1, StockMinimo = 1, EsServicio = false },
                new[]
                {
                    new FuenteArticuloDto { IdCatalogoProveedor = 25, RazonSocialProveedor = "Distribuidora Sur", CostoActual = 1000m, FechaActualizacion = DateTime.UtcNow, EsPrincipal = true },
                    new FuenteArticuloDto { IdCatalogoProveedor = 26, RazonSocialProveedor = "Mayorista Norte", CostoActual = 950m, FechaActualizacion = DateTime.UtcNow }
                },
                Array.Empty<CategoriaDto>(),
                Array.Empty<MarcaDto>());
            var raiz = Montar(new ArticuloFormDialog(viewModel), 960, 660);
            // El selector Producto/Servicio también son RadioButton: solo interesan los renglones de fuentes
            var radios = Descendientes<RadioButton>(raiz).Where(r => r.DataContext is FuenteFormularioFila).ToList();
            var radioNorte = radios.Single(r => ((FuenteFormularioFila)r.DataContext).IdCatalogoProveedor == 26);
            var radioSur = radios.Single(r => ((FuenteFormularioFila)r.DataContext).IdCatalogoProveedor == 25);

            // Act: OnClick es lo que ejecuta el botón al soltar el mouse (marca el radio y luego ejecuta el comando).
            // Quitar destruye el contenedor del botón mientras termina de procesar su propio clic.
            try
            {
                Clic(radioNorte);
                ProcesarPendientesDelDispatcher();
                principal = viewModel.IdCatalogoProveedor;
                costo = viewModel.CostoReposicion;
                radioDeSurMarcado = radioSur.IsChecked == true;

                var quitarSur = Descendientes<Button>(raiz)
                    .Single(b => b.DataContext is FuenteFormularioFila { IdCatalogoProveedor: 25 } && b.Command == viewModel.QuitarFuenteCommand);
                Clic(quitarSur);
                ProcesarPendientesDelDispatcher();
                raiz.UpdateLayout();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            fuentesRestantes = viewModel.Fuentes.Select(f => f.IdCatalogoProveedor).ToArray();
        });

        // Assert
        error.Should().BeNull("elegir la principal y quitar una fuente con el mouse no debe romper la lista");
        principal.Should().Be(26);
        costo.Should().Be(950m, "la principal muestra su costo como vista previa (M2)");
        radioDeSurMarcado.Should().BeFalse("el radio de la principal anterior sigue al ViewModel");
        fuentesRestantes.Should().Equal(26);
    }

    [Fact]
    public void SelectorTipoArticulo_EdicionDeUnProducto_MarcaProductoYAlElegirServicioActualizaElViewModel()
    {
        // Arrange: el selector segmentado Producto / Servicio enlaza sus dos RadioButton a EsServicio
        bool? productoAlAbrir = null;
        bool? servicioAlAbrir = null;
        bool? productoTrasElegirServicio = null;
        var esServicio = false;
        Exception? error = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new ArticuloFormViewModel();
            viewModel.ConfigurarEdicion(
                new ArticuloDto { IdArticulo = 7, Descripcion = "Marcador", CostoReposicion = 1000m, PorcentajeGanancia = 40m, PrecioVenta = 1400m, StockActual = 1, StockMinimo = 1, EsServicio = false },
                Array.Empty<FuenteArticuloDto>(),
                Array.Empty<CategoriaDto>(),
                Array.Empty<MarcaDto>());
            var raiz = Montar(new ArticuloFormDialog(viewModel), 960, 660);
            var producto = Descendientes<RadioButton>(raiz).Single(r => r.Name == "OpcionProducto");
            var servicio = Descendientes<RadioButton>(raiz).Single(r => r.Name == "OpcionServicio");

            try
            {
                productoAlAbrir = producto.IsChecked;
                servicioAlAbrir = servicio.IsChecked;

                // Act
                Clic(servicio);
                ProcesarPendientesDelDispatcher();
                productoTrasElegirServicio = producto.IsChecked;
                esServicio = viewModel.EsServicio;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        // Assert
        error.Should().BeNull();
        productoAlAbrir.Should().BeTrue("un artículo que no es servicio abre con Producto elegido");
        servicioAlAbrir.Should().BeFalse();
        esServicio.Should().BeTrue("elegir Servicio con el mouse llega al ViewModel");
        productoTrasElegirServicio.Should().BeFalse();
    }

    /// <summary>
    /// El clic de un <see cref="ButtonBase"/>, tal como lo dispara al soltar el mouse: alterna el estado (si es un
    /// <see cref="ToggleButton"/>), levanta Click y ejecuta su comando.
    /// </summary>
    private static void Clic(ButtonBase boton)
    {
        typeof(ButtonBase)
            .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(boton, null);
    }

    /// <summary>
    /// Monta el contenido de la ventana en un elemento suelto, con los mismos recursos y DataContext, y lo mide: una
    /// ventana sin mostrar no arma el árbol visual de su contenido, y mostrarla procesa mensajes que en el proceso de
    /// pruebas fallan (WPF-UI carga un recurso diferido por URI pack).
    /// </summary>
    private static Border Montar(Window ventana, double ancho, double alto)
    {
        var contenido = (UIElement)ventana.Content;
        ventana.Content = null;
        var raiz = new Border
        {
            Child = contenido,
            DataContext = ventana.DataContext,
            Resources = ventana.Resources
        };
        raiz.Measure(new Size(ancho, alto));
        raiz.Arrange(new Rect(0, 0, ancho, alto));
        raiz.UpdateLayout();
        return raiz;
    }

    /// <summary>
    /// Primer combo dentro de una celda de grilla cuyo origen de opciones es <paramref name="opciones"/>.
    /// </summary>
    private static ComboBox BuscarCombo(DependencyObject raiz, string? grilla, object opciones)
    {
        var grillas = Descendientes<DataGrid>(raiz).Where(g => grilla is null || g.Name == grilla);
        return grillas
            .SelectMany(g => Descendientes<ComboBox>(g))
            .First(c => ReferenceEquals(c.ItemsSource, opciones));
    }

    /// <summary>
    /// Abre el desplegable y devuelve el contenedor de la opción. Sin ventana real el Popup no se muestra y WPF no genera
    /// los contenedores, así que se mide su contenido a mano: es lo que haría al mostrarlo.
    /// </summary>
    private static ComboBoxItem AbrirYObtenerOpcion(ComboBox combo, object opcion)
    {
        combo.ApplyTemplate();
        combo.IsDropDownOpen = true;
        var popup = Descendientes<Popup>(combo).First();
        popup.Child.Measure(new Size(400, 400));
        popup.Child.Arrange(new Rect(0, 0, 400, 400));
        popup.Child.UpdateLayout();
        return (ComboBoxItem)combo.ItemContainerGenerator.ContainerFromItem(opcion);
    }

    /// <summary>
    /// Ejecuta lo que la vista dejó encolado en el Dispatcher (por ejemplo, un binding diferido), como pasaría al
    /// terminar el evento en la aplicación.
    /// </summary>
    private static void ProcesarPendientesDelDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
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
