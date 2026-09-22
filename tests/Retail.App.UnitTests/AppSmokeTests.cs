using System.Windows.Controls;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Auth;
using Retail.App.ViewModels.Clientes;
using Retail.App.ViewModels.Proveedores;
using Retail.App.ViewModels.Usuarios;
using Retail.App.ViewModels.Ventas;
using Retail.App.Views.Auth;
using Retail.App.Views.Dev;
using Retail.App.Views.Dialogs;
using Retail.App.Views.Pages;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clientes;
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
            view.InputBindings.Count.Should().Be(3);

            var keyBindings = view.InputBindings.OfType<System.Windows.Input.KeyBinding>().ToList();
            keyBindings.Should().HaveCount(3);

            var bindingF2 = keyBindings.FirstOrDefault(b => b.Key == System.Windows.Input.Key.F2);
            bindingF2.Should().NotBeNull();
            bindingF2!.Command.Should().Be(viewModel.NuevoArticuloCommand);

            var bindingF3 = keyBindings.FirstOrDefault(b => b.Key == System.Windows.Input.Key.F3);
            bindingF3.Should().NotBeNull();
            bindingF3!.Command.Should().Be(viewModel.AlternarSoloStockCriticoCommand);

            var bindingF5 = keyBindings.FirstOrDefault(b => b.Key == System.Windows.Input.Key.F5);
            bindingF5.Should().NotBeNull();
            bindingF5!.Command.Should().Be(viewModel.CargarArticulosCommand);
        });
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
            var viewModel = new UsuariosViewModel(usuarioServiceMock, dialogServiceMock);

            try
            {
                view = new UsuariosView(viewModel);
                viewModel.Usuarios.Add(new UsuarioDto
                {
                    IdUsuario = 1,
                    NombreUsuario = "admin",
                    NombreCompleto = "Administrador General",
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
    public void UsuarioFormDialog_InstanciacionEnHiloSTA_DebeCargarXAMLSinExcepciones()
    {
        // Arrange
        Exception? xamlException = null;
        UsuarioFormDialog? dialog = null;

        WpfTestHelper.Run(() =>
        {
            var viewModel = new UsuarioFormViewModel();
            viewModel.ConfigurarAlta();

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
                NombreCompleto = "Juan Pérez",
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

            sessionMock.NombreCompleto.Returns("Administrador General");
            var viewModel = new PosViewModel(ventaServiceMock, cajaServiceMock, sessionMock, dialogServiceMock);

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
            var viewModel = new LoginViewModel(authServiceMock, sessionMock, loggerMock);

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
            var viewModel = new ImportadorCatalogosViewModel(
                proveedorServiceMock,
                dialogServiceMock,
                inventarioServiceMock,
                navigationServiceMock,
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
}
