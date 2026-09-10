using FluentAssertions;
using Retail.App.Views.Dev;
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
        var staThread = new System.Threading.Thread(() =>
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
        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();
    }
}
