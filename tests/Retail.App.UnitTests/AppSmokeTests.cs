using System.Windows.Controls;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Auth;
using Retail.App.ViewModels.Backups;
using Retail.App.ViewModels.Clientes;
using Retail.App.ViewModels.Compras;
using Retail.App.ViewModels.Proveedores;
using Retail.App.ViewModels.Reportes;
using Retail.App.ViewModels.Usuarios;
using Retail.App.ViewModels.Ventas;
using Retail.App.Views.Auth;
using Retail.App.Views.Dev;
using Retail.App.Views.Dialogs;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Backups;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Retail.App.UnitTests;

public class AppSmokeTests
{
    [Fact]
    public void AppProject_DebeEstarConfiguradoCorrectamente()
    {
        const bool proyectoConfigurado = true;
        proyectoConfigurado.Should().BeTrue();
    }

    [Fact]
    public void ArticuloMuestra_CreacionValida_DebePreservarPropiedades()
    {
        // Arrange & Act
        var articulo = new ArticuloMuestra(
            CodigoBarras: "9789500762885",
            Descripcion: "El Principito",
            Categoria: "Literatura",
            Stock: 3,
            StockBajo: true,
            PrecioVentaFormateado: "$ 12.500,00"
        );

        // Assert
        articulo.CodigoBarras.Should().Be("9789500762885");
        articulo.Descripcion.Should().Be("El Principito");
        articulo.Categoria.Should().Be("Literatura");
        articulo.Stock.Should().Be(3);
        articulo.StockBajo.Should().BeTrue();
        articulo.PrecioVentaFormateado.Should().Be("$ 12.500,00");
    }

    [Fact]
    public void ArticuloMuestra_ArtesaniaSinCodigo_DebeAdmitirIdentificadorArtesanal()
    {
        // Arrange & Act
        var artesania = new ArticuloMuestra(
            CodigoBarras: "(Artesanal)",
            Descripcion: "Señalador de Madera",
            Categoria: "Regalería",
            Stock: 10,
            StockBajo: false,
            PrecioVentaFormateado: "$ 1.800,00"
        );

        // Assert
        artesania.CodigoBarras.Should().Be("(Artesanal)");
        artesania.StockBajo.Should().BeFalse();
        artesania.Stock.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AccentManager_ColorCarmín_DebeAplicarseCorrectamente()
    {
        // Verificar que la API de WPF-UI aplique el color carmín a la paleta de acento
        var color = System.Windows.Media.Color.FromRgb(0x9D, 0x0F, 0x33);
        var ex = Record.Exception(() =>
            Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(color, Wpf.Ui.Appearance.ApplicationTheme.Light));

        ex.Should().BeNull();
    }

    [Fact]
    public void FluentWindow_InspeccionTipos_DebenEstarDisponiblesEnWpfUi()
    {
        var windowType = typeof(Wpf.Ui.Controls.FluentWindow);
        windowType.Should().NotBeNull();
        var titleBarType = typeof(Wpf.Ui.Controls.TitleBar);
        titleBarType.Should().NotBeNull();

        // Verificar propiedades en STA thread
        WpfTestHelper.Run(() =>
        {
            var titleBar = new Wpf.Ui.Controls.TitleBar
            {
                Title = "Retail POS"
            };
            titleBar.Title.Should().Be("Retail POS");

            // Verificar si TitleBar soporta Header o Content
            titleBar.Header = "HeaderTest";
            titleBar.Header.Should().Be("HeaderTest");

            titleBar.Icon = new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.BarcodeScanner24);
            titleBar.Icon.Should().NotBeNull();

            var window = new Wpf.Ui.Controls.FluentWindow
            {
                ExtendsContentIntoTitleBar = true,
                WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType.Mica
            };
            window.ExtendsContentIntoTitleBar.Should().BeTrue();
        });
    }

    [Fact]
    public void ArticulosView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ArticulosView? view = null;

