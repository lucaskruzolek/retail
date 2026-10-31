using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Clientes;
using Retail.App.ViewModels.Comun;
using Retail.App.ViewModels.Pedidos;
using Retail.App.ViewModels.Ventas;
using Retail.App.Views.Components;
using Retail.App.Views.Dev;
using Retail.App.Views.Dialogs;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.Snapshots;

/// <summary>
/// "Bucle de capturas": renderiza vistas seleccionadas a PNG en <c>artifacts/snapshots/Light</c> y <c>artifacts/snapshots/Dark</c> para revisar la
/// interfaz sin levantar la aplicación. Se ejecuta a pedido con <c>--filter "Category=Snapshot"</c>.
/// </summary>
[Trait("Category", "Snapshot")]
public class VistaSnapshotTests
{
    private const int TamanoMinimoPng = 1000;
    private const double AltoParteGaleria = 1600;

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void StyleGalleryView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        string[]? rutas = null;
        var (_, error) = Capturar(tema, carpeta =>
        {
            // Es una página larga con scroll: se captura por partes de ~1600 px de alto para que cada imagen sea legible.
            var generadas = SnapshotRenderer.GuardarPorPartes(new StyleGalleryView(), 1280, AltoParteGaleria, carpeta + "/StyleGalleryView");
            rutas = [.. generadas];
            return generadas[0];
        });

