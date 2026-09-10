using FluentAssertions;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Articulos;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ArticuloValidatorTests
{
    private readonly CrearArticuloValidator _crearValidator = new();
    private readonly ActualizarArticuloValidator _actualizarValidator = new();

    [Fact]
    public void CrearArticuloValidator_DatosValidos_DebePasarValidacion()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            CodigoBarras = "7791234567890",
            Descripcion = "Cuaderno Tapa Dura",
            IdCategoria = 1,
            IdMarca = 2,
            CostoReposicion = 1500m,
            PorcentajeGanancia = 45m,
            StockActual = 20,
            StockMinimo = 5,
            EsServicio = false
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearArticuloValidator_DescripcionVacia_DebeFallar(string descripcionInvalida)
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            CodigoBarras = null,
            Descripcion = descripcionInvalida,
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 100m,
            PorcentajeGanancia = 20m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearArticuloDto.Descripcion));
    }

    [Fact]
    public void CrearArticuloValidator_CostoReposicionNegativo_DebeFallar()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            Descripcion = "Válido",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = -50m,
            PorcentajeGanancia = 20m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearArticuloDto.CostoReposicion));
    }

    [Fact]
    public void CrearArticuloValidator_PorcentajeGananciaNegativo_DebeFallar()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            Descripcion = "Válido",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 100m,
            PorcentajeGanancia = -10m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearArticuloDto.PorcentajeGanancia));
    }

    [Fact]
    public void CrearArticuloValidator_EsServicio_ConStockCero_DebePasar()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            Descripcion = "Fotocopia Simple",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 10m,
            PorcentajeGanancia = 100m,
            StockActual = 0,
            StockMinimo = 0,
            EsServicio = true
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ActualizarArticuloValidator_IdArticuloInvalido_DebeFallar()
    {
        // Arrange
        var dto = new ActualizarArticuloDto
        {
            IdArticulo = 0,
            Descripcion = "Válido",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 100m,
            PorcentajeGanancia = 20m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        // Act
        var result = _actualizarValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ActualizarArticuloDto.IdArticulo));
    }

    [Fact]
    public void CrearArticuloValidator_CategoriaYMarcaNulas_DebePasarValidacion()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            Descripcion = "Artículo Sin Categoria Ni Marca",
            IdCategoria = null,
            IdMarca = null,
            CostoReposicion = 200m,
            PorcentajeGanancia = 30m,
            StockActual = 5,
            StockMinimo = 1,
            EsServicio = false
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CrearArticuloValidator_IdCategoriaInvalido_DebeFallar(int idInvalido)
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            Descripcion = "Artículo Con Categoria Invalida",
            IdCategoria = idInvalido,
            IdMarca = null,
            CostoReposicion = 200m,
            PorcentajeGanancia = 30m,
            StockActual = 5,
            StockMinimo = 1,
            EsServicio = false
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearArticuloDto.IdCategoria));
    }
}
