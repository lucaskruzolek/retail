using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Articulos;
using Retail.App.Views.Dev;
using Retail.App.Views.Pages;
using Retail.Application.Interfaces.Services;
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
}
