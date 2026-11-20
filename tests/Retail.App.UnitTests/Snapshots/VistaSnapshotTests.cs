using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Clientes;
using Retail.App.ViewModels.Compras;
using Retail.App.ViewModels.Comun;
using Retail.App.ViewModels.Listas;
using Retail.App.ViewModels.Pedidos;
using Retail.App.ViewModels.Presupuestos;
using Retail.App.ViewModels.Ventas;
using Retail.App.Views.Components;
using Retail.App.Views.Dev;
using Retail.App.Views.Dialogs;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Listas;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Presupuestos;
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
    [InlineData(TemaPrueba.Claro, true, true, "PosView")]
    [InlineData(TemaPrueba.Oscuro, true, true, "PosView")]
    [InlineData(TemaPrueba.Claro, false, false, "PosView_CajaCerrada")]
    [InlineData(TemaPrueba.Oscuro, false, false, "PosView_CajaCerrada")]
    [InlineData(TemaPrueba.Claro, true, false, "PosView_TicketVacio")]
    [InlineData(TemaPrueba.Oscuro, true, false, "PosView_TicketVacio")]
    public void PosView_PorTema_GeneraCaptura(TemaPrueba tema, bool cajaAbierta, bool conItems, string nombre)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var caja = Substitute.For<ICajaService>();
            caja.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(cajaAbierta
                ? new TurnoCajaDto
                {
                    IdTurno = 12,
                    IdUsuario = 1,
                    NombreUsuario = "admin",
                    FechaApertura = DateTime.UtcNow,
                    SaldoInicial = 5000m,
                    SaldoTeoricoEfectivo = 5000m,
                    Estado = EstadoTurnoEnum.Abierto
                }
                : null);

            var viewModel = new PosViewModel(
                Substitute.For<IVentaService>(),
                caja,
                Substitute.For<IInventarioService>(),
                DatosDeMuestraPaginas.Sesion(1, false),
                Substitute.For<IVentaDialogService>(),
                Substitute.For<ICajaDialogService>(),
                Substitute.For<IPresupuestoService>(),
                Substitute.For<IPresupuestoDialogService>(),
                Substitute.For<IClienteService>(),
                Substitute.For<IListaService>(),
                Substitute.For<IListaDialogService>());

            if (conItems)
            {
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
            }

            var vista = new PosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/" + nombre, () => viewModel.EstadoCajaVerificado);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PosView_TurnoDeOtroCajero_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        // La terminal tiene un turno sin cerrar de otro cajero: aviso con su nombre, sin "Abrir caja" y buscador bloqueado.
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var caja = Substitute.For<ICajaService>();
            caja.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.TurnoAjeno());

            var viewModel = new PosViewModel(
                Substitute.For<IVentaService>(),
                caja,
                Substitute.For<IInventarioService>(),
                DatosDeMuestraPaginas.Sesion(1, false),
                Substitute.For<IVentaDialogService>(),
                Substitute.For<ICajaDialogService>(),
                Substitute.For<IPresupuestoService>(),
                Substitute.For<IPresupuestoDialogService>(),
                Substitute.For<IClienteService>(),
                Substitute.For<IListaService>(),
                Substitute.For<IListaDialogService>());

            var vista = new PosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/PosView_TurnoAjeno", () => viewModel.EstadoCajaVerificado);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PosView_MantenimientoProgramado_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        // Un Gerente programó un mantenimiento para dentro de 25 minutos: franja ámbar sobre el mostrador con la venta en curso.
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var caja = Substitute.For<ICajaService>();
            caja.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(new TurnoCajaDto
            {
                IdTurno = 12,
                IdUsuario = 1,
                NombreUsuario = "admin",
                FechaApertura = DateTime.UtcNow,
                SaldoInicial = 5000m,
                SaldoTeoricoEfectivo = 5000m,
                Estado = EstadoTurnoEnum.Abierto
            });

            var mantenimiento = new EstadoMantenimiento();
            mantenimiento.Establecer(DateTime.Now.AddMinutes(25), "Actualización del sistema");

            var viewModel = new PosViewModel(
                Substitute.For<IVentaService>(),
                caja,
                Substitute.For<IInventarioService>(),
                DatosDeMuestraPaginas.Sesion(1, false),
                Substitute.For<IVentaDialogService>(),
                Substitute.For<ICajaDialogService>(),
                Substitute.For<IPresupuestoService>(),
                Substitute.For<IPresupuestoDialogService>(),
                Substitute.For<IClienteService>(),
                Substitute.For<IListaService>(),
                Substitute.For<IListaDialogService>(),
                estadoMantenimiento: mantenimiento);

            viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
            {
                IdArticulo = 1,
                CodigoBarras = "7790001000011",
                Descripcion = "Hoja A4 Autor 75 g x 500",
                PrecioVenta = 9800m,
                StockActual = 120,
                EsServicio = false
            });

            var vista = new PosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/PosView_Mantenimiento", () => viewModel.EstadoCajaVerificado);
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

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void MainWindow_MenuExpandido_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var gerente = Substitute.For<ICurrentUserSession>();
            gerente.EstaAutenticado.Returns(true);
            gerente.NombreCompleto.Returns("Lucas Kruzolek");
            gerente.Rol.Returns(RolUsuarioEnum.Gerente);
            gerente.EsGerente.Returns(true);

            var navegacion = Substitute.For<INavigationService>();
            var viewModel = new MainViewModel(gerente, navegacion, Substitute.For<IServiceScopeFactory>());
            viewModel.IrAPaginaCommand.Execute("clientes");
            viewModel.ToggleSidebarCommand.Execute(null);

            var ventana = new MainWindow(viewModel, navegacion, Substitute.For<IServiceProvider>());
            return SnapshotRenderer.Guardar(ventana, 1366, 768, carpeta + "/MainWindow_MenuExpandido");
        });

        VerificarCaptura(ruta, error);
    }

    /// <summary>
    /// Aplica el tema pedido, ejecuta la captura en el hilo STA y SIEMPRE restaura el tema claro: la
    /// <c>Application</c> se comparte con el resto de la suite.
    /// </summary>
    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void SeleccionarPedidoModalDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var pedidoService = Substitute.For<IPedidoService>();
            var viewModel = new SeleccionarPedidoModalViewModel(1, "Distribuidora Sur S.R.L.", pedidoService);
            viewModel.Pedidos.Add(new PedidoEmitidoDto
            {
                IdPedido = 101,
                IdProveedor = 1,
                RazonSocialProveedor = "Distribuidora Sur S.R.L.",
                FechaEmision = new DateTime(2026, 11, 1, 10, 30, 0, DateTimeKind.Utc),
                CantidadRenglones = 4,
                TotalEstimado = 12500.50m,
            });
            viewModel.Pedidos.Add(new PedidoEmitidoDto
            {
                IdPedido = 102,
                IdProveedor = 1,
                RazonSocialProveedor = "Distribuidora Sur S.R.L.",
                FechaEmision = new DateTime(2026, 10, 28, 15, 0, 0, DateTimeKind.Utc),
                CantidadRenglones = 2,
                TotalEstimado = 4800m,
            });

            var dialogo = new SeleccionarPedidoModalDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 540, 620, carpeta + "/SeleccionarPedidoModalDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void SeleccionarPedidoModalDialog_TodosLosProveedores_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            // Sin proveedor elegido en la compra: se listan los pedidos de todos y aparece la columna Proveedor
            var pedidoService = Substitute.For<IPedidoService>();
            var viewModel = new SeleccionarPedidoModalViewModel(null, null, pedidoService);
            viewModel.Pedidos.Add(new PedidoEmitidoDto
            {
                IdPedido = 101,
                IdProveedor = 1,
                RazonSocialProveedor = "Distribuidora Sur S.R.L.",
                FechaEmision = new DateTime(2026, 11, 1, 10, 30, 0, DateTimeKind.Utc),
                CantidadRenglones = 4,
                TotalEstimado = 12500.50m,
            });
            viewModel.Pedidos.Add(new PedidoEmitidoDto
            {
                IdPedido = 102,
                IdProveedor = 2,
                RazonSocialProveedor = "Papelera del Litoral S.A.",
                FechaEmision = new DateTime(2026, 10, 28, 15, 0, 0, DateTimeKind.Utc),
                CantidadRenglones = 2,
                TotalEstimado = 4800m,
            });

            var dialogo = new SeleccionarPedidoModalDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, SeleccionarPedidoModalDialog.AnchoConProveedor, 620, carpeta + "/SeleccionarPedidoModalDialog_TodosLosProveedores");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PresupuestosView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var presupuestos = DatosDeMuestra.Presupuestos();
            var servicio = Substitute.For<IPresupuestoService>();
            servicio.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>())
                .Returns(new PresupuestosPaginadosDto
                {
                    Items = presupuestos,
                    TotalRegistros = 52,
                    PaginaActual = 1,
                    TamanoPagina = 15
                });

            var viewModel = new PresupuestosViewModel(
                servicio,
                Substitute.For<IPresupuestoDialogService>(),
                new RelojDePrueba(DatosDeMuestra.AhoraPresupuestos),
                Substitute.For<ILogger<PresupuestosViewModel>>());

            var vista = new PresupuestosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/PresupuestosView", () => viewModel.Presupuestos.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PresupuestosView_SinPresupuestos_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var servicio = Substitute.For<IPresupuestoService>();
            servicio.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>())
                .Returns(new PresupuestosPaginadosDto { Items = [], TotalRegistros = 0, PaginaActual = 1, TamanoPagina = 15 });

            var viewModel = new PresupuestosViewModel(
                servicio,
                Substitute.For<IPresupuestoDialogService>(),
                new RelojDePrueba(DatosDeMuestra.AhoraPresupuestos),
                Substitute.For<ILogger<PresupuestosViewModel>>());

            var vista = new PresupuestosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/PresupuestosView_Vacio", () => viewModel.EstaVacio);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ListasView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var servicio = Substitute.For<IListaService>();
            servicio.ListarAsync(Arg.Any<ConsultaListasDto>(), Arg.Any<CancellationToken>())
                .Returns(new ListasPaginadosDto
                {
                    Items = DatosDeMuestra.Listas(),
                    TotalRegistros = 31,
                    PaginaActual = 1,
                    TamanoPagina = 15
                });

            var viewModel = new ListasViewModel(
                servicio,
                Substitute.For<IListaDialogService>(),
                new RelojDePrueba(DatosDeMuestra.AhoraListas),
                Substitute.For<ILogger<ListasViewModel>>());

            var vista = new ListasView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/ListasView", () => viewModel.Listas.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ListaFormDialog_ConUnArticuloDadoDeBaja_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var acceso = new CargaSerializada();
            var viewModel = new ListaFormViewModel(
                DatosDeMuestra.ListaDetalle(),
                Substitute.For<IListaService>(),
                Substitute.For<IVentaService>(),
                Substitute.For<ICurrentUserSession>(),
                acceso,
                Substitute.For<ILogger>());

            var dialogo = new ListaFormDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 800, 700, carpeta + "/ListaFormDialog", () => viewModel.Editor.Renglones.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void SeleccionarListaDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var servicio = Substitute.For<IListaService>();
            servicio.ListarAsync(Arg.Any<ConsultaListasDto>(), Arg.Any<CancellationToken>())
                .Returns(new ListasPaginadosDto
                {
                    Items = DatosDeMuestra.Listas(),
                    TotalRegistros = 5,
                    PaginaActual = 1,
                    TamanoPagina = 50
                });

            var viewModel = new SeleccionarListaViewModel(
                servicio,
                new CargaSerializada(),
                Substitute.For<ILogger>(),
                new RelojDePrueba(DatosDeMuestra.AhoraListas));

            var dialogo = new SeleccionarListaDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 760, 600, carpeta + "/SeleccionarListaDialog", () => viewModel.Listas.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro, true, "GuardarListaDialog_Actualizar")]
    [InlineData(TemaPrueba.Oscuro, true, "GuardarListaDialog_Actualizar")]
    [InlineData(TemaPrueba.Claro, false, "GuardarListaDialog_Nueva")]
    [InlineData(TemaPrueba.Oscuro, false, "GuardarListaDialog_Nueva")]
    public void GuardarListaDialog_PorTema_GeneraCaptura(TemaPrueba tema, bool conListaCargada, string nombre)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new GuardarListaViewModel(
                2,
                [new RenglonListaEntradaDto { IdArticulo = 1, Cantidad = 3 }, new RenglonListaEntradaDto { IdArticulo = 2, Cantidad = 12 }],
                conListaCargada ? new ListaCargada(12, "3.º grado — Colegio San Martín", DateTime.UtcNow) : null,
                Substitute.For<IListaService>(),
                new CargaSerializada(),
                Substitute.For<ILogger>());

            var dialogo = new GuardarListaDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 460, 420, carpeta + "/" + nombre);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro, true, "PreguntaCargaListaDialog")]
    [InlineData(TemaPrueba.Oscuro, true, "PreguntaCargaListaDialog")]
    [InlineData(TemaPrueba.Claro, false, "PreguntaCargaListaDialog_ConPresupuesto")]
    public void PreguntaCargaListaDialog_PorTema_GeneraCaptura(TemaPrueba tema, bool permitirAgregar, string nombre)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var dialogo = new PreguntaCargaListaDialog("3.º grado — Colegio San Martín", permitirAgregar);
            return SnapshotRenderer.Guardar(dialogo, 500, 300, carpeta + "/" + nombre);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ProgramarMantenimientoDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var dialogo = new ProgramarMantenimientoDialog(2);
            return SnapshotRenderer.Guardar(dialogo, 500, 330, carpeta + "/ProgramarMantenimientoDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PosView_ConListaCargada_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        // Ficha «Lista «nombre»» con su ✕ junto a los desplegables Cargar ▾ y Guardar como ▾
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var caja = Substitute.For<ICajaService>();
            caja.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(new TurnoCajaDto
            {
                IdTurno = 12,
                IdUsuario = 1,
                NombreUsuario = "admin",
                FechaApertura = DateTime.UtcNow,
                SaldoInicial = 5000m,
                SaldoTeoricoEfectivo = 5000m,
                Estado = EstadoTurnoEnum.Abierto
            });

            var viewModel = new PosViewModel(
                Substitute.For<IVentaService>(),
                caja,
                Substitute.For<IInventarioService>(),
                DatosDeMuestraPaginas.Sesion(1, false),
                Substitute.For<IVentaDialogService>(),
                Substitute.For<ICajaDialogService>(),
                Substitute.For<IPresupuestoService>(),
                Substitute.For<IPresupuestoDialogService>(),
                Substitute.For<IClienteService>(),
                Substitute.For<IListaService>(),
                Substitute.For<IListaDialogService>());

            var lista = DatosDeMuestra.ListaParaVenta();
            foreach (var renglon in lista.Renglones)
            {
                viewModel.Items.Add(new ItemVentaPosViewModel
                {
                    NumeroItem = viewModel.Items.Count + 1,
                    IdArticulo = renglon.IdArticulo,
                    CodigoBarras = renglon.CodigoBarras,
                    Descripcion = renglon.Descripcion,
                    PrecioUnitario = renglon.PrecioActual,
                    Cantidad = renglon.Cantidad,
                    StockActual = renglon.StockActual
                });
            }

            viewModel.ListaCargada = new ListaCargada(lista.IdLista, lista.Nombre, lista.FechaModificacion);

            var vista = new PosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/PosView_Lista", () => viewModel.EstadoCajaVerificado);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void DetallePresupuestoDialog_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var servicio = Substitute.For<IPresupuestoService>();
            servicio.ObtenerAsync(123, Arg.Any<CancellationToken>()).Returns(DatosDeMuestra.PresupuestoDetalle());

            var viewModel = new DetallePresupuestoViewModel(
                123,
                servicio,
                Substitute.For<IPresupuestoDialogService>(),
                new RelojDePrueba(DatosDeMuestra.AhoraPresupuestos),
                Substitute.For<ILogger<DetallePresupuestoViewModel>>());

            var dialogo = new DetallePresupuestoDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 540, 720, carpeta + "/DetallePresupuestoDialog", () => viewModel.Cargado);
        });

        VerificarCaptura(ruta, error);
    }

    private static PresupuestoFormViewModel CrearFormularioPresupuesto(PresupuestoParaVentaDto? aCorregir, CargaSerializada acceso)
    {
        var sesion = Substitute.For<ICurrentUserSession>();
        sesion.IdUsuario.Returns(2);

        return new PresupuestoFormViewModel(
            aCorregir,
            Substitute.For<IPresupuestoService>(),
            Substitute.For<IVentaService>(),
            Substitute.For<IClienteService>(),
            Substitute.For<IVentaDialogService>(),
            Substitute.For<IPresupuestoDialogService>(),
            sesion,
            acceso,
            new RelojDePrueba(DatosDeMuestra.AhoraPresupuestos),
            Substitute.For<ILogger>());
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PresupuestoFormDialog_Nuevo_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var acceso = new CargaSerializada();
            var viewModel = CrearFormularioPresupuesto(null, acceso);

            // Tres artículos como los dejaría el buscador, con cantidades distintas, un título, un cliente y un descuento
            foreach (var articulo in DatosDeMuestra.ArticulosParaPresupuestoNuevo())
            {
                viewModel.Editor.AgregarArticuloCommand.Execute(articulo);
            }

            viewModel.Editor.Renglones[0].Cantidad = 3;
            viewModel.Editor.Renglones[1].Cantidad = 12;
            viewModel.Editor.Renglones[2].Cantidad = 2;
            viewModel.Titulo = "Útiles de 3.º grado";
            viewModel.Cliente = DatosDeMuestra.Clientes()[0];
            viewModel.Descuento = 1500d;

            var dialogo = new PresupuestoFormDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 960, 760, carpeta + "/PresupuestoFormDialog_Nuevo", () => viewModel.Editor.Renglones.Count == 3);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PresupuestoFormDialog_Correccion_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            using var acceso = new CargaSerializada();

            // Con novedades: dos precios cambiaron y un artículo se dio de baja, como los avisos de la corrección
            var viewModel = CrearFormularioPresupuesto(DatosDeMuestra.PresupuestoParaVenta(vigente: true, conNovedades: true), acceso);

            var dialogo = new PresupuestoFormDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 960, 760, carpeta + "/PresupuestoFormDialog_Correccion", () => viewModel.Editor.Renglones.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PosView_ConPresupuestoVigenteCargado_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        // Etiqueta del presupuesto en lugar de "Guardar presupuesto" y candados en las líneas con tope (PS-04)
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var caja = Substitute.For<ICajaService>();
            caja.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(new TurnoCajaDto
            {
                IdTurno = 12,
                IdUsuario = 1,
                NombreUsuario = "admin",
                FechaApertura = DateTime.UtcNow,
                SaldoInicial = 5000m,
                SaldoTeoricoEfectivo = 5000m,
                Estado = EstadoTurnoEnum.Abierto
            });

            var viewModel = new PosViewModel(
                Substitute.For<IVentaService>(),
                caja,
                Substitute.For<IInventarioService>(),
                DatosDeMuestraPaginas.Sesion(1, false),
                Substitute.For<IVentaDialogService>(),
                Substitute.For<ICajaDialogService>(),
                Substitute.For<IPresupuestoService>(),
                Substitute.For<IPresupuestoDialogService>(),
                Substitute.For<IClienteService>(),
                Substitute.For<IListaService>(),
                Substitute.For<IListaDialogService>());

            var presupuesto = DatosDeMuestra.PresupuestoParaVenta(vigente: true);
            foreach (var renglon in presupuesto.Renglones)
            {
                viewModel.Items.Add(new ItemVentaPosViewModel
                {
                    NumeroItem = viewModel.Items.Count + 1,
                    IdArticulo = renglon.IdArticulo,
                    CodigoBarras = renglon.CodigoBarras,
                    Descripcion = renglon.Descripcion,
                    PrecioUnitario = renglon.PrecioACobrar,
                    Cantidad = renglon.Cantidad,
                    StockActual = renglon.StockActual,
                    EsDePresupuesto = true,
                    CantidadMaxima = renglon.Cantidad
                });
            }

            // Una línea nueva, agregada a precio de catálogo y sin tope
            viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
            {
                IdArticulo = 3,
                CodigoBarras = "7790001000035",
                Descripcion = "Regla de plástico 30 cm",
                PrecioVenta = 700m,
                StockActual = 25,
                EsServicio = false
            });
            viewModel.Descuento = presupuesto.Descuento;
            viewModel.PresupuestoCargado = new PresupuestoCargado(
                presupuesto.IdPresupuesto,
                presupuesto.Titulo,
                presupuesto.EstaVigente,
                presupuesto.FechaVencimiento);

            var vista = new PosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/PosView_Presupuesto", () => viewModel.EstadoCajaVerificado);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro, false, "GuardarPresupuestoDialog_Datos")]
    [InlineData(TemaPrueba.Oscuro, false, "GuardarPresupuestoDialog_Datos")]
    [InlineData(TemaPrueba.Claro, true, "GuardarPresupuestoDialog_Guardado")]
    [InlineData(TemaPrueba.Oscuro, true, "GuardarPresupuestoDialog_Guardado")]
    public void GuardarPresupuestoDialog_PorTema_GeneraCaptura(TemaPrueba tema, bool guardado, string nombre)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var servicio = Substitute.For<IPresupuestoService>();
            servicio.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>()).Returns(DatosDeMuestra.PresupuestoDetalle());

            var viewModel = new GuardarPresupuestoViewModel(
                2,
                7,
                "Escuela N.º 12 Domingo F. Sarmiento",
                9690m,
                184110m,
                [new DetallePresupuestoDto { IdArticulo = 1, Descripcion = "Cuaderno", Cantidad = 3, PrecioUnitarioPactado = 2450m }],
                servicio,
                Substitute.For<IPresupuestoDialogService>(),
                new CargaSerializada(),
                Substitute.For<ILogger>());

            if (guardado)
            {
                Task.Run(() => viewModel.GuardarCommand.ExecuteAsync(null)).GetAwaiter().GetResult();
            }
            else
            {
                viewModel.Titulo = "Lista escolar 3.º grado";
            }

            var dialogo = new GuardarPresupuestoDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 460, 520, carpeta + "/" + nombre);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void GuardarPresupuestoDialog_DesdeBase_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new GuardarPresupuestoViewModel(
                2,
                7,
                "Escuela N.º 12 Domingo F. Sarmiento",
                500m,
                184110m,
                [new DetallePresupuestoDto { IdArticulo = 1, Descripcion = "Cuaderno", Cantidad = 3, PrecioUnitarioPactado = 2700m }],
                Substitute.For<IPresupuestoService>(),
                Substitute.For<IPresupuestoDialogService>(),
                new CargaSerializada(),
                Substitute.For<ILogger>(),
                numeroBase: 123,
                tituloBase: "Lista escolar 3.º grado",
                preciosActualizados: 2);

            var dialogo = new GuardarPresupuestoDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 460, 620, carpeta + "/GuardarPresupuestoDialog_DesdeBase");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void RecuperarPresupuestoDialog_ConClientePrefiltrado_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = CrearRecuperarPresupuesto(DatosDeMuestra.PresupuestosPendientesDeUnCliente(), conCliente: true);

            var dialogo = new RecuperarPresupuestoDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 900, 640, carpeta + "/RecuperarPresupuestoDialog");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void RecuperarPresupuestoDialog_SinPendientes_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = CrearRecuperarPresupuesto([], conCliente: false);

            var dialogo = new RecuperarPresupuestoDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 900, 640, carpeta + "/RecuperarPresupuestoDialog_Vacio");
        });

        VerificarCaptura(ruta, error);
    }

    // La lista se carga antes de crear el diálogo (la carga corre en un hilo del pool y no debe tocar una grilla ya enlazada).
    private static RecuperarPresupuestoViewModel CrearRecuperarPresupuesto(IReadOnlyList<PresupuestoResumenDto> pendientes, bool conCliente)
    {
        var servicio = Substitute.For<IPresupuestoService>();
        servicio.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>())
            .Returns(new PresupuestosPaginadosDto
            {
                Items = pendientes,
                TotalRegistros = pendientes.Count,
                PaginaActual = 1,
                TamanoPagina = 50
            });

        var viewModel = new RecuperarPresupuestoViewModel(
            servicio,
            new CargaSerializada(),
            Substitute.For<ILogger>(),
            conCliente ? 7 : null,
            conCliente ? DatosDeMuestra.ClienteDeLosPresupuestosPendientes : null,
            new RelojDePrueba(DatosDeMuestra.AhoraPresupuestos));
        Task.Run(() => viewModel.CargarInicialAsync()).GetAwaiter().GetResult();
        return viewModel;
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void AlertaPreciosPresupuestoDialog_ConDiscrepanciasFaltantesYBajas_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = new AlertaPreciosPresupuestoViewModel(DatosDeMuestra.PresupuestoParaVenta(vigente: false, conNovedades: true));

            var dialogo = new AlertaPreciosPresupuestoDialog(viewModel);
            return SnapshotRenderer.Guardar(dialogo, 580, 700, carpeta + "/AlertaPreciosPresupuestoDialog");
        });

        VerificarCaptura(ruta, error);
    }

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
