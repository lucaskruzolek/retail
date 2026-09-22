using FluentAssertions;
using Retail.Domain.Entities;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class ArticuloTests
{
    [Theory]
    [InlineData(100.0, 50.0, 150.0)]
    [InlineData(2500.50, 30.0, 3250.65)]
    [InlineData(1000.0, 0.0, 1000.0)]
    public void CalcularPrecioVenta_ValoresValidos_DebeCalcularPrecioCorrectamente(
        decimal costo,
        decimal porcentajeGanancia,
        decimal precioEsperado)
    {
        // Act
        var resultado = Articulo.CalcularPrecioVenta(costo, porcentajeGanancia);

        // Assert
        resultado.Should().Be(precioEsperado);
    }

    [Fact]
    public void CalcularPrecioVenta_CostoNegativo_DebeLanzarArgumentOutOfRangeException()
    {
        // Act
        var act = () => Articulo.CalcularPrecioVenta(-10m, 30m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*costo de reposición*");
    }

    [Fact]
    public void CalcularPrecioVenta_PorcentajeGananciaNegativo_DebeLanzarArgumentOutOfRangeException()
    {
        // Act
        var act = () => Articulo.CalcularPrecioVenta(100m, -5m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*porcentaje de ganancia*");
    }

    [Fact]
    public void ActualizarCostoYRecalcularPrecio_DebeActualizarCostoYPrecioVenta()
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Cuaderno Rivadavia 100H",
            CostoReposicion = 1000m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 1400m
        };

        // Act
        articulo.ActualizarCostoYRecalcularPrecio(1500m);

        // Assert
        articulo.CostoReposicion.Should().Be(1500m);
        articulo.PrecioVenta.Should().Be(2100m);
    }

    [Theory]
    [InlineData(2, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    public void TieneStockBajo_ArticuloFisico_DebeEvaluarSegunStockMinimo(
        int stockActual,
        int stockMinimo,
        bool esperadoCritico)
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Regla 30cm",
            EsServicio = false,
            StockActual = stockActual,
            StockMinimo = stockMinimo
        };

        // Assert
        articulo.TieneStockBajo.Should().Be(esperadoCritico);
    }

    [Fact]
    public void TieneStockBajo_EsServicio_NuncaDebeIndicarCritico()
    {
        // Arrange
        var servicio = new Articulo
        {
            Descripcion = "Servicio de Plastificado A4",
            EsServicio = true,
            StockActual = 0,
            StockMinimo = 0
        };

        // Assert
        servicio.TieneStockBajo.Should().BeFalse();
    }

    [Fact]
    public void ActualizarDatos_ValoresValidos_DebeActualizarCamposCorrectamente()
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Original",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        // Act
        articulo.ActualizarDatos(
            descripcion: "Modificado",
            idCategoria: 2,
            idMarca: 3,
            codigoBarras: "7791234567890",
            costoReposicion: 200m,
            porcentajeGanancia: 25m,
            stockActual: 15,
            stockMinimo: 5,
            esServicio: false);

        // Assert
        articulo.Descripcion.Should().Be("Modificado");
        articulo.IdCategoria.Should().Be(2);
        articulo.IdMarca.Should().Be(3);
        articulo.CodigoBarras.Should().Be("7791234567890");
        articulo.CostoReposicion.Should().Be(200m);
        articulo.PorcentajeGanancia.Should().Be(25m);
        articulo.PrecioVenta.Should().Be(250m);
        articulo.StockActual.Should().Be(15);
        articulo.StockMinimo.Should().Be(5);
    }

    [Fact]
    public void ActualizarDatos_ArticuloArtesanal_PermiteCodigoBarrasNulo()
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Original" };

        // Act
        articulo.ActualizarDatos(
            descripcion: "Cuenco Artesanal de Barro",
            idCategoria: 1,
            idMarca: 1,
            codigoBarras: null,
            costoReposicion: 500m,
            porcentajeGanancia: 60m,
            stockActual: 4,
            stockMinimo: 1,
            esServicio: false);

        // Assert
        articulo.CodigoBarras.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ActualizarDatos_DescripcionInvalida_DebeLanzarArgumentException(string descripcionInvalida)
    {
        // Arrange
        var articulo = new Articulo();

        // Act
        var act = () => articulo.ActualizarDatos(
            descripcion: descripcionInvalida,
            idCategoria: 1,
            idMarca: 1,
            codigoBarras: null,
            costoReposicion: 100m,
            porcentajeGanancia: 50m,
            stockActual: 10,
            stockMinimo: 2,
            esServicio: false);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ActualizarDatos_CategoriaYMarcaNulas_DebePermitirValoresNulos()
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Original" };

        // Act
        articulo.ActualizarDatos(
            descripcion: "Artículo Sin Rubro Ni Marca",
            idCategoria: null,
            idMarca: null,
            codigoBarras: null,
            costoReposicion: 100m,
            porcentajeGanancia: 50m,
            stockActual: 5,
            stockMinimo: 1,
            esServicio: false);

        // Assert
        articulo.IdCategoria.Should().BeNull();
        articulo.IdMarca.Should().BeNull();
    }

    [Fact]
    public void VincularCatalogoProveedor_IdInvalido_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Lapicera Azul", CostoReposicion = 100m, PorcentajeGanancia = 50m };

        // Act
        var act = () => articulo.VincularCatalogoProveedor(0, 120m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*identificador de catálogo*");
    }

    [Fact]
    public void VincularCatalogoProveedor_CostoNegativo_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Lapicera Azul", CostoReposicion = 100m, PorcentajeGanancia = 50m };

        // Act
        var act = () => articulo.VincularCatalogoProveedor(10, -5m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*costo*");
    }

    [Fact]
    public void VincularCatalogoProveedor_DatosValidos_ActualizaIdCostoYPrecioVenta()
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Cuaderno Rivadavia",
            CostoReposicion = 1000m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 1500m
        };

        // Act
        articulo.VincularCatalogoProveedor(42, 1200m);

        // Assert
        articulo.IdCatalogoProveedor.Should().Be(42);
        articulo.CostoReposicion.Should().Be(1200m);
        articulo.PrecioVenta.Should().Be(1800m); // 1200 + 50%
    }

    [Fact]
    public void DesvincularCatalogoProveedor_ArticuloPreviamenteVinculado_RemueveEnlaceYPasaAManual()
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Resma A4",
            IdCatalogoProveedor = 15,
            CostoReposicion = 5000m,
            PorcentajeGanancia = 30m,
            PrecioVenta = 6500m
        };

        // Act
        articulo.DesvincularCatalogoProveedor();

        // Assert
        articulo.IdCatalogoProveedor.Should().BeNull();
        articulo.CostoReposicion.Should().Be(5000m); // Preserva el último costo
        articulo.PrecioVenta.Should().Be(6500m);
    }
}
