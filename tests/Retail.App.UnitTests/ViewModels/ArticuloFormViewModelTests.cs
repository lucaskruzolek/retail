using FluentAssertions;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ArticuloFormViewModelTests
{
    private readonly ArticuloFormViewModel _sut = new();

    private readonly List<CategoriaDto> _categorias =
    [
        new() { IdCategoria = 1, NombreCategoria = "Escolar" },
        new() { IdCategoria = 2, NombreCategoria = "Comercial" }
    ];

    private readonly List<MarcaDto> _marcas =
    [
        new() { IdMarca = 1, NombreMarca = "Rivadavia" },
        new() { IdMarca = 2, NombreMarca = "Bic" }
    ];

    [Fact]
    public void RecalcularPrecioVenta_AlModificarCosto_ActualizaPrecioVentaCalculado()
    {
        // Arrange
        _sut.CostoReposicion = 1000m;
        _sut.PorcentajeGanancia = 50m;

        // Assert
        _sut.PrecioVentaCalculado.Should().Be(1500m);

        // Act - modificar costo reactivamente
        _sut.CostoReposicion = 2000m;

        // Assert
        _sut.PrecioVentaCalculado.Should().Be(3000m);
    }

    [Fact]
    public void RecalcularPrecioVenta_AlModificarPorcentajeGanancia_ActualizaPrecioVentaCalculado()
    {
        // Arrange
        _sut.CostoReposicion = 1000m;
        _sut.PorcentajeGanancia = 25m;

        // Assert
        _sut.PrecioVentaCalculado.Should().Be(1250m);

        // Act - modificar margen reactivamente
        _sut.PorcentajeGanancia = 40m;

        // Assert
        _sut.PrecioVentaCalculado.Should().Be(1400m);
    }

    [Fact]
    public void EsServicio_CuandoSeMarcaTrue_ReseteaStockActualYMinimo()
    {
        // Arrange
        _sut.StockActual = 20;
        _sut.StockMinimo = 5;

        // Act
        _sut.EsServicio = true;

        // Assert
        _sut.StockActual.Should().Be(0);
        _sut.StockMinimo.Should().Be(0);
        _sut.ManejaStockFisico.Should().BeFalse();
    }

    [Fact]
    public void Validar_DatosValidos_RetornaTrueYSinError()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.Descripcion = "Cuaderno 80 Hojas";
        _sut.IdCategoria = 1;
        _sut.IdMarca = 1;
        _sut.CostoReposicion = 500m;
        _sut.PorcentajeGanancia = 40m;
        _sut.StockActual = 15;
        _sut.StockMinimo = 5;
        _sut.EsServicio = false;

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
        _sut.MensajeError.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public void Validar_DescripcionInvalida_RetornaFalseYError(string descInvalida)
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.Descripcion = descInvalida;

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("al menos 2 caracteres");
    }

    [Fact]
    public void Validar_CostoNegativo_RetornaFalseYError()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.Descripcion = "Artículo Válido";
        _sut.CostoReposicion = -10m;

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("costo de reposición no puede ser negativo");
    }

    [Fact]
    public void Validar_PorcentajeGananciaNegativo_RetornaFalseYError()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.Descripcion = "Artículo Válido";
        _sut.PorcentajeGanancia = -5m;

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("porcentaje de ganancia no puede ser negativo");
    }

    [Fact]
    public void ConfigurarEdicion_ConArticuloExistente_CargaDatosYRecalcula()
    {
        // Arrange
        var articulo = new ArticuloDto
        {
            IdArticulo = 42,
            CodigoBarras = "7791234567890",
            Descripcion = "Carpeta A4",
            IdCategoria = 2,
            IdMarca = 2,
            CostoReposicion = 800m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 1200m,
            StockActual = 12,
            StockMinimo = 3,
            EsServicio = false
        };

        // Act
        _sut.ConfigurarEdicion(articulo, _categorias, _marcas);

        // Assert
        _sut.EsModoEdicion.Should().BeTrue();
        _sut.IdArticulo.Should().Be(42);
        _sut.CodigoBarras.Should().Be("7791234567890");
        _sut.Descripcion.Should().Be("Carpeta A4");
        _sut.IdCategoria.Should().Be(2);
        _sut.IdMarca.Should().Be(2);
        _sut.CostoReposicion.Should().Be(800m);
        _sut.PorcentajeGanancia.Should().Be(50m);
        _sut.PrecioVentaCalculado.Should().Be(1200m);
        _sut.StockActual.Should().Be(12);
        _sut.StockMinimo.Should().Be(3);
    }

    [Fact]
    public void ObtenerCrearDto_RetornaDtoCorrectamente()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.CodigoBarras = " 7799999999999 ";
        _sut.Descripcion = " Goma de Borrar ";
        _sut.IdCategoria = 1;
        _sut.IdMarca = 1;
        _sut.CostoReposicion = 100m;
        _sut.PorcentajeGanancia = 60m;
        _sut.StockActual = 30;
        _sut.StockMinimo = 5;
        _sut.EsServicio = false;

        // Act
        var dto = _sut.ObtenerCrearDto();

        // Assert
        dto.CodigoBarras.Should().Be("7799999999999");
        dto.Descripcion.Should().Be("Goma de Borrar");
        dto.CostoReposicion.Should().Be(100m);
        dto.PorcentajeGanancia.Should().Be(60m);
        dto.StockActual.Should().Be(30);
    }

    [Fact]
    public void ConfigurarAlta_InicializaConNingunoPorDefecto()
    {
        // Act
        _sut.ConfigurarAlta(_categorias, _marcas);

        // Assert
        _sut.Categorias.Should().HaveCount(3);
        _sut.Categorias[0].IdCategoria.Should().Be(0);
        _sut.Categorias[0].NombreCategoria.Should().Be("(Ninguna)");
        _sut.IdCategoria.Should().Be(0);

        _sut.Marcas.Should().HaveCount(3);
        _sut.Marcas[0].IdMarca.Should().Be(0);
        _sut.Marcas[0].NombreMarca.Should().Be("(Ninguno/a)");
        _sut.IdMarca.Should().Be(0);
    }

    [Fact]
    public void Validar_SinCategoriaNiMarca_RetornaTrueYSinError()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.Descripcion = "Servicio Especial";
        _sut.IdCategoria = 0;
        _sut.IdMarca = 0;
        _sut.EsServicio = true;

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
    }

    [Fact]
    public void ObtenerCrearDto_ConNingunaCategoriaNiMarca_RetornaDtoConValoresNulos()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.Descripcion = "Producto Sin Rubro Ni Marca";
        _sut.IdCategoria = 0;
        _sut.IdMarca = 0;

        // Act
        var dto = _sut.ObtenerCrearDto();

        // Assert
        dto.IdCategoria.Should().BeNull();
        dto.IdMarca.Should().BeNull();
    }
}
