using FluentAssertions;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;
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

    [Fact]
    public void ConfigurarEdicion_ConArticuloVinculado_CargaCamposDeProveedorYEstado()
    {
        // Arrange
        var articulo = new ArticuloDto
        {
            IdArticulo = 10,
            Descripcion = "Cuaderno Espiral A4",
            IdCatalogoProveedor = 99,
            ProveedorNombre = "Laprida Distribuidora S.A.",
            CodigoProveedor = "LAP-445",
            DescripcionProveedor = "Cuaderno Espiral 80H A4 Rayado",
            CostoCatalogoProveedor = 1250m,
            CostoReposicion = 1250m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 1750m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        // Act
        _sut.ConfigurarEdicion(articulo, _categorias, _marcas);

        // Assert
        _sut.EstaVinculadoAProveedor.Should().BeTrue();
        _sut.NoEstaVinculadoAProveedor.Should().BeFalse();
        _sut.IdCatalogoProveedor.Should().Be(99);
        _sut.ProveedorRazonSocial.Should().Be("Laprida Distribuidora S.A.");
        _sut.CodigoProveedor.Should().Be("LAP-445");
        _sut.DescripcionProveedor.Should().Be("Cuaderno Espiral 80H A4 Rayado");
        _sut.CostoProveedor.Should().Be(1250m);
        _sut.TextoBotonVinculacion.Should().Be("Cambiar...");
    }

    [Fact]
    public async Task VincularCatalogoAsync_SeleccionValida_ActualizaPropiedadesYCosto()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.PorcentajeGanancia = 50m;
        _sut.Descripcion = "Cuaderno";

        var catalogoItem = new CatalogoProveedorDto
        {
            Id = 55,
            IdProveedor = 3,
            ProveedorRazonSocial = "Distribuidora Papelera",
            CodigoProveedor = "PAP-001",
            DescripcionProveedor = "Cuaderno A4 80H Rayado",
            CodigoBarras = "7798888888888",
            CostoReposicion = 1000m
        };

        _sut.OnAbrirSelectorProveedorAsync = (termino, idProv) => Task.FromResult<CatalogoProveedorDto?>(catalogoItem);

        // Act
        await _sut.VincularCatalogoCommand.ExecuteAsync(null);

        // Assert
        _sut.EstaVinculadoAProveedor.Should().BeTrue();
        _sut.IdCatalogoProveedor.Should().Be(55);
        _sut.ProveedorRazonSocial.Should().Be("Distribuidora Papelera");
        _sut.CodigoProveedor.Should().Be("PAP-001");
        _sut.DescripcionProveedor.Should().Be("Cuaderno A4 80H Rayado");
        _sut.CostoProveedor.Should().Be(1000m);
        _sut.CostoReposicion.Should().Be(1000m);
        _sut.PrecioVentaCalculado.Should().Be(1500m); // 1000 + 50%
        _sut.CodigoBarras.Should().Be("7798888888888");
    }

    [Fact]
    public async Task VincularCatalogoAsync_SeleccionCancelada_MantieneValoresPrevios()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.CostoReposicion = 600m;
        _sut.OnAbrirSelectorProveedorAsync = (termino, idProv) => Task.FromResult<CatalogoProveedorDto?>(null);

        // Act
        await _sut.VincularCatalogoCommand.ExecuteAsync(null);

        // Assert
        _sut.EstaVinculadoAProveedor.Should().BeFalse();
        _sut.IdCatalogoProveedor.Should().BeNull();
        _sut.CostoReposicion.Should().Be(600m);
    }

    [Fact]
    public void DesvincularCatalogo_ArticuloVinculado_LimpiaCamposProveedorYPreservaCosto()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.IdCatalogoProveedor = 12;
        _sut.ProveedorRazonSocial = "Laprida";
        _sut.CodigoProveedor = "L-01";
        _sut.DescripcionProveedor = "Item Laprida";
        _sut.CostoProveedor = 850m;
        _sut.CostoReposicion = 850m;

        // Act
        _sut.DesvincularCatalogoCommand.Execute(null);

        // Assert
        _sut.EstaVinculadoAProveedor.Should().BeFalse();
        _sut.NoEstaVinculadoAProveedor.Should().BeTrue();
        _sut.IdCatalogoProveedor.Should().BeNull();
        _sut.ProveedorRazonSocial.Should().BeNull();
        _sut.CodigoProveedor.Should().BeNull();
        _sut.DescripcionProveedor.Should().BeNull();
        _sut.CostoProveedor.Should().BeNull();
        _sut.CostoReposicion.Should().Be(850m);
    }

    [Fact]
    public void ObtenerCrearDto_ConVinculacionAProveedor_IncluyeIdCatalogoProveedor()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.Descripcion = "Producto Vinculado";
        _sut.IdCatalogoProveedor = 77;
        _sut.CostoReposicion = 300m;

        // Act
        var dto = _sut.ObtenerCrearDto();

        // Assert
        dto.IdCatalogoProveedor.Should().Be(77);
    }

    [Fact]
    public void ObtenerActualizarDto_ConVinculacionAProveedor_IncluyeIdCatalogoProveedor()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.IdArticulo = 5;
        _sut.Descripcion = "Producto Actualizado";
        _sut.IdCatalogoProveedor = 88;
        _sut.CostoReposicion = 400m;

        // Act
        var dto = _sut.ObtenerActualizarDto();

        // Assert
        dto.IdCatalogoProveedor.Should().Be(88);
    }
}
