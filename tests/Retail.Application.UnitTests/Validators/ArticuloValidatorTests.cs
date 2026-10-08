using FluentAssertions;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Validators.Articulos;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class ArticuloValidatorTests
{
    private readonly CrearArticuloValidator _crearValidator = new();
    private readonly ActualizarArticuloValidator _actualizarValidator = new();
    private readonly CrearPresentacionValidator _crearPresentacionValidator = new();
    private readonly FraccionarValidator _fraccionarValidator = new();

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

    [Fact]
    public void CrearPresentacionValidator_DatosValidos_DebePasarValidacion()
    {
        // Act
        var result = _crearPresentacionValidator.Validate(PresentacionValida());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CrearPresentacionValidator_UnidadesMenoresAUno_DebeFallar(int unidades)
    {
        // Act
        var result = _crearPresentacionValidator.Validate(PresentacionValida() with { UnidadesPorOrigen = unidades });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPresentacionDto.UnidadesPorOrigen));
    }

    [Fact]
    public void CrearPresentacionValidator_OrigenInvalido_DebeFallar()
    {
        // Act
        var result = _crearPresentacionValidator.Validate(PresentacionValida() with { IdArticuloOrigen = 0 });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPresentacionDto.IdArticuloOrigen));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearPresentacionValidator_DescripcionVacia_DebeFallar(string descripcion)
    {
        // Act
        var result = _crearPresentacionValidator.Validate(PresentacionValida() with { Descripcion = descripcion });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPresentacionDto.Descripcion));
    }

    [Fact]
    public void CrearPresentacionValidator_GananciaYStockMinimoNegativos_DebeFallar()
    {
        // Act
        var result = _crearPresentacionValidator.Validate(PresentacionValida() with { PorcentajeGanancia = -1m, StockMinimo = -1 });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPresentacionDto.PorcentajeGanancia));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPresentacionDto.StockMinimo));
    }

    [Fact]
    public void FraccionarValidator_DatosValidos_DebePasarValidacion()
    {
        // Act
        var result = _fraccionarValidator.Validate(new FraccionarDto { IdArticuloDerivado = 2, CantidadOrigen = 1 });

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 1, nameof(FraccionarDto.IdArticuloDerivado))]
    [InlineData(2, 0, nameof(FraccionarDto.CantidadOrigen))]
    [InlineData(2, -3, nameof(FraccionarDto.CantidadOrigen))]
    public void FraccionarValidator_DatosInvalidos_DebeFallar(int idArticuloDerivado, int cantidadOrigen, string propiedadEsperada)
    {
        // Act
        var result = _fraccionarValidator.Validate(new FraccionarDto { IdArticuloDerivado = idArticuloDerivado, CantidadOrigen = cantidadOrigen });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == propiedadEsperada);
    }

    private static CrearPresentacionDto PresentacionValida()
    {
        return new CrearPresentacionDto
        {
            IdArticuloOrigen = 1,
            UnidadesPorOrigen = 100,
            Descripcion = "Sobre manila (unidad)",
            CodigoBarras = "SOBRE-U",
            PorcentajeGanancia = 100m,
            StockMinimo = 10
        };
    }
}
