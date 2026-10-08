using FluentAssertions;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class PresentacionFormViewModelTests
{
    private readonly PresentacionFormViewModel _sut = new();

    [Fact]
    public void Configurar_ConOrigen_CargaDatosDeReferenciaYSugiereDescripcion()
    {
        // Act
        _sut.Configurar(Origen(costo: 1000m, ganancia: 40m, stock: 5));

        // Assert
        _sut.IdArticuloOrigen.Should().Be(7);
        _sut.DescripcionOrigen.Should().Be("Sobre manila (pack x100)");
        _sut.CostoOrigen.Should().Be(1000m);
        _sut.StockOrigen.Should().Be(5);
        _sut.Descripcion.Should().Be("Sobre manila (pack x100) (unidad)");
        _sut.PorcentajeGanancia.Should().Be(40m);
        _sut.UnidadesPorOrigen.Should().Be(1);
        _sut.CostoUnitarioCalculado.Should().Be(1000m);
    }

    [Fact]
    public void UnidadesPorOrigen_AlModificarse_RecalculaCostoUnitarioYPrecio()
    {
        // Arrange
        _sut.Configurar(Origen(costo: 1000m, ganancia: 40m));
        _sut.PorcentajeGanancia = 50m;

        // Act
        _sut.UnidadesPorOrigen = 100;

        // Assert
        _sut.CostoUnitarioCalculado.Should().Be(10m);
        _sut.PrecioVentaCalculado.Should().Be(15m);
    }

    [Fact]
    public void UnidadesPorOrigen_PuntoMedio_RedondeaComoElDominio()
    {
        // Arrange: 45 / 1000 = 0,045 → 0,05 (AwayFromZero, D-06)
        _sut.Configurar(Origen(costo: 45m, ganancia: 0m));

        // Act
        _sut.UnidadesPorOrigen = 1000;

        // Assert
        _sut.CostoUnitarioCalculado.Should().Be(0.05m);
    }

    [Fact]
    public void UnidadesPorOrigen_EnCero_MuestraCeroSinLanzar()
    {
        // Arrange
        _sut.Configurar(Origen(costo: 1000m, ganancia: 40m));

        // Act
        _sut.UnidadesPorOrigen = 0;

        // Assert
        _sut.CostoUnitarioCalculado.Should().Be(0m);
        _sut.PrecioVentaCalculado.Should().Be(0m);
    }

    [Fact]
    public void Validar_DatosValidos_RetornaTrueSinMensaje()
    {
        // Arrange
        _sut.Configurar(Origen());
        _sut.UnidadesPorOrigen = 100;

        // Act
        var valido = _sut.Validar();

        // Assert
        valido.Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
    }

    [Theory]
    [InlineData("", 100, 40, 0, "descripción")]
    [InlineData("Unidad", 0, 40, 0, "unidades por origen")]
    [InlineData("Unidad", 100, -1, 0, "porcentaje de ganancia")]
    [InlineData("Unidad", 100, 40, -1, "stock mínimo")]
    public void Validar_DatosInvalidos_RetornaFalseConMensaje(
        string descripcion,
        int unidades,
        int ganancia,
        int stockMinimo,
        string fragmentoMensaje)
    {
        // Arrange
        _sut.Configurar(Origen());
        _sut.Descripcion = descripcion;
        _sut.UnidadesPorOrigen = unidades;
        _sut.PorcentajeGanancia = ganancia;
        _sut.StockMinimo = stockMinimo;

        // Act
        var valido = _sut.Validar();

        // Assert
        valido.Should().BeFalse();
        _sut.MensajeError.Should().Contain(fragmentoMensaje);
    }

    [Fact]
    public void ObtenerDto_DatosCargados_NormalizaTextoYMapeaCampos()
    {
        // Arrange
        _sut.Configurar(Origen());
        _sut.Descripcion = "  Sobre manila (unidad)  ";
        _sut.CodigoBarras = "   ";
        _sut.UnidadesPorOrigen = 100;
        _sut.PorcentajeGanancia = 50m;
        _sut.StockMinimo = 10;

        // Act
        var dto = _sut.ObtenerDto();

        // Assert
        dto.IdArticuloOrigen.Should().Be(7);
        dto.Descripcion.Should().Be("Sobre manila (unidad)");
        dto.CodigoBarras.Should().BeNull();
        dto.UnidadesPorOrigen.Should().Be(100);
        dto.PorcentajeGanancia.Should().Be(50m);
        dto.StockMinimo.Should().Be(10);
    }

    private static ArticuloDto Origen(decimal costo = 1000m, decimal ganancia = 40m, int stock = 5)
    {
        return new ArticuloDto
        {
            IdArticulo = 7,
            Descripcion = "Sobre manila (pack x100)",
            CostoReposicion = costo,
            PorcentajeGanancia = ganancia,
            PrecioVenta = 0m,
            StockActual = stock,
            StockMinimo = 1,
            EsServicio = false
        };
    }
}
