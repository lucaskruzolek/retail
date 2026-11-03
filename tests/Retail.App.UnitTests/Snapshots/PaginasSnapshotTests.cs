using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Backups;
using Retail.App.ViewModels.Caja;
using Retail.App.ViewModels.Compras;
using Retail.App.ViewModels.Pedidos;
using Retail.App.ViewModels.Proveedores;
using Retail.App.ViewModels.Reportes;
using Retail.App.ViewModels.Usuarios;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Compras;
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
    [InlineData("", TemaPrueba.Claro)]
    [InlineData("", TemaPrueba.Oscuro)]
    [InlineData("_ConPedido", TemaPrueba.Claro)]
    [InlineData("_ConPedido", TemaPrueba.Oscuro)]
    [InlineData("_Incompleta", TemaPrueba.Claro)]
    [InlineData("_Incompleta", TemaPrueba.Oscuro)]
    public void ComprasView_PorTema_GeneraCaptura(string estado, TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var servicioProveedores = Substitute.For<IProveedorService>();
            servicioProveedores.ListarProveedoresAsync(Arg.Any<CancellationToken>()).Returns(DatosDeMuestraPaginas.Proveedores());
            var viewModel = new ComprasViewModel(
                servicioProveedores,
                Substitute.For<ICompraService>(),
                Substitute.For<IPedidoService>(),
                Substitute.For<ICompraDialogService>(),
                Substitute.For<ICurrentUserSession>(),
                Substitute.For<ILogger<ComprasViewModel>>());

            // El combo parte de "Todos los proveedores" y los proveedores reales: se cargan antes de armar la vista
            Task.Run(() => viewModel.CargarProveedoresCommand.ExecuteAsync(null)).GetAwaiter().GetResult();

            // Estado "" = pantalla recién abierta: "Todos los proveedores" y sin renglones (estado vacío)
            if (estado == "_ConPedido")
            {
                // Comprobante completo con pedido vinculado y tres renglones (uno cuyo precio sube, uno sin cambio y uno manual)
                var proveedor = viewModel.Proveedores[0];
                viewModel.ProveedorSeleccionado = proveedor;
                viewModel.NumeroComprobante = "0003-00004521";
                viewModel.FechaEmision = new DateTime(2026, 11, 2);
                foreach (var renglon in DatosDeMuestraPaginas.RenglonesDeCompra())
                {
                    viewModel.DetalleCompraItems.Add(renglon);
                }

                viewModel.IdPedidoOrigen = 101;
                viewModel.InformacionPedidoOrigen = "Pedido #101";
                viewModel.TienePedidoOrigen = true;
            }
            else if (estado == "_Incompleta")
            {
                // Con proveedor, sin número y con un renglón de costo 0: la lista de comprobación mezcla cumplidos y pendientes
                viewModel.ProveedorSeleccionado = viewModel.Proveedores[0];
                viewModel.FechaEmision = new DateTime(2026, 11, 2);
                viewModel.DetalleCompraItems.Add(DetalleCompraItemViewModel.DesdeArticulo(new ArticuloParaCompraDto
                {
                    IdArticulo = 104,
                    CodigoBarras = "7791112223334",
                    Descripcion = "Marcador permanente negro",
                    CostoReposicion = 0m,
                    PorcentajeGanancia = 50m,
                    PrecioVenta = 900m
                }));
            }

            var vista = new ComprasView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/ComprasView" + estado);
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

            // Opciones de los filtros de Categoría y Marca (el rubro lo resuelve cada categoría).
            var inventario = Substitute.For<IInventarioService>();
            inventario.ListarCategoriasAsync(Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IReadOnlyList<CategoriaDto>>(DatosDeMuestraDialogosGrandes.Categorias()));
            inventario.ListarMarcasAsync(Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IReadOnlyList<MarcaDto>>(DatosDeMuestraDialogosGrandes.Marcas()));
            var rubros = Substitute.For<IRubroService>();
            rubros.ListarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IReadOnlyList<RubroDto>>(DatosDeMuestraDialogosGrandes.Rubros()));

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
                inventario,
                rubros,
                Substitute.For<IClasificacionCatalogoService>(),
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
            // Ya cargado el catálogo (cambiar de proveedor borra los avisos), se muestra el resumen de una clasificación con
            // ítems sin categoría, para que la captura incluya el enlace "Ver sin categoría" (M-81).
            var vista = new ImportadorCatalogosView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/ImportadorCatalogosView", () =>
            {
                if (viewModel.ItemsCatalogo.Count == 0)
                {
                    return false;
                }

                // La primera vez fija el aviso y espera otra pasada, para que el diseño ya lo incluya al capturar.
                if (!viewModel.MostrarVerSinCategoria)
                {
                    viewModel.MensajeEstado = "Catálogo clasificado: 312 ítems, 280 con categoría y 251 con marca. 32 quedaron sin categoría.";
                    viewModel.MostrarVerSinCategoria = true;
                    return false;
                }

                return true;
            });
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
    public void AjustesView_PorTema_GeneraCaptura(TemaPrueba tema)
    {
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var preferencias = Substitute.For<IPreferenciasUsuario>();
            preferencias.Tema.Returns(tema == TemaPrueba.Oscuro ? TemaVisual.Oscuro : TemaVisual.Claro);

            var viewModel = new Retail.App.ViewModels.Ajustes.AjustesViewModel(Substitute.For<IServicioTema>(), preferencias);
            var vista = new AjustesView(viewModel);

            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/AjustesView");
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
            var viewModel = Task.Run(() => CrearDashboardConTableroAsync()).GetAwaiter().GetResult();

            var vista = new DashboardView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/DashboardView");
        });

        VerificarCaptura(ruta, error);
    }

    [Theory]
    [InlineData(TipoReporteEnum.VentasPorPeriodo, TemaPrueba.Claro)]
    [InlineData(TipoReporteEnum.Ranking, TemaPrueba.Claro)]
    [InlineData(TipoReporteEnum.Rentabilidad, TemaPrueba.Claro)]
    [InlineData(TipoReporteEnum.Rentabilidad, TemaPrueba.Oscuro)]
    public void DashboardView_PorReporte_GeneraCaptura(TipoReporteEnum reporte, TemaPrueba tema)
    {
        // Las grillas de los reportes: se revisan los márgenes de la primera y la última columna contra el borde
        var (ruta, error) = Capturar(tema, carpeta =>
        {
            var viewModel = Task.Run(() => CrearDashboardConTableroAsync(reporte)).GetAwaiter().GetResult();

            var vista = new DashboardView(viewModel);
            return SnapshotRenderer.Guardar(vista, 1366, 768, carpeta + "/DashboardView_" + reporte);
        });

        VerificarCaptura(ruta, error);
    }

    private static async Task<DashboardViewModel> CrearDashboardConTableroAsync(TipoReporteEnum reporte = TipoReporteEnum.Tablero)
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
            DesdeAnterior = new DateOnly(2026, 10, 1),
            HastaAnterior = new DateOnly(2026, 10, 3),
            VariacionMargenPuntos = 2.5m,
            ArticulosBajoStockMinimo = 7,
            ValorizacionInventarioCosto = 3250000m,
            EvolucionVentas = evolucion,
            MargenPorCategoria = margenes
        });

        reportes.ObtenerVentasPorPeriodoAsync(1, Arg.Any<FiltroVentasPorPeriodoDto>(), Arg.Any<CancellationToken>()).Returns(new VentasPorPeriodoDto
        {
            Filtro = new FiltroVentasPorPeriodoDto { Desde = desde, Hasta = hasta },
            Puntos = evolucion,
            TotalVendido = evolucion.Sum(p => p.TotalVendido),
            CantidadVentas = evolucion.Sum(p => p.CantidadVentas),
            Efectivo = evolucion.Sum(p => p.Efectivo),
            TarjetaDebito = evolucion.Sum(p => p.TarjetaDebito),
            TarjetaCredito = evolucion.Sum(p => p.TarjetaCredito),
            TransferenciaQr = evolucion.Sum(p => p.TransferenciaQr),
            CuentaCorriente = evolucion.Sum(p => p.CuentaCorriente)
        });
        reportes.ObtenerRankingAsync(1, Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>()).Returns(new RankingArticulosDto
        {
            Filtro = new FiltroRankingDto { Desde = desde, Hasta = hasta },
            MasVendidos = Enumerable.Range(1, 8)
                .Select(i => new RankingArticuloDto { IdArticulo = i, Descripcion = $"Cuaderno Rivadavia ABC {i * 10} hojas", Unidades = 90 - (i * 8), IngresoNeto = (90 - (i * 8)) * 1850m })
                .ToList(),
            MenosVendidos = Enumerable.Range(1, 8)
                .Select(i => new RankingArticuloDto { IdArticulo = 100 + i, Descripcion = $"Lápiz negro Faber-Castell HB x{i}", Unidades = i, IngresoNeto = i * 320m })
                .ToList()
        });
        reportes.ObtenerRentabilidadAsync(1, Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>()).Returns(new RentabilidadDto
        {
            Filtro = new FiltroRentabilidadDto { Desde = desde, Hasta = hasta },
            Filas = margenes,
            Total = new RentabilidadFilaDto
            {
                Grupo = "Total",
                Unidades = margenes.Sum(m => m.Unidades),
                IngresoNeto = margenes.Sum(m => m.IngresoNeto),
                Costo = margenes.Sum(m => m.Costo),
                MargenBruto = margenes.Sum(m => m.MargenBruto),
                MargenPorcentual = 44.3m
            }
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

        if (reporte != TipoReporteEnum.Tablero)
        {
            viewModel.ReporteSeleccionado = reporte;
            await viewModel.EsperarConsultaAutomaticaAsync();
        }

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
