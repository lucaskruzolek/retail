using FluentAssertions;
using Retail.App.UnitTests.Helpers;
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
        _sut.ConfigurarEdicion(articulo, [], _categorias, _marcas);

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

    // ---------- Sección Fuentes: estado deseado hasta Guardar (Track 4.3, A-19) ----------

    [Fact]
    public void ConfigurarEdicion_ConFuentes_LasOrdenaPorCostoConLaPrincipalYConservaElCostoGuardado()
    {
        // Arrange: el costo guardado (1300) se editó a mano; cargar las fuentes no lo pisa
        var articulo = ArticuloConCosto(1300m);
        IReadOnlyList<FuenteArticuloDto> fuentes =
        [
            Fuente(25, "Distribuidora Sur", 1250m, esPrincipal: true),
            Fuente(26, "Mayorista Norte", 1100m)
        ];

        // Act
        _sut.ConfigurarEdicion(articulo, fuentes, _categorias, _marcas);

        // Assert
        _sut.Fuentes.Select(f => f.IdCatalogoProveedor).Should().Equal(26, 25);
        _sut.IdCatalogoProveedor.Should().Be(25);
        _sut.TieneFuentes.Should().BeTrue();
        _sut.TextoCantidadFuentes.Should().Be("2 fuentes");
        _sut.CostoReposicion.Should().Be(1300m);
    }

    [Fact]
    public void ConfigurarEdicion_FechaDeLista_SeMuestraEnHoraLocalYMarcaLaListaVieja()
    {
        // Arrange: el reloj está en UTC-3 el 29/10/2026; una lista de 01:00 UTC es del 28/10 local
        var sut = new ArticuloFormViewModel(new RelojDePrueba(new DateTimeOffset(2026, 10, 29, 15, 0, 0, TimeSpan.Zero)));
        IReadOnlyList<FuenteArticuloDto> fuentes =
        [
            Fuente(25, "Distribuidora Sur", 100m, esPrincipal: true, fechaUtc: new DateTime(2026, 10, 29, 1, 0, 0)),
            Fuente(26, "Mayorista Norte", 200m, fechaUtc: new DateTime(2026, 9, 20, 12, 0, 0))
        ];

        // Act
        sut.ConfigurarEdicion(ArticuloConCosto(100m), fuentes, _categorias, _marcas);

        // Assert
        sut.Fuentes[0].FechaLocal.Date.Should().Be(new DateTime(2026, 10, 28));
        sut.Fuentes[0].ListaDesactualizada.Should().BeFalse();
        sut.Fuentes[1].ListaDesactualizada.Should().BeTrue("tiene 39 días y el umbral es 30");
    }

    [Fact]
    public async Task AgregarFuenteAsync_SinPrincipal_LaPrimeraQuedaPrincipalTomaSuCostoYCompletaElAlta()
    {
        // Arrange (M1)
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.PorcentajeGanancia = 50m;
        _sut.OnAbrirSelectorProveedorAsync = (termino, ids) => Task.FromResult<CatalogoProveedorDto?>(Item(55, 1000m));

        // Act
        await _sut.AgregarFuenteCommand.ExecuteAsync(null);

        // Assert
        _sut.Fuentes.Should().ContainSingle().Which.EsPrincipal.Should().BeTrue();
        _sut.IdCatalogoProveedor.Should().Be(55);
        _sut.CostoReposicion.Should().Be(1000m);
        _sut.PrecioVentaCalculado.Should().Be(1500m);
        _sut.Descripcion.Should().Be("Cuaderno A4 80H Rayado");
        _sut.CodigoBarras.Should().Be("7798888888888");
    }

    [Fact]
    public async Task AgregarFuenteAsync_ConPrincipal_LaUbicaPorCostoSinCambiarLaPrincipalNiElCosto()
    {
        // Arrange
        _sut.ConfigurarEdicion(ArticuloConCosto(1250m), [Fuente(25, "Distribuidora Sur", 1250m, esPrincipal: true)], _categorias, _marcas);
        _sut.OnAbrirSelectorProveedorAsync = (termino, ids) => Task.FromResult<CatalogoProveedorDto?>(Item(55, 1000m));

        // Act
        await _sut.AgregarFuenteCommand.ExecuteAsync(null);

        // Assert
        _sut.Fuentes.Select(f => f.IdCatalogoProveedor).Should().Equal(55, 25);
        _sut.IdCatalogoProveedor.Should().Be(25);
        _sut.CostoReposicion.Should().Be(1250m);
        _sut.TextoCantidadFuentes.Should().Be("2 fuentes");
    }

    [Fact]
    public async Task AgregarFuenteAsync_InformaAlSelectorLasFuentesDelFormularioEIgnoraUnaRepetida()
    {
        // Arrange
        _sut.ConfigurarEdicion(ArticuloConCosto(1250m), [Fuente(25, "Distribuidora Sur", 1250m, esPrincipal: true)], _categorias, _marcas);
        IReadOnlyCollection<int>? idsInformados = null;
        string? terminoInformado = null;
        _sut.OnAbrirSelectorProveedorAsync = (termino, ids) =>
        {
            terminoInformado = termino;
            idsInformados = ids;
            return Task.FromResult<CatalogoProveedorDto?>(Item(25, 1250m));
        };

        // Act
        await _sut.AgregarFuenteCommand.ExecuteAsync(null);

        // Assert
        terminoInformado.Should().Be("Cuaderno Espiral A4");
        idsInformados.Should().Equal(25);
        _sut.Fuentes.Should().ContainSingle();
    }

    [Fact]
    public async Task AgregarFuenteAsync_SeleccionCancelada_NoCambiaNada()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.CostoReposicion = 600m;
        _sut.OnAbrirSelectorProveedorAsync = (termino, ids) => Task.FromResult<CatalogoProveedorDto?>(null);

        // Act
        await _sut.AgregarFuenteCommand.ExecuteAsync(null);

        // Assert
        _sut.NoTieneFuentes.Should().BeTrue();
        _sut.IdCatalogoProveedor.Should().BeNull();
        _sut.CostoReposicion.Should().Be(600m);
    }

    [Fact]
    public void MarcarPrincipal_OtraFuente_LaVuelvePrincipalYMuestraSuCosto()
    {
        // Arrange (M2)
        _sut.ConfigurarEdicion(ArticuloConCosto(1250m), DosFuentes(), _categorias, _marcas);
        _sut.PorcentajeGanancia = 50m;
        var norte = _sut.Fuentes.Single(f => f.IdCatalogoProveedor == 26);

        // Act
        _sut.MarcarPrincipalCommand.Execute(norte);

        // Assert
        _sut.IdCatalogoProveedor.Should().Be(26);
        _sut.Fuentes.Count(f => f.EsPrincipal).Should().Be(1);
        _sut.CostoReposicion.Should().Be(1100m);
        _sut.PrecioVentaCalculado.Should().Be(1650m);
    }

    [Fact]
    public void QuitarFuente_LaPrincipal_QuedaSinPrincipalYConservaElCosto()
    {
        // Arrange (M3)
        _sut.ConfigurarEdicion(ArticuloConCosto(1250m), DosFuentes(), _categorias, _marcas);
        var sur = _sut.Fuentes.Single(f => f.IdCatalogoProveedor == 25);

        // Act
        _sut.QuitarFuenteCommand.Execute(sur);

        // Assert
        _sut.Fuentes.Select(f => f.IdCatalogoProveedor).Should().Equal(26);
        _sut.IdCatalogoProveedor.Should().BeNull();
        _sut.CostoReposicion.Should().Be(1250m);
        _sut.TextoCantidadFuentes.Should().Be("1 fuente");
    }

    [Fact]
    public void ObtenerActualizarDto_ConFuentes_EnviaElEstadoDeseadoConLaPrincipal()
    {
        // Arrange
        _sut.ConfigurarEdicion(ArticuloConCosto(1250m), DosFuentes(), _categorias, _marcas);

        // Act
        var dto = _sut.ObtenerActualizarDto();

        // Assert
        dto.IdsFuentes.Should().BeEquivalentTo([25, 26]);
        dto.IdCatalogoProveedor.Should().Be(25);
    }

    [Fact]
    public void ObtenerCrearDto_SinFuentes_EnviaListaVaciaYSinPrincipal()
    {
        // Arrange
        _sut.ConfigurarAlta(_categorias, _marcas);
        _sut.Descripcion = "Producto artesanal";

        // Act
        var dto = _sut.ObtenerCrearDto();

        // Assert
        dto.IdsFuentes.Should().BeEmpty();
        dto.IdCatalogoProveedor.Should().BeNull();
    }

    [Fact]
    public void ConfigurarAlta_DespuesDeEditarConFuentes_LasDescarta()
    {
        // Arrange
        _sut.ConfigurarEdicion(ArticuloConCosto(1250m), DosFuentes(), _categorias, _marcas);

        // Act
        _sut.ConfigurarAlta(_categorias, _marcas);

        // Assert
        _sut.NoTieneFuentes.Should().BeTrue();
        _sut.IdCatalogoProveedor.Should().BeNull();
    }

    private static ArticuloDto ArticuloConCosto(decimal costo)
    {
        return new ArticuloDto
        {
            IdArticulo = 10,
            Descripcion = "Cuaderno Espiral A4",
            CostoReposicion = costo,
            PorcentajeGanancia = 40m,
            PrecioVenta = costo * 1.4m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };
    }

    /// <summary>Sur (25, costo 1250) es la principal; Norte (26, costo 1100) es adicional.</summary>
    private static IReadOnlyList<FuenteArticuloDto> DosFuentes()
    {
        return
        [
            Fuente(25, "Distribuidora Sur", 1250m, esPrincipal: true),
            Fuente(26, "Mayorista Norte", 1100m)
        ];
    }

    private static FuenteArticuloDto Fuente(int id, string proveedor, decimal costo, bool esPrincipal = false, DateTime? fechaUtc = null)
    {
        return new FuenteArticuloDto
        {
            IdCatalogoProveedor = id,
            IdProveedor = id,
            RazonSocialProveedor = proveedor,
            CodigoProveedor = $"COD-{id}",
            Descripcion = "Cuaderno Espiral 80H A4",
            CostoActual = costo,
            FechaActualizacion = fechaUtc ?? DateTime.UtcNow,
            EsPrincipal = esPrincipal
        };
    }

    private static CatalogoProveedorDto Item(int id, decimal costo)
    {
        return new CatalogoProveedorDto
        {
            Id = id,
            IdProveedor = 3,
            ProveedorRazonSocial = "Distribuidora Papelera",
            CodigoProveedor = "PAP-001",
            DescripcionProveedor = "Cuaderno A4 80H Rayado",
            CodigoBarras = "7798888888888",
            CostoReposicion = costo,
            FechaActualizacion = DateTime.UtcNow
        };
    }

    // ---------- Edición de una presentación derivada (RF-21) ----------

    [Fact]
    public void ConfigurarEdicion_PresentacionDerivada_ActivaModoDerivadoConAvisoDeCosto()
    {
        // Act
        _sut.ConfigurarEdicion(PresentacionDerivada(), [], _categorias, _marcas);

        // Assert
        _sut.EsDerivado.Should().BeTrue();
        _sut.NoEsDerivado.Should().BeFalse();
        _sut.UnidadesPorOrigen.Should().Be(100);
        _sut.TituloVentana.Should().Be("Modificar Presentación de Venta");
        _sut.AvisoCostoDerivado.Should().Contain("÷ 100").And.Contain("se reemplazará");
    }

    [Fact]
    public void ConfigurarEdicion_ArticuloComun_NoMuestraAvisoDeCosto()
    {
        // Arrange
        var articulo = PresentacionDerivada() with { IdArticuloOrigen = null, UnidadesPorOrigen = null };

        // Act
        _sut.ConfigurarEdicion(articulo, [], _categorias, _marcas);

        // Assert
        _sut.EsDerivado.Should().BeFalse();
        _sut.AvisoCostoDerivado.Should().BeEmpty();
        _sut.TituloVentana.Should().Be("Modificar Artículo de Catálogo");
    }

    [Fact]
    public void ConfigurarAlta_DespuesDeEditarUnaPresentacion_SaleDelModoDerivado()
    {
        // Arrange
        _sut.ConfigurarEdicion(PresentacionDerivada(), [], _categorias, _marcas);

        // Act
        _sut.ConfigurarAlta(_categorias, _marcas);

        // Assert
        _sut.EsDerivado.Should().BeFalse();
        _sut.UnidadesPorOrigen.Should().BeNull();
    }

    [Fact]
    public void ObtenerActualizarDto_PresentacionConCostoEditado_EnviaElCostoManualSinCatalogoNiServicio()
    {
        // Arrange: la regla 4 permite editar el costo de un derivado
        _sut.ConfigurarEdicion(PresentacionDerivada(), [], _categorias, _marcas);
        _sut.CostoReposicion = 12.5m;

        // Act
        var dto = _sut.ObtenerActualizarDto();

        // Assert
        dto.CostoReposicion.Should().Be(12.5m);
        dto.IdCatalogoProveedor.Should().BeNull();
        dto.EsServicio.Should().BeFalse();
    }

    private static ArticuloDto PresentacionDerivada()
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
            StockActual = 20,
            StockMinimo = 5,
            EsServicio = false
        };
    }
}
