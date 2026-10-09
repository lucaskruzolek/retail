using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Retail.Domain.Services;
using Xunit;

namespace Retail.Domain.UnitTests.Services;

public class ServicioFraccionamientoTests
{
    [Fact]
    public void Fraccionar_StockSuficiente_DescuentaOrigenEIncrementaDerivado()
    {
        // Arrange
        var (origen, derivado) = CrearPackYUnidad(stockOrigen: 5, stockDerivado: 3, unidadesPorOrigen: 100);

        // Act
        var unidadesObtenidas = ServicioFraccionamiento.Fraccionar(origen, derivado, 2);

        // Assert
        unidadesObtenidas.Should().Be(200);
        origen.StockActual.Should().Be(3);
        derivado.StockActual.Should().Be(203);
    }

    [Fact]
    public void Fraccionar_StockInsuficiente_NoModificaNingunStock()
    {
        // Arrange
        var (origen, derivado) = CrearPackYUnidad(stockOrigen: 1, stockDerivado: 3, unidadesPorOrigen: 100);

        // Act
        var act = () => ServicioFraccionamiento.Fraccionar(origen, derivado, 2);

        // Assert
        act.Should().Throw<StockInsuficienteException>();
        origen.StockActual.Should().Be(1);
        derivado.StockActual.Should().Be(3);
    }

    [Fact]
    public void Fraccionar_DerivadoDeOtroOrigen_LanzaDomainException()
    {
        // Arrange
        var (_, derivado) = CrearPackYUnidad(stockOrigen: 5, stockDerivado: 0, unidadesPorOrigen: 10);
        var otroOrigen = new Articulo { Id = 99, Descripcion = "Otro pack", StockActual = 5 };

        // Act
        var act = () => ServicioFraccionamiento.Fraccionar(otroOrigen, derivado, 1);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*no es una presentación derivada*");
        otroOrigen.StockActual.Should().Be(5);
        derivado.StockActual.Should().Be(0);
    }

    [Fact]
    public void Fraccionar_ArticuloNoDerivado_LanzaDomainException()
    {
        // Arrange
        var origen = new Articulo { Id = 1, Descripcion = "Pack", StockActual = 5 };
        var noDerivado = new Articulo { Id = 2, Descripcion = "Artículo suelto" };

        // Act
        var act = () => ServicioFraccionamiento.Fraccionar(origen, noDerivado, 1);

        // Assert
        act.Should().Throw<DomainException>();
        origen.StockActual.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Fraccionar_CantidadMenorAUno_LanzaArgumentOutOfRangeException(int cantidadOrigen)
    {
        // Arrange
        var (origen, derivado) = CrearPackYUnidad(stockOrigen: 5, stockDerivado: 0, unidadesPorOrigen: 10);

        // Act
        var act = () => ServicioFraccionamiento.Fraccionar(origen, derivado, cantidadOrigen);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        origen.StockActual.Should().Be(5);
    }

    [Fact]
    public void Fraccionar_ArticulosNulos_LanzaArgumentNullException()
    {
        // Arrange
        var (origen, derivado) = CrearPackYUnidad(stockOrigen: 5, stockDerivado: 0, unidadesPorOrigen: 10);

        // Act
        var sinOrigen = () => ServicioFraccionamiento.Fraccionar(null!, derivado, 1);
        var sinDerivado = () => ServicioFraccionamiento.Fraccionar(origen, null!, 1);

        // Assert
        sinOrigen.Should().Throw<ArgumentNullException>();
        sinDerivado.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Fraccionar_DesbordeEnElDerivado_NoModificaNingunStock()
    {
        // Arrange
        var (origen, derivado) = CrearPackYUnidad(stockOrigen: 5, stockDerivado: int.MaxValue - 5, unidadesPorOrigen: 10);

        // Act
        var act = () => ServicioFraccionamiento.Fraccionar(origen, derivado, 1);

        // Assert
        act.Should().Throw<OverflowException>();
        origen.StockActual.Should().Be(5);
        derivado.StockActual.Should().Be(int.MaxValue - 5);
    }

    [Theory]
    [InlineData(3, 100, 1)]
    [InlineData(100, 100, 1)]
    [InlineData(101, 100, 2)]
    [InlineData(7, 6, 2)]
    [InlineData(5, 1, 5)]
    public void CalcularOrigenesNecesarios_UnidadesFaltantes_RedondeaHaciaArriba(int faltantes, int unidadesPorOrigen, int esperado)
    {
        // Act
        var origenes = ServicioFraccionamiento.CalcularOrigenesNecesarios(faltantes, unidadesPorOrigen);

        // Assert
        origenes.Should().Be(esperado);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(3, 0)]
    public void CalcularOrigenesNecesarios_ValoresMenoresAUno_LanzaArgumentOutOfRangeException(int faltantes, int unidadesPorOrigen)
    {
        // Act
        var act = () => ServicioFraccionamiento.CalcularOrigenesNecesarios(faltantes, unidadesPorOrigen);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static (Articulo Origen, Articulo Derivado) CrearPackYUnidad(int stockOrigen, int stockDerivado, int unidadesPorOrigen)
    {
        var origen = new Articulo
        {
            Id = 1,
            Descripcion = "Sobre manila (pack)",
            CostoReposicion = 1000m,
            PorcentajeGanancia = 40m,
            StockActual = stockOrigen
        };

        var derivado = new Articulo
        {
            Id = 2,
            Descripcion = "Sobre manila (unidad)",
            PorcentajeGanancia = 100m,
            StockActual = stockDerivado
        };
        derivado.DefinirComoPresentacionDe(origen, unidadesPorOrigen);

        return (origen, derivado);
    }
}
