using FluentAssertions;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class FraccionarViewModelTests
{
    private readonly FraccionarViewModel _sut = new();

    [Fact]
    public void Configurar_ConDerivadoYOrigen_MuestraVistaPreviaDeUnaUnidad()
    {
        // Act
        _sut.Configurar(Derivado(stock: 3), Origen(stock: 5));

        // Assert
        _sut.CantidadOrigen.Should().Be(1);
        _sut.UnidadesResultantes.Should().Be(100);
        _sut.StockOrigenResultante.Should().Be(4);
        _sut.StockDerivadoResultante.Should().Be(103);
    }

    [Fact]
    public void CantidadOrigen_AlModificarse_ActualizaLaVistaPreviaYNotifica()
    {
        // Arrange
        _sut.Configurar(Derivado(stock: 3), Origen(stock: 5));
        var notificadas = new List<string?>();
        _sut.PropertyChanged += (_, e) => notificadas.Add(e.PropertyName);

        // Act
        _sut.CantidadOrigen = 2;

        // Assert
        _sut.UnidadesResultantes.Should().Be(200);
        _sut.StockOrigenResultante.Should().Be(3);
        _sut.StockDerivadoResultante.Should().Be(203);
        notificadas.Should().Contain(new[]
        {
            nameof(FraccionarViewModel.UnidadesResultantes),
            nameof(FraccionarViewModel.StockOrigenResultante),
            nameof(FraccionarViewModel.StockDerivadoResultante)
        });
    }

    [Fact]
    public void Validar_CantidadDentroDelStock_RetornaTrue()
    {
        // Arrange
        _sut.Configurar(Derivado(), Origen(stock: 5));
        _sut.CantidadOrigen = 5;

        // Act + Assert
        _sut.Validar().Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validar_CantidadMenorAUno_RetornaFalseConMensaje(int cantidad)
    {
        // Arrange
        _sut.Configurar(Derivado(), Origen(stock: 5));
        _sut.CantidadOrigen = cantidad;

        // Act + Assert
        _sut.Validar().Should().BeFalse();
        _sut.MensajeError.Should().Contain("al menos 1");
        _sut.UnidadesResultantes.Should().Be(0);
    }

    [Fact]
    public void Validar_CantidadMayorAlStockDelOrigen_RetornaFalseConMensaje()
    {
        // Arrange
        _sut.Configurar(Derivado(), Origen(stock: 2));
        _sut.CantidadOrigen = 3;

        // Act + Assert
        _sut.Validar().Should().BeFalse();
        _sut.MensajeError.Should().Contain("2 unidad(es)");
    }

    [Fact]
    public void ObtenerDto_DatosCargados_IdentificaAlDerivado()
    {
        // Arrange
        _sut.Configurar(Derivado(), Origen(stock: 5));
        _sut.CantidadOrigen = 2;

        // Act
        var dto = _sut.ObtenerDto();

        // Assert
        dto.IdArticuloDerivado.Should().Be(8);
        dto.CantidadOrigen.Should().Be(2);
    }

    private static ArticuloDto Derivado(int stock = 0)
    {
        return new ArticuloDto
        {
            IdArticulo = 8,
            Descripcion = "Sobre manila (unidad)",
            IdArticuloOrigen = 7,
            UnidadesPorOrigen = 100,
            CostoReposicion = 10m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 15m,
            StockActual = stock,
            StockMinimo = 0,
            EsServicio = false
        };
    }

    private static ArticuloDto Origen(int stock)
    {
        return new ArticuloDto
        {
            IdArticulo = 7,
            Descripcion = "Sobre manila (pack x100)",
            CostoReposicion = 1000m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 1400m,
            StockActual = stock,
            StockMinimo = 1,
            EsServicio = false
        };
    }
}
