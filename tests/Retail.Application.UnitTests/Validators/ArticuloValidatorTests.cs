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

    // ---------- Fuentes del formulario (Track 4.3, A-19) ----------

    [Theory]
    [InlineData(new[] { 25, 26 }, 26)]
    [InlineData(new[] { 25 }, null)]
    [InlineData(new int[0], null)]
    public void CrearArticuloValidator_FuentesCoherentes_DebePasar(int[] idsFuentes, int? idPrincipal)
    {
        // Act
        var result = _crearValidator.Validate(AltaConFuentes(idsFuentes, idPrincipal));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(new[] { 25 }, 26, nameof(CrearArticuloDto.IdCatalogoProveedor))]
    [InlineData(new int[0], 25, nameof(CrearArticuloDto.IdCatalogoProveedor))]
    [InlineData(new[] { 25, 25 }, 25, nameof(CrearArticuloDto.IdsFuentes))]
    [InlineData(new[] { 0 }, null, "IdsFuentes[0]")]
    public void CrearArticuloValidator_FuentesIncoherentes_DebeFallar(int[] idsFuentes, int? idPrincipal, string propiedadEsperada)
    {
        // Act
        var result = _crearValidator.Validate(AltaConFuentes(idsFuentes, idPrincipal));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == propiedadEsperada);
    }

    [Fact]
    public void ActualizarArticuloValidator_PrincipalFueraDeLasFuentes_DebeFallar()
    {
        // Arrange
        var dto = new ActualizarArticuloDto
        {
            IdArticulo = 10,
            Descripcion = "Marcador Edding 700",
            CostoReposicion = 800m,
            PorcentajeGanancia = 50m,
            StockActual = 0,
            StockMinimo = 1,
            EsServicio = false,
            IdsFuentes = [25],
            IdCatalogoProveedor = 26
        };

        // Act
        var result = _actualizarValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ActualizarArticuloDto.IdCatalogoProveedor));
    }

    [Fact]
    public void CrearArticuloValidator_ServicioConFuentes_DebeFallar()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            Descripcion = "Anillado",
            CostoReposicion = 800m,
            PorcentajeGanancia = 50m,
            StockActual = 0,
            StockMinimo = 0,
            EsServicio = true,
            IdsFuentes = [25],
            IdCatalogoProveedor = 25
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearArticuloDto.IdsFuentes)
            && e.ErrorMessage.Contains("Un servicio no puede tener fuentes"));
    }

    [Theory]
    [InlineData(true, new int[0], true)]
    [InlineData(true, new[] { 25 }, false)]
    [InlineData(false, new[] { 25 }, true)]
    public void ActualizarArticuloValidator_ServicioYFuentes_SoloFallaUnServicioConFuentes(bool esServicio, int[] idsFuentes, bool esValido)
    {
        // Arrange: convertir en servicio exige haber quitado antes las fuentes
        var dto = new ActualizarArticuloDto
        {
            IdArticulo = 10,
            Descripcion = "Anillado",
            CostoReposicion = 800m,
            PorcentajeGanancia = 50m,
            StockActual = 0,
            StockMinimo = 0,
            EsServicio = esServicio,
            IdsFuentes = idsFuentes
        };

        // Act
        var result = _actualizarValidator.Validate(dto);

        // Assert
        result.IsValid.Should().Be(esValido);
    }

    [Theory]
    [InlineData("11", true)]
    [InlineData("7791234567890", true)]
    [InlineData("SOBRE-U", false)]
    [InlineData("779 123", false)]
    public void CrearArticuloValidator_Codigo_SoloAdmiteDigitos(string codigo, bool esValido)
    {
        // Arrange: el mismo campo guarda el código de barras de fábrica o un código interno corto (M11)
        var dto = new CrearArticuloDto
        {
            Descripcion = "Fotocopia simple",
            CodigoBarras = codigo,
            CostoReposicion = 20m,
            PorcentajeGanancia = 50m,
            StockActual = 0,
            StockMinimo = 0,
            EsServicio = true
        };

        // Act
        var result = _crearValidator.Validate(dto);

        // Assert
        result.IsValid.Should().Be(esValido);
    }

    [Fact]
    public void CrearPresentacionValidator_CodigoConLetras_DebeFallar()
    {
        // Arrange
        var dto = PresentacionValida() with { CodigoBarras = "SOBRE-U" };

        // Act
        var result = _crearPresentacionValidator.Validate(dto);

        // Assert
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CrearPresentacionDto.CodigoBarras));
    }

    private static CrearArticuloDto AltaConFuentes(int[] idsFuentes, int? idPrincipal)
    {
        return new CrearArticuloDto
        {
            Descripcion = "Marcador Edding 700",
            CostoReposicion = 800m,
            PorcentajeGanancia = 50m,
            StockActual = 0,
            StockMinimo = 1,
            EsServicio = false,
            IdsFuentes = idsFuentes,
            IdCatalogoProveedor = idPrincipal
        };
    }

    private static CrearPresentacionDto PresentacionValida()
    {
        return new CrearPresentacionDto
        {
            IdArticuloOrigen = 1,
            UnidadesPorOrigen = 100,
            Descripcion = "Sobre manila (unidad)",
            CodigoBarras = "512",
            PorcentajeGanancia = 100m,
            StockMinimo = 10
        };
    }
}
