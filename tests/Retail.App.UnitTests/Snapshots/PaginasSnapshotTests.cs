using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Backups;
using Retail.App.ViewModels.Caja;
using Retail.App.ViewModels.Pedidos;
using Retail.App.ViewModels.Proveedores;
using Retail.App.ViewModels.Reportes;
using Retail.App.ViewModels.Usuarios;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.DTOs.Reportes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.Snapshots;

/// <summary>
/// "Bucle de capturas" de las páginas de listado y gestión (Proveedores, Usuarios, Pedidos, Importador de catálogos,
/// Respaldos, Caja y Dashboard): renderiza cada una con datos de muestra a PNG en <c>artifacts/snapshots/Light</c> y
/// <c>artifacts/snapshots/Dark</c>. Se ejecuta a pedido con <c>--filter "Category=Snapshot"</c>.
/// </summary>
[Trait("Category", "Snapshot")]
public class PaginasSnapshotTests
{
    private const int TamanoMinimoPng = 1000;

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ProveedoresView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var servicio = Substitute.For<IProveedorService>();
            servicio.ListarProveedoresAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.Proveedores());

            var viewModel = new ProveedoresViewModel(
                servicio,
                Substitute.For<IProveedorDialogService>(),
                Substitute.For<ILogger<ProveedoresViewModel>>());

            var vista = new ProveedoresView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/ProveedoresView", () => viewModel.Proveedores.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void UsuariosView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            // Con un Gerente en sesión se dibuja también la acción de desbloquear cuentas.
            var gerente = Substitute.For<ICurrentUserSession>();
            gerente.IdUsuario.Returns(1);
            gerente.EsGerente.Returns(true);
            gerente.Rol.Returns(RolUsuarioEnum.Gerente);

            var servicio = Substitute.For<IUsuarioService>();
            servicio.ObtenerRolesAsignablesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Enum.GetValues<RolUsuarioEnum>());
            servicio.ListarUsuariosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.Usuarios());

            var viewModel = new UsuariosViewModel(servicio, Substitute.For<IUsuarioDialogService>(), gerente);
            var vista = new UsuariosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/UsuariosView", () => viewModel.Usuarios.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void PedidosView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var pedidos = DatosDeMuestraPaginas.Pedidos();
            var servicioPedidos = Substitute.For<IPedidoService>();
            servicioPedidos.ListarPedidosAsync(Arg.Any<ConsultaPedidosDto>(), Arg.Any<CancellationToken>())
                .Returns(new PedidosPaginadosDto
                {
                    Items = pedidos,
                    TotalRegistros = 37,
                    PaginaActual = 1,
                    TamanoPagina = 20
                });

            var servicioProveedores = Substitute.For<IProveedorService>();
            servicioProveedores.ListarProveedoresAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.Proveedores());

            var viewModel = new PedidosViewModel(
                servicioPedidos,
                servicioProveedores,
                Substitute.For<IPedidoDialogService>(),
                new RelojDePrueba(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)),
                Substitute.For<ILogger<PedidosViewModel>>());

            // Un filtro de estado aplicado (Emitidos), para que la captura muestre el embudo resaltado en su cabecera
            viewModel.FiltroEstado.EstaAbierto = true;
            viewModel.FiltroEstado.Grupos[0].Opciones[1].Seleccionada = true;
            viewModel.FiltroEstado.AplicarCommand.Execute(null);

            var vista = new PedidosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/PedidosView", () => viewModel.Pedidos.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void ImportadorCatalogosView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var items = DatosDeMuestraPaginas.CatalogoProveedor();
            var servicio = Substitute.For<IProveedorService>();
            servicio.ListarProveedoresAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.Proveedores());
            servicio.ListarItemsCatalogoAsync(Arg.Any<ConsultaCatalogoProveedorDto>(), Arg.Any<CancellationToken>())
                .Returns(new CatalogoPaginadoDto
                {
                    Items = items,
                    TotalRegistros = 312,
                    PaginaActual = 1,
                    TamanoPagina = 50,
                    FechaUltimaPlanilla = DateTime.UtcNow.AddDays(-3)
                });

            // Con un Gerente en sesión, para que también se dibujen las acciones de pedir.
            var gerente = Substitute.For<ICurrentUserSession>();
            gerente.EsGerente.Returns(true);
            var modalViewModel = new IncorporarArticulosModalViewModel(
                Substitute.For<IClasificacionService>(),
                Substitute.For<ITerminoClasificacionService>(),
                Substitute.For<IEquivalenciaClasificacionService>(),
                Substitute.For<IRubroService>(),
                Substitute.For<ICurrentUserSession>(),
                Substitute.For<ILogger<IncorporarArticulosModalViewModel>>());

            var viewModel = new ImportadorCatalogosViewModel(
                servicio,
                Substitute.For<IProveedorDialogService>(),
                Substitute.For<IInventarioService>(),
                Substitute.For<INavigationService>(),
                modalViewModel,
                new FlujoPedirArticulos(
                    Substitute.For<IPedidoService>(),
                    Substitute.For<IPedidoDialogService>(),
                    gerente,
                    Substitute.For<ILogger<FlujoPedirArticulos>>()),
                TimeProvider.System,
                Substitute.For<ILogger<ImportadorCatalogosViewModel>>());

            // La vista carga los proveedores al mostrarse y activa el primero, que dispara la carga de su catálogo.
            var vista = new ImportadorCatalogosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/ImportadorCatalogosView", () => viewModel.ItemsCatalogo.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void BackupView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var gerente = Substitute.For<ICurrentUserSession>();
            gerente.IdUsuario.Returns(1);
            gerente.EsGerente.Returns(true);

            var backups = Substitute.For<IBackupService>();
            backups.ListarAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.Copias());

            var restauraciones = Substitute.For<IRestauracionService>();
            restauraciones.ObtenerUltimaRestauracionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(DatosDeMuestraPaginas.UltimaRestauracion());
            restauraciones.ListarRestauracionesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new List<Retail.Application.DTOs.Backups.RegistroRestauracionDto> { DatosDeMuestraPaginas.UltimaRestauracion() });

            var viewModel = new BackupViewModel(
                backups,
                restauraciones,
                Substitute.For<IBackupDialogService>(),
                gerente,
                Substitute.For<IReinicioAplicacion>());

            var vista = new BackupView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/BackupView", () => viewModel.Copias.Count > 0);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void CajaView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            // El ViewModel consulta el turno activo en su constructor: el doble se configura antes de crearlo.
            var servicio = Substitute.For<ICajaService>();
            servicio.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.TurnoAbierto());

            var viewModel = new CajaViewModel(servicio, Substitute.For<ICajaDialogService>());
            var vista = new CajaView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/CajaView", () => viewModel.TieneTurnoActivo);
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TemaPrueba.Claro)]
    [InlineData(TemaPrueba.Oscuro)]
    public void DashboardView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            // El ViewModel consulta el tablero ANTES de crear la vista, en un hilo sin contexto de sincronización
            // (como en AppSmokeTests): así las tarjetas y los gráficos ya tienen datos al dibujarse.
            var viewModel = Task.Run(CrearDashboardConTableroAsync).GetAwaiter().GetResult();

            var vista = new DashboardView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/DashboardView");
        });

        VerificarCaptura(ruta, error);
    }

    private static async Task<DashboardViewModel> CrearDashboardConTableroAsync()
    {
        var sesion = Substitute.For<ICurrentUserSession>();
        sesion.IdUsuario.Returns(1);
        sesion.EsGerente.Returns(true);

        var desde = new DateOnly(2026, 10, 1);
        var hasta = new DateOnly(2026, 10, 31);
        var ventasPorDia = new (int Cantidad, decimal Total)[]
        {
            (18, 142000m), (22, 171500m), (15, 118200m), (27, 205800m), (31, 238400m),
            (12, 96300m), (20, 160900m), (25, 198700m), (29, 224100m), (34, 262500m)
        };
        var evolucion = ventasPorDia
            .Select((v, i) => new PuntoVentasDto
            {
                InicioPeriodo = desde.AddDays(i * 3),
                CantidadVentas = v.Cantidad,
                TotalVendido = v.Total,
                Efectivo = v.Total * 0.45m,
                TarjetaDebito = v.Total * 0.25m,
                TarjetaCredito = v.Total * 0.15m,
                TransferenciaQr = v.Total * 0.10m,
                CuentaCorriente = v.Total * 0.05m
            })
            .ToList();

        var margenes = new[]
        {
            new RentabilidadFilaDto { Grupo = "Cuadernos", Unidades = 410, IngresoNeto = 758500m, Costo = 455100m, MargenBruto = 303400m, MargenPorcentual = 40m },
            new RentabilidadFilaDto { Grupo = "Escritura", Unidades = 980, IngresoNeto = 312000m, Costo = 171600m, MargenBruto = 140400m, MargenPorcentual = 45m },
            new RentabilidadFilaDto { Grupo = "Papel para impresión", Unidades = 120, IngresoNeto = 828000m, Costo = 540000m, MargenBruto = 288000m, MargenPorcentual = 34.8m },
            new RentabilidadFilaDto { Grupo = "Servicios", Unidades = 2300, IngresoNeto = 92000m, Costo = 18400m, MargenBruto = 73600m, MargenPorcentual = 80m }
        };

        var reportes = Substitute.For<IReporteService>();
        reportes.ObtenerTableroAsync(1, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>()).Returns(new TableroDto
        {
            Filtro = new FiltroPeriodoDto { Desde = desde, Hasta = hasta },
            TotalVendido = new IndicadorDto { Valor = 1818400m, ValorAnterior = 1530000m, VariacionPorcentual = 18.8m },
            CantidadVentas = new IndicadorDto { Valor = 233m, ValorAnterior = 251m, VariacionPorcentual = -7.2m },
            MargenBruto = new IndicadorDto { Valor = 805400m, ValorAnterior = 640000m, VariacionPorcentual = 25.9m },
            MargenPorcentual = 44.3m,
            MargenPorcentualAnterior = 41.8m,
            VariacionMargenPuntos = 2.5m,
            ArticulosBajoStockMinimo = 7,
            ValorizacionInventarioCosto = 3250000m,
            EvolucionVentas = evolucion,
            MargenPorCategoria = margenes
        });

        var inventario = Substitute.For<IInventarioService>();
        inventario.ListarCategoriasAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestra.Categorias());
        inventario.ListarMarcasAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestra.Marcas());
        var usuarios = Substitute.For<IUsuarioService>();
        usuarios.ListarUsuariosAsync(1, Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.Usuarios());

        var viewModel = new DashboardViewModel(
            reportes,
            Substitute.For<IExportacionReportesService>(),
            inventario,
            usuarios,
            sesion,
            Substitute.For<IReporteDialogService>(),
            TimeProvider.System);

        await viewModel.CargarAsync();
        return viewModel;
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