        error.Should().BeNull("la vista debe poder renderizarse a PNG sin excepciones");
        rutas.Should().NotBeNull().And.HaveCountGreaterThan(1, "la galería es más larga que un pantallazo");
        foreach (var ruta in rutas!)
        {
            VerificarCaptura(ruta, null);
        }
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void DialogoMuestraDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        // El modal de referencia de la galería: el patrón "una sola superficie" sin datos de negocio
        var (ruta, error) = Capturar(tema, carpeta =>
            SnapshotRenderer.Guardar(new DialogoMuestraDialog(), 520, 600, carpeta + "/DialogoMuestraDialog"));

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ArticulosView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var (vista, viewModel) = CrearArticulosView();
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/ArticulosView", () => viewModel.Articulos.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    private static (ArticulosView Vista, ArticulosViewModel ViewModel) CrearArticulosView()
    {
        var inventario = Substitute.For<IInventarioService>();
        inventario.ListarCategoriasAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestra.Categorias());
        inventario.ListarMarcasAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestra.Marcas());
        var articulos = DatosDeMuestra.ArticulosPagina();
        inventario.ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>())
            .Returns(new ArticulosPaginadosDto
            {
                Items = articulos,
                TotalRegistros = 128,
                TotalArticulos = 128,
                TotalAlertasStock = articulos.Count(a => a.StockBajo),
                PaginaActual = 1,
                TamanoPagina = 50
            });

        var rubros = Substitute.For<IRubroService>();
        rubros.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(DatosDeMuestra.Rubros());

        // Con un Gerente en sesión, para que también se dibujen la columna de selección y las acciones de pedir.
        var gerente = Substitute.For<ICurrentUserSession>();
        gerente.EsGerente.Returns(true);
        var viewModel = new ArticulosViewModel(
            inventario,
            Substitute.For<IArticuloDialogService>(),
            session: gerente,
            flujoPedir: new FlujoPedirArticulos(
                Substitute.For<IPedidoService>(),
                Substitute.For<IPedidoDialogService>(),
                gerente,
                Substitute.For<ILogger<FlujoPedirArticulos>>()),
            rubroService: rubros);

        // Un filtro de stock aplicado, para que la captura muestre el embudo resaltado en su cabecera
        viewModel.FiltroEstadoStock.EstaAbierto = true;
        viewModel.FiltroEstadoStock.Grupos[0].Opciones[1].Seleccionada = true;
        viewModel.FiltroEstadoStock.AplicarCommand.Execute(null);

        // Los datos llegan por el camino real: el manejador Loaded de la vista invoca la carga del ViewModel.
        return (new ArticulosView(viewModel), viewModel);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void FiltroColumnaPanel_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var filtro = new FiltroColumnaViewModel("Categoría");
            filtro.CargarOpciones(DatosDeMuestra.Categorias()
                .Select(c => new OpcionFiltroColumna(
                    c.IdCategoria,
                    c.NombreCategoria,
                    DatosDeMuestra.Rubros().Single(r => r.IdRubro == c.IdRubro).NombreRubro))
                .OrderBy(o => o.Grupo)
                .ThenBy(o => o.Nombre));
            filtro.Grupos[0].TodasSeleccionadas = true;

            // El panel vive en un Popup, que se mostraría en pantalla: se captura su tarjeta fuera del Popup
            var control = new FiltroColumna { Filtro = filtro };
            var tarjeta = (FrameworkElement)control.FindName("Tarjeta");
            ((Popup)control.FindName("Panel")).Child = null;
            tarjeta.DataContext = filtro;
            tarjeta.VerticalAlignment = VerticalAlignment.Top; // como en el Popup, que mide lo que su contenido
            return SnapshotRenderer.Guardar(tarjeta, 300, 520, carpeta + "/FiltroColumnaPanel");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ClientesView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var clientes = DatosDeMuestra.Clientes();
            var servicio = Substitute.For<IClienteService>();
            servicio.ListarClientesPaginadosAsync(Arg.Any<ConsultaClientesDto>(), Arg.Any<CancellationToken>())
                .Returns(new ClientesPaginadosDto
                {
                    Items = clientes,
                    TotalRegistros = clientes.Count,
                    TotalClientes = clientes.Count,
                    TotalClientesConDeuda = clientes.Count(c => c.TieneDeuda),
                    TotalDeudaCartera = clientes.Sum(c => c.SaldoCuentaCorriente),
                    PaginaActual = 1,
                    TamanoPagina = 50
                });

            var viewModel = new ClientesViewModel(servicio, Substitute.For<IClienteDialogService>());
            var vista = new ClientesView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/ClientesView", () => viewModel.Clientes.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PosView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var sesion = Substitute.For<ICurrentUserSession>();
            sesion.NombreCompleto.Returns("Administrador General");
            var viewModel = new PosViewModel(
                Substitute.For<IVentaService>(),
                Substitute.For<ICajaService>(),
                Substitute.For<IInventarioService>(),
                sesion,
                Substitute.For<IVentaDialogService>());

            // Dos líneas de venta, para que la grilla del mostrador dibuje filas, cantidades y botones de eliminar.
            viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
            {
                IdArticulo = 1,
                CodigoBarras = "7790001000011",
                Descripcion = "Hoja A4 Autor 75 g x 500",
                PrecioVenta = 9800m,
                StockActual = 120,
                EsServicio = false
            });
            viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
            {
                IdArticulo = 2,
                CodigoBarras = "7790001000028",
                Descripcion = "Lápiz negro HB Faber",
                PrecioVenta = 450m,
                StockActual = 300,
                EsServicio = false
            });

            var vista = new PosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/PosView");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ArticuloFormDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new ArticuloFormViewModel();
            viewModel.ConfigurarAlta(DatosDeMuestra.Categorias(), DatosDeMuestra.Marcas());

            // El XAML declara Width="960" y SizeToContent="Height": el renderer respeta ese alto y el 750 no se usa.
            var dialogo = new ArticuloFormDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 960, 750, carpeta + "/ArticuloFormDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void MainWindow_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            // Con un Gerente en sesión se dibujan todos los módulos del sidebar.
            var gerente = Substitute.For<ICurrentUserSession>();
            gerente.EstaAutenticado.Returns(true);
            gerente.NombreCompleto.Returns("Lucas Kruzolek");
            gerente.Rol.Returns(RolUsuarioEnum.Gerente);
            gerente.EsGerente.Returns(true);

            var navegacion = Substitute.For<INavigationService>();
            var viewModel = new MainViewModel(gerente, navegacion, Substitute.For<IServiceScopeFactory>());

            // El frame de contenido queda vacío: el servicio de navegación es un doble y no carga páginas.
            var ventana = new MainWindow(viewModel, navegacion, Substitute.For<IServiceProvider>());
            return SnapshotRenderer.Guardar(ventana, 1366, 768, carpeta + "/MainWindow");
        });

        VerificarCaptura(ruta, error);
    }

    /// <summary>
    /// Aplica el tema pedido, ejecuta la captura en el hilo STA y SIEMPRE restaura el tema claro: la
    /// <c>Application</c> se comparte con el resto de la suite.
    /// </summary>
    private static (string? Ruta, Exception? Error) Capturar(TemaPrueba tema, Func<string, string> accion)
    {
        string? ruta = null;
        Exception? error = null;
        var carpeta = tema == TemaPrueba.Oscuro ? "Dark" : "Light";

        WpfTestHelper.Run(() =>
        {
            try
            {
                WpfTestHelper.AplicarTema(tema);
                ruta = accion(carpeta);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                WpfTestHelper.AplicarTema(TemaPrueba.Claro);
            }
        });

        return (ruta, error);
    }

    private static void VerificarCaptura(string? ruta, Exception? error)
    {
        error.Should().BeNull("la vista debe poder renderizarse a PNG sin excepciones");
        ruta.Should().NotBeNull();
        File.Exists(ruta).Should().BeTrue();
        new FileInfo(ruta!).Length.Should().BeGreaterThan(TamanoMinimoPng);
    }
}