        WpfTestHelper.Run(() =>
        {
            var inventarioServiceMock = Substitute.For<IInventarioService>();
            var dialogServiceMock = Substitute.For<IArticuloDialogService>();
            var viewModel = new ArticulosViewModel(inventarioServiceMock, dialogServiceMock);

            try
            {
                view = new ArticulosView(viewModel);
                viewModel.Articulos.Add(new Retail.Application.DTOs.Articulos.ArticuloDto
                {
                    IdArticulo = 1,
                    Descripcion = "Artículo de Prueba",
                    IdCategoria = 1,
                    IdMarca = 1,
                    CostoReposicion = 50m,
                    PorcentajeGanancia = 100m,
                    PrecioVenta = 100m,
                    StockActual = 5,
                    StockMinimo = 10,
                    EsServicio = false
                });
                // Una presentación derivada (RF-21) activa los triggers del badge y del botón Fraccionar.
                viewModel.Articulos.Add(ArticuloDeEjemplo(idArticulo: 2, idArticuloOrigen: 1));
                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ArticulosView debe compilarse y resolverse sin errores de tipo o estilos (KeycapToggleButtonStyle)");
        view.Should().NotBeNull();
        view!.ViewModel.Should().NotBeNull();
    }

    [Fact]
    public void ArticulosView_InputBindings_DebenEstarAsociadosAComandosCorrespondientes()
    {
        // Arrange & Act
        WpfTestHelper.Run(() =>
        {
            var inventarioServiceMock = Substitute.For<IInventarioService>();
            var dialogServiceMock = Substitute.For<IArticuloDialogService>();
            var viewModel = new ArticulosViewModel(inventarioServiceMock, dialogServiceMock);

            var view = new ArticulosView(viewModel);

            // Assert
            view.InputBindings.Count.Should().Be(2, "los filtros no tienen atajo de teclado");

            var keyBindings = view.InputBindings.OfType<System.Windows.Input.KeyBinding>().ToList();
            keyBindings.Should().HaveCount(2);

            var bindingF2 = keyBindings.FirstOrDefault(b => b.Key == System.Windows.Input.Key.F2);
            bindingF2.Should().NotBeNull();
            bindingF2!.Command.Should().Be(viewModel.NuevoArticuloCommand);

            var bindingF5 = keyBindings.FirstOrDefault(b => b.Key == System.Windows.Input.Key.F5);
            bindingF5.Should().NotBeNull();
            bindingF5!.Command.Should().Be(viewModel.CargarArticulosCommand);
        });
    }

    [Fact]
    public void CajaView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        CajaView? view = null;

        WpfTestHelper.Run(() =>
        {
            var cajaServiceMock = Substitute.For<ICajaService>();
            var dialogServiceMock = Substitute.For<ICajaDialogService>();
            var viewModel = new Retail.App.ViewModels.Caja.CajaViewModel(cajaServiceMock, dialogServiceMock);

            try
            {
                view = new CajaView(viewModel);
                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de CajaView debe compilarse y resolverse sin errores de recursos o controles");
        view.Should().NotBeNull();
        view!.ViewModel.Should().NotBeNull();
    }
    [Fact]
    public void ClientesView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ClientesView? view = null;

        WpfTestHelper.Run(() =>
        {
            var clienteServiceMock = Substitute.For<IClienteService>();
            var dialogServiceMock = Substitute.For<IClienteDialogService>();
            var viewModel = new Retail.App.ViewModels.Clientes.ClientesViewModel(clienteServiceMock, dialogServiceMock);

            try
            {
                view = new ClientesView(viewModel);
                viewModel.Clientes.Add(new Retail.Application.DTOs.Clientes.ClienteDto
                {
                    IdCliente = 1,
                    RazonSocialONombre = "Librería Central",
                    TipoDocumento = Retail.Domain.Enums.TipoDocumentoEnum.Cuit,
                    NumeroDocumento = "30712345678",
                    CondicionIva = Retail.Domain.Enums.CondicionIvaEnum.ResponsableInscripto,
                    TieneCuentaCorriente = true,
                    LimiteCredito = 100000m,
                    SaldoCuentaCorriente = 25000m
                });
                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ClientesView debe compilarse y resolverse sin errores de recursos estáticos (TextBlockBodyMuted, etc.)");
        view.Should().NotBeNull();
        view!.ViewModel.Should().NotBeNull();
    }

    [Fact]
    public void CobranzaModalDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        Retail.App.Views.Dialogs.CobranzaModalDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var clienteServiceMock = Substitute.For<IClienteService>();
            var cajaServiceMock = Substitute.For<ICajaService>();
            var clienteEjemplo = new Retail.Application.DTOs.Clientes.ClienteDto
            {
                IdCliente = 10,
                RazonSocialONombre = "Estudiante Universitario",
                TipoDocumento = Retail.Domain.Enums.TipoDocumentoEnum.Dni,
                NumeroDocumento = "40123456",
                CondicionIva = Retail.Domain.Enums.CondicionIvaEnum.ConsumidorFinal,
                TieneCuentaCorriente = true,
                LimiteCredito = 50000m,
                SaldoCuentaCorriente = 12000m
            };

            var viewModel = new Retail.App.ViewModels.Clientes.CobranzaModalViewModel(clienteServiceMock, cajaServiceMock, clienteEjemplo);

            try
            {
                dialog = new Retail.App.Views.Dialogs.CobranzaModalDialog(viewModel);
                dialog.Measure(new System.Windows.Size(560, 680));
                dialog.Arrange(new System.Windows.Rect(0, 0, 560, 680));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de CobranzaModalDialog debe compilarse y resolverse sin errores de recursos estáticos o dinámicos");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void UsuariosView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        UsuariosView? view = null;

        WpfTestHelper.Run(() =>
        {
            var usuarioServiceMock = Substitute.For<IUsuarioService>();
            var dialogServiceMock = Substitute.For<IUsuarioDialogService>();
            var viewModel = new UsuariosViewModel(usuarioServiceMock, dialogServiceMock, Substitute.For<ICurrentUserSession>());

            try
            {
                view = new UsuariosView(viewModel);
                viewModel.Usuarios.Add(new UsuarioDto
                {
                    IdUsuario = 1,
                    NombreUsuario = "admin",
                    Nombre = "Administrador",
                    Apellido = "General",
                    Rol = RolUsuarioEnum.Gerente,
                    Activo = true,
                    CreatedAt = DateTime.UtcNow
                });
                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de UsuariosView debe resolverse sin errores de recursos");
        view.Should().NotBeNull();
        view!.ViewModel.Should().NotBeNull();
    }

    [Fact]
    public void BackupView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        BackupView? view = null;

        WpfTestHelper.Run(() =>
        {
            var session = Substitute.For<ICurrentUserSession>();
            session.EsGerente.Returns(true);
            var viewModel = new BackupViewModel(
                Substitute.For<IBackupService>(),
                Substitute.For<IRestauracionService>(),
                Substitute.For<IBackupDialogService>(),
                session,
                Substitute.For<IReinicioAplicacion>());

            try
            {
                view = new BackupView(viewModel);
                viewModel.UltimaRestauracionTexto = "Última restauración: 01/10/2026 10:00 por gerente (consola).";
                foreach (var estado in Enum.GetValues<EstadoBackupEnum>())
                {
                    viewModel.Copias.Add(new RegistroBackupFila(new RegistroBackupDto
                    {
                        IdRegistroBackup = (int)estado,
                        Referencia = Guid.NewGuid(),
                        Tipo = TipoBackupEnum.Manual,
                        Estado = estado,
                        Operador = "gerente",
                        FechaInicio = DateTime.UtcNow,
                        RutaArchivo = @"C:\RetailData\Backups\copia.bak",
                        TamanoBytes = 5L * 1024 * 1024,
                        EsRestaurable = estado == EstadoBackupEnum.Verificado
                    }));
                }

                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();

                // D-34: la pestaña de restauraciones, vacía y con datos, también debe resolver sus recursos.
                viewModel.MostrandoRestauraciones = true;
                view.UpdateLayout();
                viewModel.Restauraciones.Add(new RegistroRestauracionFila(new RegistroRestauracionDto
                {
                    Referencia = Guid.NewGuid(),
                    Fecha = DateTime.UtcNow,
                    Origen = OrigenRestauracionEnum.Aplicacion,
                    NombreUsuario = "gerente",
                    NombreOperador = "admin",
                    ReferenciaCopia = Guid.NewGuid(),
                    RutaArchivo = @"C:\RetailData\Backups\copia.bak",
                    TuvoCopiaPrevia = true,
                    BaseAnterior = "Retail_anterior"
                }));
                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de BackupView debe resolverse sin errores de recursos");
        view.Should().NotBeNull();
    }

    [Fact]
    public async Task DashboardView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange: el ViewModel consulta y recibe un resultado de cada reporte ANTES de crear la vista, en el hilo de la
        // prueba, para que las tarjetas, las grillas y los gráficos también resuelvan sus recursos al dibujarse.
        var viewModel = await CrearDashboardConResultadosAsync();

        Exception? xamlException = null;
        DashboardView? view = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                view = new DashboardView(viewModel);
                view.Measure(new System.Windows.Size(960, 600));
                view.Arrange(new System.Windows.Rect(0, 0, 960, 600));
                view.UpdateLayout();

                foreach (var tipo in Enum.GetValues<TipoReporteEnum>())
                {
                    viewModel.ReporteSeleccionado = tipo;
                    view.Measure(new System.Windows.Size(960, 600));
                    view.Arrange(new System.Windows.Rect(0, 0, 960, 600));
                    view.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de DashboardView debe resolverse sin errores de recursos");
        view.Should().NotBeNull();
    }

    private static async Task<DashboardViewModel> CrearDashboardConResultadosAsync()
    {
        var session = Substitute.For<ICurrentUserSession>();
        session.IdUsuario.Returns(1);
        session.EsGerente.Returns(true);

        var desde = new DateOnly(2026, 10, 1);
        var hasta = new DateOnly(2026, 10, 15);
        var fila = new RentabilidadFilaDto { Grupo = "Libros", Unidades = 4, IngresoNeto = 4000m, Costo = 2800m, MargenBruto = 1200m, MargenPorcentual = 30m };

        var reportes = Substitute.For<IReporteService>();
        reportes.ObtenerTableroAsync(1, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>()).Returns(new TableroDto
        {
            Filtro = new FiltroPeriodoDto { Desde = desde, Hasta = hasta },
            TotalVendido = new IndicadorDto { Valor = 4000m, ValorAnterior = 3000m, VariacionPorcentual = 33.3m },
            CantidadVentas = new IndicadorDto { Valor = 12m, ValorAnterior = 15m, VariacionPorcentual = -20m },
            MargenBruto = new IndicadorDto { Valor = 1200m },
            MargenPorcentual = 30m,
            VariacionMargenPuntos = 1.5m,
            ArticulosBajoStockMinimo = 2,
            ValorizacionInventarioCosto = 90000m,
            EvolucionVentas = [new PuntoVentasDto { InicioPeriodo = desde, CantidadVentas = 3, TotalVendido = 1000m }, new PuntoVentasDto { InicioPeriodo = hasta, CantidadVentas = 9, TotalVendido = 3000m }],
            MargenPorCategoria = [fila]
        });
        reportes.ObtenerVentasPorPeriodoAsync(1, Arg.Any<FiltroVentasPorPeriodoDto>(), Arg.Any<CancellationToken>()).Returns(new VentasPorPeriodoDto
        {
            Filtro = new FiltroVentasPorPeriodoDto { Desde = desde, Hasta = hasta },
            Puntos = [new PuntoVentasDto { InicioPeriodo = desde, CantidadVentas = 3, TotalVendido = 1000m }],
            TotalVendido = 1000m,
            CantidadVentas = 3
        });
        reportes.ObtenerRankingAsync(1, Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>()).Returns(new RankingArticulosDto
        {
            Filtro = new FiltroRankingDto { Desde = desde, Hasta = hasta },
            MasVendidos = [new RankingArticuloDto { IdArticulo = 1, Descripcion = "El Principito", CodigoBarras = "9789500762885", Unidades = 9, IngresoNeto = 900m }],
            MenosVendidos = [new RankingArticuloDto { IdArticulo = 2, Descripcion = "Atlas", Unidades = 0 }]
        });
        reportes.ObtenerRentabilidadAsync(1, Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>()).Returns(new RentabilidadDto
        {
            Filtro = new FiltroRentabilidadDto { Desde = desde, Hasta = hasta },
            Filas = [fila],
            Total = fila
        });
        reportes.ObtenerStockCriticoAsync(1, Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>()).Returns(new StockCriticoDto
        {
            Filtro = new FiltroInventarioDto(),
            Filas = [new StockCriticoFilaDto { IdArticulo = 1, Descripcion = "Lapicera", CodigoBarras = "7790001", StockActual = 1, StockMinimo = 5, Faltante = 4, CostoReposicion = 50m, PrecioVenta = 100m }],
            ArticulosConsiderados = 10
        });
        reportes.ObtenerSinMovimientoAsync(1, Arg.Any<FiltroSinMovimientoDto>(), Arg.Any<CancellationToken>()).Returns(new SinMovimientoDto
        {
            Filtro = new FiltroSinMovimientoDto(),
            Filas = [new ArticuloSinMovimientoDto { IdArticulo = 3, Descripcion = "Atlas", StockActual = 2, CostoReposicion = 500m, CapitalInmovilizado = 1000m }]
        });

        var inventario = Substitute.For<IInventarioService>();
        inventario.ListarCategoriasAsync(Arg.Any<CancellationToken>()).Returns(new List<CategoriaDto>());
        inventario.ListarMarcasAsync(Arg.Any<CancellationToken>()).Returns(new List<MarcaDto>());
        var usuarios = Substitute.For<IUsuarioService>();
        usuarios.ListarUsuariosAsync(1, Arg.Any<CancellationToken>()).Returns(new List<UsuarioDto>());

        var viewModel = new DashboardViewModel(
            reportes,
            Substitute.For<IExportacionReportesService>(),
            inventario,
            usuarios,
            session,
            Substitute.For<IReporteDialogService>(),
            TimeProvider.System);

        await viewModel.CargarAsync();
        foreach (var tipo in Enum.GetValues<TipoReporteEnum>().Where(t => t != TipoReporteEnum.Tablero))
        {
            viewModel.ReporteSeleccionado = tipo;
            await viewModel.EsperarConsultaAutomaticaAsync();
        }

        viewModel.ReporteSeleccionado = TipoReporteEnum.Tablero;
        return viewModel;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ConfirmarCredencialDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones(int modo)
    {
        // Arrange
        Exception? xamlException = null;
        ConfirmarCredencialDialog? dialog = null;
        var alturaMedida = 0d;
        var sizeToContent = System.Windows.SizeToContent.Manual;

        WpfTestHelper.Run(() =>
        {
            Func<string, IProgress<int>, Task> operacion = (_, _) => Task.CompletedTask;
            Func<string, Retail.Application.DTOs.Backups.CredencialAutorizanteDto, IProgress<int>, Task> operacionAutorizada = (_, _, _) => Task.CompletedTask;

            // 0 = copia manual, 1 = restauración del Gerente, 2 = restauración autorizada (Administrador, D-30).
            var viewModel = modo switch
            {
                1 => ConfirmarCredencialViewModel.ParaRestauracion(operacion, "01/10/2026 10:00", "Conciliar con ARCA."),
                2 => ConfirmarCredencialViewModel.ParaRestauracionAutorizada(operacionAutorizada, "01/10/2026 10:00", "Conciliar con ARCA."),
                _ => ConfirmarCredencialViewModel.ParaBackupManual(operacion)
            };
            viewModel.MensajeError = "Contraseña incorrecta.";
            viewModel.IsBusy = true;

            try
            {
                dialog = new ConfirmarCredencialDialog(viewModel);
                dialog.Measure(new System.Windows.Size(500, double.PositiveInfinity));
                dialog.Arrange(new System.Windows.Rect(0, 0, 500, 640));
                dialog.UpdateLayout();

                // Sin mostrarla, la ventana no resuelve los bindings de visibilidad ni aplica SizeToContent: se la muestra un
                // instante para medir la altura real que tendría ante el usuario. Fuera de la pantalla, sin foco ni botón en la
                // barra de tareas, para que correr los tests no abra ventanas visibles (como en SelectorFechaCortaTests).
                dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                dialog.Left = -10000;
                dialog.Top = -10000;
                dialog.ShowActivated = false;
                dialog.ShowInTaskbar = false;
                dialog.Show();
                dialog.UpdateLayout();
                alturaMedida = dialog.ActualHeight;
                sizeToContent = dialog.SizeToContent;
                dialog.Close();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ConfirmarCredencialDialog debe resolverse sin errores de recursos");
        dialog.Should().NotBeNull();

        // El alto lo da el contenido (patrón de ClasificacionFormDialog) y respeta la salvaguarda de SISTEMA_DE_DISENO.md.
        sizeToContent.Should().Be(System.Windows.SizeToContent.Height);
        alturaMedida.Should().BeLessThanOrEqualTo(680, $"el modo {modo} no debe superar la altura máxima de un modal");
    }

    [Fact]
    public void UsuarioFormDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        UsuarioFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new UsuarioFormViewModel();
            viewModel.ConfigurarAlta(Enum.GetValues<RolUsuarioEnum>());

            try
            {
                dialog = new UsuarioFormDialog(viewModel);
                dialog.Measure(new System.Windows.Size(520, 600));
                dialog.Arrange(new System.Windows.Rect(0, 0, 520, 600));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de UsuarioFormDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void ClienteFormDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ClienteFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new ClienteFormViewModel();
            viewModel.ConfigurarAlta();

            try
            {
                dialog = new ClienteFormDialog(viewModel);
                dialog.Measure(new System.Windows.Size(600, 700));
                dialog.Arrange(new System.Windows.Rect(0, 0, 600, 700));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ClienteFormDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void ArticuloFormDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ArticuloFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new ArticuloFormViewModel();
            viewModel.ConfigurarAlta(Array.Empty<CategoriaDto>(), Array.Empty<MarcaDto>());

            try
            {
                dialog = new ArticuloFormDialog(viewModel);
                dialog.Measure(new System.Windows.Size(640, 750));
                dialog.Arrange(new System.Windows.Rect(0, 0, 640, 750));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ArticuloFormDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void ArticuloFormDialog_EdicionDePresentacionDerivada_DebeCargarXAMLSinExcepciones()
    {
        // Arrange: el modo derivado oculta servicio y distribuidor y muestra el aviso de costo (RF-21)
        Exception? xamlException = null;
        ArticuloFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new ArticuloFormViewModel();
            viewModel.ConfigurarEdicion(
                ArticuloDeEjemplo(idArticulo: 8, idArticuloOrigen: 7),
                Array.Empty<CategoriaDto>(),
                Array.Empty<MarcaDto>());

            try
            {
                dialog = new ArticuloFormDialog(viewModel);
                dialog.Measure(new System.Windows.Size(640, 750));
                dialog.Arrange(new System.Windows.Rect(0, 0, 640, 750));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el modo derivado de ArticuloFormDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
        dialog!.ViewModel.EsDerivado.Should().BeTrue();
    }

    [Fact]
    public void PresentacionFormDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        PresentacionFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new PresentacionFormViewModel();
            viewModel.Configurar(ArticuloDeEjemplo(idArticulo: 7, idArticuloOrigen: null));

            try
            {
                dialog = new PresentacionFormDialog(viewModel);
                dialog.Measure(new System.Windows.Size(560, 660));
                dialog.Arrange(new System.Windows.Rect(0, 0, 560, 660));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de PresentacionFormDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void FraccionarDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        FraccionarDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new FraccionarViewModel();
            viewModel.Configurar(
                ArticuloDeEjemplo(idArticulo: 8, idArticuloOrigen: 7),
                ArticuloDeEjemplo(idArticulo: 7, idArticuloOrigen: null));

            try
            {
                dialog = new FraccionarDialog(viewModel);
                dialog.Measure(new System.Windows.Size(500, 480));
                dialog.Arrange(new System.Windows.Rect(0, 0, 500, 480));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de FraccionarDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    private static ArticuloDto ArticuloDeEjemplo(int idArticulo, int? idArticuloOrigen)
    {
        return new ArticuloDto
        {
            IdArticulo = idArticulo,
            Descripcion = idArticuloOrigen.HasValue ? "Sobre manila (unidad)" : "Sobre manila (pack x100)",
            IdArticuloOrigen = idArticuloOrigen,
            UnidadesPorOrigen = idArticuloOrigen.HasValue ? 100 : null,
            CostoReposicion = idArticuloOrigen.HasValue ? 10m : 1000m,
            PorcentajeGanancia = 40m,
            PrecioVenta = idArticuloOrigen.HasValue ? 14m : 1400m,
            StockActual = 5,
            StockMinimo = 1,
            EsServicio = false
        };
    }

    [Fact]
    public void CambiarPasswordDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        CambiarPasswordDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var usuarioEjemplo = new UsuarioDto
            {
                IdUsuario = 2,
                NombreUsuario = "cajero1",
                Nombre = "Juan",
                Apellido = "Pérez",
                Rol = RolUsuarioEnum.Cajero,
                Activo = true,
                CreatedAt = DateTime.UtcNow
            };
            var viewModel = new CambiarPasswordViewModel();
            viewModel.Configurar(usuarioEjemplo);

            try
            {
                dialog = new CambiarPasswordDialog(viewModel);
                dialog.Measure(new System.Windows.Size(480, 420));
                dialog.Arrange(new System.Windows.Rect(0, 0, 480, 420));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de CambiarPasswordDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void PosView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        PosView? view = null;

        WpfTestHelper.Run(() =>
        {
            var ventaServiceMock = Substitute.For<IVentaService>();
            var cajaServiceMock = Substitute.For<ICajaService>();
            var sessionMock = Substitute.For<ICurrentUserSession>();
            var dialogServiceMock = Substitute.For<IVentaDialogService>();
            var inventarioServiceMock = Substitute.For<IInventarioService>();

            sessionMock.NombreCompleto.Returns("Administrador General");
            var viewModel = new PosViewModel(ventaServiceMock, cajaServiceMock, inventarioServiceMock, sessionMock, dialogServiceMock);

            // Con un ítem, la grilla genera la fila: cantidad editable y botón de eliminar con sus estilos
            viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
            {
                IdArticulo = 1,
                Descripcion = "Hoja A4",
                PrecioVenta = 50m,
                StockActual = 500,
                EsServicio = false
            });

            try
            {
                view = new PosView(viewModel);
                view.Measure(new System.Windows.Size(1280, 800));
                view.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de PosView debe resolverse sin errores");
        view.Should().NotBeNull();
        view!.ViewModel.Should().NotBeNull();
    }

    [Fact]
    public void PosView_DesplegableConResultadoSeleccionado_ResuelveElEstiloYLaPlantillaDeLaFila()
    {
        // Arrange: el contenido de un Popup solo se construye al abrirse, así que el smoke test anterior nunca
        // generaba las filas del desplegable. Acá se mide el contenido directamente para forzar su construcción.
        Exception? xamlException = null;
        bool filaGenerada = false;
        bool filaSeleccionada = false;

        WpfTestHelper.Run(() =>
        {
            var ventaServiceMock = Substitute.For<IVentaService>();
            var cajaServiceMock = Substitute.For<ICajaService>();
            var sessionMock = Substitute.For<ICurrentUserSession>();
            var dialogServiceMock = Substitute.For<IVentaDialogService>();
            var inventarioServiceMock = Substitute.For<IInventarioService>();
            var viewModel = new PosViewModel(ventaServiceMock, cajaServiceMock, inventarioServiceMock, sessionMock, dialogServiceMock);
            viewModel.ResultadosBusqueda.Add(new ArticuloVentaDto
            {
                IdArticulo = 1,
                CodigoBarras = "7790001",
                Descripcion = "Lápiz HB",
                PrecioVenta = 300m,
                StockActual = 5,
                EsServicio = false
            });
            viewModel.IndiceResultadoSeleccionado = 0;

            try
            {
                var view = new PosView(viewModel);
                var popup = (System.Windows.Controls.Primitives.Popup)view.FindName("PopupResultados");
                var lista = (System.Windows.Controls.ListBox)view.FindName("ListaResultados");
                var contenido = (System.Windows.FrameworkElement)popup.Child;

                contenido.Measure(new System.Windows.Size(600, 300));
                contenido.Arrange(new System.Windows.Rect(0, 0, 600, 300));
                contenido.UpdateLayout();

                // Los objetos de WPF solo se pueden leer desde su hilo STA
                var fila = lista.ItemContainerGenerator.ContainerFromIndex(0) as System.Windows.Controls.ListBoxItem;
                filaGenerada = fila != null;
                filaSeleccionada = fila?.IsSelected == true;
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el estilo y la plantilla de las filas del desplegable deben resolverse");
        filaGenerada.Should().BeTrue("la fila del resultado debe generarse con su contenedor");
        filaSeleccionada.Should().BeTrue();
    }

    [Fact]
    public void CobroModalDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        CobroModalDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var clienteEjemplo = new ClienteDto
            {
                IdCliente = 1,
                RazonSocialONombre = "Estudiante Universitario",
                TipoDocumento = TipoDocumentoEnum.Dni,
                NumeroDocumento = "40123456",
                CondicionIva = CondicionIvaEnum.ConsumidorFinal,
                TieneCuentaCorriente = true,
                LimiteCredito = 50000m,
                SaldoCuentaCorriente = 12000m
            };

            var viewModel = new CobroModalViewModel(8500m, clienteEjemplo);

            try
            {
                dialog = new CobroModalDialog(viewModel);
                dialog.Measure(new System.Windows.Size(560, 680));
                dialog.Arrange(new System.Windows.Rect(0, 0, 560, 680));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de CobroModalDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void SeleccionarClienteModalDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        SeleccionarClienteModalDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var clienteServiceMock = Substitute.For<IClienteService>();
            var viewModel = new SeleccionarClienteModalViewModel(clienteServiceMock);

            try
            {
                dialog = new SeleccionarClienteModalDialog(viewModel);
                dialog.Measure(new System.Windows.Size(520, 580));
                dialog.Arrange(new System.Windows.Rect(0, 0, 520, 580));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de SeleccionarClienteModalDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void ButtonHelper_PropiedadesAdjuntas_DebenAsignarseYLeerseCorrectamente()
    {
        WpfTestHelper.Run(() =>
        {
            // Arrange
            var button = new Button();

            // Act
            ButtonHelper.SetIsLoading(button, true);
            ButtonHelper.SetLoadingText(button, "Cargando datos...");

            // Assert
            ButtonHelper.GetIsLoading(button).Should().BeTrue();
            ButtonHelper.GetLoadingText(button).Should().Be("Cargando datos...");
        });
    }

    [Fact]
    public void LoginWindow_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        LoginWindow? window = null;

        WpfTestHelper.Run(() =>
        {
            var authServiceMock = Substitute.For<IAuthService>();
            var sessionMock = Substitute.For<ICurrentUserSession>();
            var loggerMock = Substitute.For<ILogger<LoginViewModel>>();
            var viewModel = new LoginViewModel(authServiceMock, Substitute.For<IUsuarioService>(), Substitute.For<IUsuarioDialogService>(), sessionMock, loggerMock);

            try
            {
                window = new LoginWindow(viewModel);
                window.Measure(new System.Windows.Size(440, 540));
                window.Arrange(new System.Windows.Rect(0, 0, 440, 540));
                window.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de LoginWindow debe resolverse sin errores de recursos o plantillas");
        window.Should().NotBeNull();
        window!.ViewModel.Should().NotBeNull();
    }

    [Fact]
    public void StyleGalleryView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        StyleGalleryView? view = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                view = new StyleGalleryView();
                view.Measure(new System.Windows.Size(1024, 1800));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 1800));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de StyleGalleryView debe resolverse sin errores");
        view.Should().NotBeNull();
    }

    [Fact]
    public void ProveedoresView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ProveedoresView? view = null;

        WpfTestHelper.Run(() =>
        {
            var proveedorServiceMock = Substitute.For<IProveedorService>();
            var dialogServiceMock = Substitute.For<IProveedorDialogService>();
            var loggerMock = Substitute.For<ILogger<ProveedoresViewModel>>();
            var viewModel = new ProveedoresViewModel(
                proveedorServiceMock,
                dialogServiceMock,
                loggerMock);

            try
            {
                view = new ProveedoresView(viewModel);
                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ProveedoresView debe resolverse sin errores");
        view.Should().NotBeNull();
        view!.ViewModel.Should().NotBeNull();
    }

    [Fact]
    public void ImportadorCatalogosView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ImportadorCatalogosView? view = null;

        WpfTestHelper.Run(() =>
        {
            var proveedorServiceMock = Substitute.For<IProveedorService>();
            var dialogServiceMock = Substitute.For<IProveedorDialogService>();
            var inventarioServiceMock = Substitute.For<IInventarioService>();
            var navigationServiceMock = Substitute.For<INavigationService>();
            var loggerMock = Substitute.For<ILogger<ImportadorCatalogosViewModel>>();
            var modalViewModel = new IncorporarArticulosModalViewModel(
                Substitute.For<IClasificacionService>(),
                Substitute.For<ITerminoClasificacionService>(),
                Substitute.For<IEquivalenciaClasificacionService>(),
                Substitute.For<IRubroService>(),
                Substitute.For<ICurrentUserSession>(),
                Substitute.For<ILogger<IncorporarArticulosModalViewModel>>());
            var viewModel = new ImportadorCatalogosViewModel(
                proveedorServiceMock,
                dialogServiceMock,
                inventarioServiceMock,
                navigationServiceMock,
                modalViewModel,
                TimeProvider.System,
                loggerMock);

            try
            {
                view = new ImportadorCatalogosView(viewModel);
                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ImportadorCatalogosView debe resolverse sin errores");
        view.Should().NotBeNull();
        view!.ViewModel.Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(AperturaTurnoDialog))]
    [InlineData(nameof(MovimientoCajaDialog))]
    [InlineData(nameof(ArqueoCiegoDialog))]
    public void DialogosDeCaja_InstanciacionEnHiloSTA_DebenCargarXAMLSinExcepciones(string nombreDialogo)
    {
        // Arrange
        Exception? xamlException = null;
        System.Windows.Window? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                dialog = nombreDialogo switch
                {
                    nameof(AperturaTurnoDialog) => new AperturaTurnoDialog(),
                    nameof(MovimientoCajaDialog) => new MovimientoCajaDialog(idTurno: 1),
                    _ => new ArqueoCiegoDialog(idTurno: 1)
                };
                dialog.Measure(new System.Windows.Size(520, 520));
                dialog.Arrange(new System.Windows.Rect(0, 0, 520, 520));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull($"el XAML de {nombreDialogo} debe resolver todos sus recursos");
        dialog.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void DiagnosticoArranqueDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones(int cantidadComandos)
    {
        // Arrange
        Exception? xamlException = null;
        string[] argumentos = cantidadComandos == 1 ? ["migrar"] : ["migrar", "inicializar"];
        var diagnostico = new DiagnosticoArranque(true, "La base no está lista.", argumentos);

        WpfTestHelper.Run(() =>
        {
            try
            {
                var dialog = new DiagnosticoArranqueDialog(diagnostico);
                dialog.Measure(new System.Windows.Size(680, 520));
                dialog.Arrange(new System.Windows.Rect(0, 0, 680, 520));
                dialog.UpdateLayout();

                // Los objetos de WPF solo se pueden leer desde su hilo STA
                dialog.MensajeMostrado.Should().Be("La base no está lista.");
                dialog.LineasMostradas.Should().HaveCount(cantidadComandos);
                dialog.CopiarTodosVisibility.Should().Be(
                    cantidadComandos > 1 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed);
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de DiagnosticoArranqueDialog debe resolverse sin errores");
    }

    [Fact]
    public void IncorporarArticulosModalDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        IncorporarArticulosModalDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                dialog = new IncorporarArticulosModalDialog();
                dialog.Measure(new System.Windows.Size(500, 450));
                dialog.Arrange(new System.Windows.Rect(0, 0, 500, 450));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de IncorporarArticulosModalDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void ImportarPlanillaDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ImportarPlanillaDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                dialog = new ImportarPlanillaDialog();
                dialog.Measure(new System.Windows.Size(540, 520));
                dialog.Arrange(new System.Windows.Rect(0, 0, 540, 520));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ImportarPlanillaDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void ProveedorFormDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ProveedorFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                dialog = new ProveedorFormDialog();
                dialog.Measure(new System.Windows.Size(520, 480));
                dialog.Arrange(new System.Windows.Rect(0, 0, 520, 480));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ProveedorFormDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void ClasificacionFormDialog_ModoCategoria_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ClasificacionFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                var vm = new ClasificacionFormViewModel(
                    Substitute.For<IRubroService>(),
                    Substitute.For<ICategoriaService>(),
                    Substitute.For<IMarcaService>(),
                    Substitute.For<ICurrentUserSession>());
                vm.ConfigurarAltaCategoria(new List<RubroDto> { new() { IdRubro = 1, NombreRubro = "Librería" } }, 1);

                dialog = new ClasificacionFormDialog { DataContext = vm };
                dialog.Measure(new System.Windows.Size(460, 390));
                dialog.Arrange(new System.Windows.Rect(0, 0, 460, 390));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ClasificacionFormDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void AdministrarClasificacionDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        AdministrarClasificacionDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                var vm = new AdministrarClasificacionViewModel(
                    Substitute.For<IRubroService>(),
                    Substitute.For<ICategoriaService>(),
                    Substitute.For<IMarcaService>(),
                    Substitute.For<ITerminoClasificacionService>(),
                    Substitute.For<IClasificacionDialogService>(),
                    Substitute.For<ICurrentUserSession>());
                vm.Rubros.Add(new RubroDto { IdRubro = 1, NombreRubro = "Librería", CantidadEnUso = 1 });
                vm.Categorias.Add(new CategoriaDto { IdCategoria = 1, NombreCategoria = "Cuadernos", IdRubro = 1, CantidadEnUso = 4 });
                vm.Marcas.Add(new MarcaDto { IdMarca = 1, NombreMarca = "Rivadavia", CantidadEnUso = 2 });
                vm.MensajeError = "Mensaje de prueba";

                dialog = new AdministrarClasificacionDialog { DataContext = vm };
                dialog.Measure(new System.Windows.Size(960, 640));
                dialog.Arrange(new System.Windows.Rect(0, 0, 960, 640));
                dialog.UpdateLayout();

                vm.MostrandoMarcas = true;
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de AdministrarClasificacionDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void VincularArticuloModalDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        VincularArticuloModalDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                dialog = new VincularArticuloModalDialog();
                dialog.Measure(new System.Windows.Size(700, 550));
                dialog.Arrange(new System.Windows.Rect(0, 0, 700, 550));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de VincularArticuloModalDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void SeleccionarCatalogoProveedorModalDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        SeleccionarCatalogoProveedorModalDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                dialog = new SeleccionarCatalogoProveedorModalDialog();
                dialog.Measure(new System.Windows.Size(750, 520));
                dialog.Arrange(new System.Windows.Rect(0, 0, 750, 520));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de SeleccionarCatalogoProveedorModalDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void ComprasView_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        ComprasView? view = null;

        WpfTestHelper.Run(() =>
        {
            var proveedorServiceMock = Substitute.For<IProveedorService>();
            var loggerMock = Substitute.For<ILogger<ComprasViewModel>>();
            var viewModel = new ComprasViewModel(proveedorServiceMock, loggerMock);

            try
            {
                view = new ComprasView(viewModel);
                viewModel.DetalleCompraItems.Add(new DetalleCompraItemViewModel
                {
                    IdArticulo = 1,
                    CodigoArticulo = "7791234567011",
                    DescripcionArticulo = "Cuaderno Rivadavia Tapa Dura",
                    CostoUnitario = 3200m,
                    Cantidad = 10,
                    PorcentajeGanancia = 50m
                });
                view.Measure(new System.Windows.Size(1024, 768));
                view.Arrange(new System.Windows.Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de ComprasView debe resolverse sin errores de recursos o controles");
        view.Should().NotBeNull();
        view!.ViewModel.Should().NotBeNull();
    }

    [Fact]
    public void ComprasView_InputBindings_DebenEstarAsociadosAComandosCorrespondientes()
    {
        // Arrange & Act
        WpfTestHelper.Run(() =>
        {
            var proveedorServiceMock = Substitute.For<IProveedorService>();
            var loggerMock = Substitute.For<ILogger<ComprasViewModel>>();
            var viewModel = new ComprasViewModel(proveedorServiceMock, loggerMock);

            var view = new ComprasView(viewModel);

            // Assert
            view.InputBindings.Count.Should().Be(2);

            var keyBindings = view.InputBindings.OfType<System.Windows.Input.KeyBinding>().ToList();
            keyBindings.Should().HaveCount(2);

            var bindingF2 = keyBindings.FirstOrDefault(b => b.Key == System.Windows.Input.Key.F2);
            bindingF2.Should().NotBeNull();
            bindingF2!.Command.Should().Be(viewModel.AgregarItemCommand);

            var bindingF12 = keyBindings.FirstOrDefault(b => b.Key == System.Windows.Input.Key.F12);
            bindingF12.Should().NotBeNull();
            bindingF12!.Command.Should().Be(viewModel.RegistrarCompraCommand);
        });
    }

    [Fact]
    public void EquivalenciasProveedorDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        EquivalenciasProveedorDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                var session = Substitute.For<ICurrentUserSession>();
                session.EsGerente.Returns(true);
                var vm = new EquivalenciasProveedorViewModel(
                    Substitute.For<IEquivalenciaClasificacionService>(),
                    Substitute.For<ICategoriaService>(),
                    Substitute.For<IMarcaService>(),
                    Substitute.For<IClasificacionDialogService>(),
                    session);
                vm.Configurar(new ProveedorDto { IdProveedor = 1, RazonSocial = "Distribuidora Norte", Cuit = "30-11111111-1" });
                vm.Equivalencias.Add(new EquivalenciaProveedorFila(new EquivalenciaDto
                {
                    Tipo = TipoEquivalenciaEnum.Categoria,
                    TextoRubro = "ESCOLAR",
                    Texto = "CUADERNOS",
                    IdCategoria = 1,
                    NombreCategoria = "Cuadernos"
                }));
                vm.MensajeError = "Mensaje de prueba";

                dialog = new EquivalenciasProveedorDialog { DataContext = vm };
                dialog.Measure(new System.Windows.Size(580, 660));
                dialog.Arrange(new System.Windows.Rect(0, 0, 580, 660));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de EquivalenciasProveedorDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void AdministrarClasificacionDialog_SeccionesDeDiccionarios_DebenCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;

        WpfTestHelper.Run(() =>
        {
            try
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
                var marcaService = Substitute.For<IMarcaService>();
                marcaService.ListarAliasAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyList<AliasListadoDto>>([]));
                marcaService.ListarPrefijosAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyList<PrefijoListadoDto>>([]));

                var vm = new AdministrarClasificacionViewModel(
                    Substitute.For<IRubroService>(),
                    categoriaService,
                    marcaService,
                    terminoService,
                    Substitute.For<IClasificacionDialogService>(),
                    Substitute.For<ICurrentUserSession>());
                var dialog = new AdministrarClasificacionDialog { DataContext = vm };

                var ambiguo = new TerminoDto { IdTermino = 1, Termino = "CUADERNO" };
                var categoria = new CategoriaDto { IdCategoria = 10, NombreCategoria = "Cuadernos", IdRubro = 1 };
                vm.Terminos.Terminos.Add(ambiguo);
                vm.Terminos.Terminos.Add(new TerminoDto { IdTermino = 2, Termino = "LAPIZ", IdCategoria = 10, NombreCategoria = "Lápices" });
                vm.Terminos.Pares.Add(new ParDto { IdTermino = 1, Termino = "CUADERNO", SegundaPalabra = "ESPIRAL" });
                vm.Terminos.TerminoSeleccionado = ambiguo;
                vm.Terminos.MensajeError = "Mensaje de prueba";

                dialog.Measure(new System.Windows.Size(960, 640));
                dialog.Arrange(new System.Windows.Rect(0, 0, 960, 640));
                dialog.UpdateLayout();

                vm.MostrandoTerminos = true;
                dialog.UpdateLayout();
                // Marcas con el detalle de la marca seleccionada (M-68): primero la sección y la selección, cuya
                // carga vacía el detalle, y después las filas a dibujar.
                var rivadavia = new MarcaDto { IdMarca = 1, NombreMarca = "Rivadavia" };
                vm.MostrandoMarcas = true;
                vm.Marcas.Add(rivadavia);
                vm.MarcaSeleccionada = rivadavia;
                vm.DetalleMarca.Alias.Add(new AliasListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Alias = "RIV" });
                vm.DetalleMarca.Prefijos.Add(new PrefijoListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Prefijo = "7790001" });
                dialog.UpdateLayout();
                // Pestaña "Pares propuestos" del término ambiguo (M-71): primero la pestaña, cuya carga vacía la
                // lista, y después las filas a dibujar.
                vm.MostrandoTerminos = true;
                vm.Terminos.MostrandoPropuestos = true;
                vm.Terminos.Propuestos.Categorias.Add(categoria);
                vm.Terminos.Propuestos.Propuestas.Add(new ParPropuestoItemViewModel(
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
                    categoria));
                vm.Terminos.Propuestos.MensajeError = "Mensaje de prueba";
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("las secciones de diccionarios de AdministrarClasificacionDialog deben resolverse sin errores");
    }

    [Fact]
    public void DiccionarioFormDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        DiccionarioFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                var vm = new DiccionarioFormViewModel(
                    "Nuevo término",
                    "Término *",
                    string.Empty,
                    textoEditable: true,
                    "Categoría",
                    [new OpcionDiccionario(null, "Ambiguo (sin categoría)"), new OpcionDiccionario(10, "Cuadernos")],
                    idOpcionInicial: null,
                    opcionObligatoria: false);
                vm.MensajeError = "Mensaje de prueba";

                dialog = new DiccionarioFormDialog { DataContext = vm };
                dialog.Measure(new System.Windows.Size(460, 400));
                dialog.Arrange(new System.Windows.Rect(0, 0, 460, 400));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de DiccionarioFormDialog debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void IncorporarArticulosModalDialog_ConSugerenciasYPasos_DebeResolverTodosLosRecursosEnCadaPaso()
    {
        // Arrange
        Exception? xamlException = null;
        IncorporarArticulosModalDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            try
            {
                var session = Substitute.For<ICurrentUserSession>();
                session.EsGerente.Returns(true);
                var viewModel = new IncorporarArticulosModalViewModel(
                    Substitute.For<IClasificacionService>(),
                    Substitute.For<ITerminoClasificacionService>(),
                    Substitute.For<IEquivalenciaClasificacionService>(),
                    Substitute.For<IRubroService>(),
                    session,
                    Substitute.For<ILogger<IncorporarArticulosModalViewModel>>());
                viewModel.Inicializar(
                    new[] { new CategoriaDto { IdCategoria = 1, NombreCategoria = "Librería" } },
                    new[] { new MarcaDto { IdMarca = 1, NombreMarca = "Bic" } },
                    new[]
                    {
                        new Retail.Application.DTOs.Proveedores.CatalogoProveedorDto
                        {
                            Id = 1,
                            IdProveedor = 3,
                            CodigoProveedor = "BIC-01",
                            CodigoBarras = "7791234567011",
                            DescripcionProveedor = "Bolígrafo Azul",
                            CostoReposicion = 1000m
                        }
                    });
                viewModel.PasoTraducirValores.Cargar(
                    3,
                    new[]
                    {
                        new Retail.Application.DTOs.Clasificacion.ValorSinTraducirDto
                        {
                            Tipo = TipoEquivalenciaEnum.Categoria,
                            TextoRubro = "LIBRERIA",
                            Texto = "ESCOLAR",
                            CantidadDeItems = 4
                        }
                    },
                    puedeAdministrar: true);
                viewModel.PasoTerminosNuevos.CargarAsync(
                    new[]
                    {
                        new Retail.Application.DTOs.Clasificacion.BloqueTerminoNuevoDto
                        {
                            Termino = "PELOTERO",
                            IdsCatalogo = new List<int> { 1 },
                            Ejemplos = new List<string> { "PELOTERO INFLABLE" }
                        }
                    },
                    puedeAdministrar: false).GetAwaiter().GetResult();
                viewModel.TienePasoTraducirValores = true;
                viewModel.TienePasoTerminosNuevos = true;

                dialog = new IncorporarArticulosModalDialog { DataContext = viewModel };

                // Cada paso y el modo clásico materializan plantillas distintas: se miden todos.
                foreach (var paso in new[]
                {
                    PasoIncorporacionEnum.TraducirValores,
                    PasoIncorporacionEnum.TerminosNuevos,
                    PasoIncorporacionEnum.RevisarArticulos
                })
                {
                    viewModel.PasoActual = paso;
                    dialog.Measure(new System.Windows.Size(1040, 680));
                    dialog.Arrange(new System.Windows.Rect(0, 0, 1040, 680));
                    dialog.UpdateLayout();
                }

                viewModel.SugerirClasificacion = false;
                dialog.Measure(new System.Windows.Size(1040, 680));
                dialog.Arrange(new System.Windows.Rect(0, 0, 1040, 680));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML del modal con sugerencias y pasos debe resolverse sin errores");
        dialog.Should().NotBeNull();
    }

    [Fact]
    public void PedidosView_InstanciacionEnHiloSTAConPedidoYDetalle_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        PedidosView? view = null;

        WpfTestHelper.Run(() =>
        {
            var reloj = new RelojDePrueba(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));
            var viewModel = new Retail.App.ViewModels.Pedidos.PedidosViewModel(
                Substitute.For<IPedidoService>(),
                Substitute.For<IProveedorService>(),
                Substitute.For<IPedidoDialogService>(),
                reloj,
                Substitute.For<ILogger<Retail.App.ViewModels.Pedidos.PedidosViewModel>>());

            try
            {
                view = new PedidosView(viewModel);

                // Una fila por estado, para que la plantilla del badge resuelva sus cuatro variantes.
                foreach (var estado in Enum.GetValues<EstadoPedidoEnum>())
                {
                    viewModel.Pedidos.Add(new Retail.App.ViewModels.Pedidos.PedidoFila(
                        new Retail.Application.DTOs.Pedidos.PedidoResumenDto
                        {
                            IdPedido = (int)estado,
                            RazonSocialProveedor = "Distribuidora Sur",
                            Estado = estado,
                            FechaCreacion = new DateTime(2026, 10, 20, 15, 0, 0),
                            CantidadRenglones = 2,
                            TotalEstimado = 4500m
                        },
                        reloj));
                }

                // El detalle de un borrador muestra la cantidad editable, el quitar y las acciones.
                viewModel.Detalle = new Retail.Application.DTOs.Pedidos.PedidoDetalleDto
                {
                    IdPedido = 1,
                    RazonSocialProveedor = "Distribuidora Sur",
                    Estado = EstadoPedidoEnum.Borrador,
                    FechaCreacion = new DateTime(2026, 10, 20, 15, 0, 0),
                    CantidadRenglones = 1,
                    TotalEstimado = 4500m
                };
                viewModel.Renglones.Add(new Retail.App.ViewModels.Pedidos.RenglonPedidoFila(
                    new Retail.Application.DTOs.Pedidos.RenglonPedidoDto
                    {
                        IdCatalogoProveedor = 3,
                        IdArticulo = 4,
                        CodigoProveedor = "CU-A4",
                        Descripcion = "Cuaderno A4 rayado",
                        Cantidad = 3,
                        CostoPactado = 1500m,
                        Subtotal = 4500m,
                        ArticuloDadoDeBaja = true
                    },
                    esEditable: true));

                view.Measure(new System.Windows.Size(1280, 800));
                view.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML de PedidosView debe resolver todos sus recursos (badges de estado, grillas y detalle)");
        view.Should().NotBeNull();
    }

    [Fact]
    public void PedirArticulosDialog_InstanciacionEnHiloSTAConGruposYOmitidos_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        PedirArticulosDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var vistaPrevia = new Retail.Application.DTOs.Pedidos.VistaPreviaPedidoDto
            {
                Destinos = new[]
                {
                    new Retail.Application.DTOs.Pedidos.DestinoPedidoDto { IdProveedor = 1, RazonSocialProveedor = "Distribuidora Sur", IdBorrador = 7, CantidadRenglonesBorrador = 2 },
                    new Retail.Application.DTOs.Pedidos.DestinoPedidoDto { IdProveedor = 2, RazonSocialProveedor = "Papelera Norte" }
                },
                Renglones = new[]
                {
                    new Retail.Application.DTOs.Pedidos.RenglonVistaPreviaDto { IdCatalogoProveedor = 10, IdProveedor = 1, CodigoProveedor = "CU-A4", Descripcion = "Cuaderno A4", CostoActual = 1500m, StockActual = 1, StockMinimo = 3, CantidadEnBorrador = 4 },
                    new Retail.Application.DTOs.Pedidos.RenglonVistaPreviaDto { IdCatalogoProveedor = 20, IdProveedor = 2, CodigoProveedor = "LAP-HB", Descripcion = "Lápiz HB", CostoActual = 200m, StockActual = 50, StockMinimo = 10 }
                },
                Omitidos = new[]
                {
                    new Retail.Application.DTOs.Pedidos.RenglonOmitidoDto { IdCatalogoProveedor = 30, CodigoProveedor = "GOM-1", Descripcion = "Goma", Motivo = Retail.Application.DTOs.Pedidos.MotivoOmisionPedidoEnum.SinIncorporar }
                }
            };

            try
            {
                dialog = new PedirArticulosDialog(new Retail.App.ViewModels.Pedidos.PedirArticulosViewModel(vistaPrevia));
                dialog.Measure(new System.Windows.Size(580, 660));
                dialog.Arrange(new System.Windows.Rect(0, 0, 580, 660));
                dialog.UpdateLayout();
            }
            catch (Exception ex)
            {
                xamlException = ex;
            }
        });

        // Assert
        xamlException.Should().BeNull("el XAML del modal de pedido debe resolver sus recursos y plantillas");
        dialog.Should().NotBeNull();
    }
}
