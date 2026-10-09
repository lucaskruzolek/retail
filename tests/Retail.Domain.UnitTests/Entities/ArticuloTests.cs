using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
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

    // ---------- Redondeo (D-06) ----------

    [Fact]
    public void CalcularPrecioVenta_PuntoMedio_RedondeaAlejandoseDeCero()
    {
        // Act: 0,67 × 1,5 = 1,005 (con ToEven daría 1,00)
        var resultado = Articulo.CalcularPrecioVenta(0.67m, 50m);

        // Assert
        resultado.Should().Be(1.01m);
    }

    [Theory]
    [InlineData(45.0, 1000, 0.05)]  // 0,045: punto medio, con ToEven daría 0,04
    [InlineData(1000.0, 100, 10.0)]
    [InlineData(100.0, 3, 33.33)]
    [InlineData(250.0, 1, 250.0)]
    public void CalcularCostoPresentacion_ValoresValidos_DivideYRedondeaAlejandoseDeCero(
        decimal costoOrigen,
        int unidadesPorOrigen,
        decimal costoEsperado)
    {
        // Act
        var resultado = Articulo.CalcularCostoPresentacion(costoOrigen, unidadesPorOrigen);

        // Assert
        resultado.Should().Be(costoEsperado);
    }

    [Fact]
    public void CalcularCostoPresentacion_CostoNegativo_LanzaArgumentOutOfRangeException()
    {
        // Act
        var act = () => Articulo.CalcularCostoPresentacion(-1m, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CalcularCostoPresentacion_UnidadesMenoresAUno_LanzaArgumentOutOfRangeException(int unidadesPorOrigen)
    {
        // Act
        var act = () => Articulo.CalcularCostoPresentacion(100m, unidadesPorOrigen);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------- Atomicidad de los métodos de costo (H-08) ----------

    [Fact]
    public void ActualizarCostoYRecalcularPrecio_CostoNegativo_NoModificaCostoNiPrecio()
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Goma de borrar",
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m
        };

        // Act
        var act = () => articulo.ActualizarCostoYRecalcularPrecio(-10m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        articulo.CostoReposicion.Should().Be(100m);
        articulo.PrecioVenta.Should().Be(150m);
    }

    [Fact]
    public void ActualizarDatos_GananciaNegativa_NoModificaNingunCampo()
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Original",
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m,
            StockActual = 10
        };

        // Act
        var act = () => articulo.ActualizarDatos(
            descripcion: "Modificado",
            idCategoria: null,
            idMarca: null,
            codigoBarras: "123",
            costoReposicion: 200m,
            porcentajeGanancia: -5m,
            stockActual: 99,
            stockMinimo: 1,
            esServicio: false);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        articulo.Descripcion.Should().Be("Original");
        articulo.CodigoBarras.Should().BeNull();
        articulo.CostoReposicion.Should().Be(100m);
        articulo.PrecioVenta.Should().Be(150m);
        articulo.StockActual.Should().Be(10);
    }

    // ---------- Presentaciones derivadas (RF-21) ----------

    [Fact]
    public void DefinirComoPresentacionDe_DatosValidos_VinculaOrigenYCalculaCostoYPrecio()
    {
        // Arrange
        var origen = CrearOrigen(id: 7, costo: 1000m);
        var derivado = new Articulo { Descripcion = "Sobre manila (unidad)", PorcentajeGanancia = 100m };

        // Act
        derivado.DefinirComoPresentacionDe(origen, 100);

        // Assert
        derivado.EsDerivado.Should().BeTrue();
        derivado.IdArticuloOrigen.Should().Be(7);
        derivado.ArticuloOrigen.Should().BeSameAs(origen);
        derivado.UnidadesPorOrigen.Should().Be(100);
        derivado.CostoReposicion.Should().Be(10m);
        derivado.PrecioVenta.Should().Be(20m); // markup propio del derivado
    }

    [Fact]
    public void DefinirComoPresentacionDe_OrigenNulo_LanzaArgumentNullException()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad" };

        // Act
        var act = () => derivado.DefinirComoPresentacionDe(null!, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DefinirComoPresentacionDe_UnidadesMenoresAUno_LanzaArgumentOutOfRangeException(int unidades)
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad" };

        // Act
        var act = () => derivado.DefinirComoPresentacionDe(CrearOrigen(), unidades);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        derivado.EsDerivado.Should().BeFalse();
    }

    [Fact]
    public void DefinirComoPresentacionDe_MismaInstancia_LanzaDomainException()
    {
        // Arrange
        var articulo = CrearOrigen();

        // Act
        var act = () => articulo.DefinirComoPresentacionDe(articulo, 10);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*sí mismo*");
    }

    [Fact]
    public void DefinirComoPresentacionDe_MismoId_LanzaDomainException()
    {
        // Arrange
        var articulo = new Articulo { Id = 5, Descripcion = "Pack" };
        var mismoArticuloOtraInstancia = CrearOrigen(id: 5);

        // Act
        var act = () => articulo.DefinirComoPresentacionDe(mismoArticuloOtraInstancia, 10);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*sí mismo*");
    }

    [Fact]
    public void DefinirComoPresentacionDe_OrigenEsDerivado_LanzaDomainException()
    {
        // Arrange
        var origenDerivado = CrearOrigen(id: 2);
        origenDerivado.DefinirComoPresentacionDe(CrearOrigen(id: 1), 10);
        var derivado = new Articulo { Descripcion = "Segundo nivel" };

        // Act
        var act = () => derivado.DefinirComoPresentacionDe(origenDerivado, 2);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*un solo nivel*");
        derivado.EsDerivado.Should().BeFalse();
    }

    [Fact]
    public void DefinirComoPresentacionDe_ArticuloVinculadoACatalogo_LanzaDomainException()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad", IdCatalogoProveedor = 15 };

        // Act
        var act = () => derivado.DefinirComoPresentacionDe(CrearOrigen(), 10);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*catálogo de proveedor*");
    }

    [Fact]
    public void DefinirComoPresentacionDe_DerivadoEsServicio_LanzaDomainException()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Plastificado", EsServicio = true };

        // Act
        var act = () => derivado.DefinirComoPresentacionDe(CrearOrigen(), 10);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*servicios*");
    }

    [Fact]
    public void DefinirComoPresentacionDe_OrigenEsServicio_LanzaDomainException()
    {
        // Arrange
        var origen = CrearOrigen();
        origen.EsServicio = true;
        var derivado = new Articulo { Descripcion = "Unidad" };

        // Act
        var act = () => derivado.DefinirComoPresentacionDe(origen, 10);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*servicios*");
    }

    [Fact]
    public void DefinirComoPresentacionDe_ArticuloYaEsOrigenDeOtros_LanzaDomainException()
    {
        // Arrange
        var articuloConPresentaciones = new Articulo { Id = 3, Descripcion = "Pack x10" };
        articuloConPresentaciones.Presentaciones.Add(new Articulo { Descripcion = "Unidad" });

        // Act
        var act = () => articuloConPresentaciones.DefinirComoPresentacionDe(CrearOrigen(id: 1), 10);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*ya es origen*");
    }

    [Fact]
    public void RecalcularCostoDesdeOrigen_Derivado_ActualizaCostoYPrecioConSuMarkup()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad", PorcentajeGanancia = 50m };
        derivado.DefinirComoPresentacionDe(CrearOrigen(costo: 1000m), 100);

        // Act
        derivado.RecalcularCostoDesdeOrigen(2000m);

        // Assert
        derivado.CostoReposicion.Should().Be(20m);
        derivado.PrecioVenta.Should().Be(30m);
    }

    [Fact]
    public void RecalcularCostoDesdeOrigen_CostoEditadoAMano_LoPisaConElCostoDerivado()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad", PorcentajeGanancia = 0m };
        derivado.DefinirComoPresentacionDe(CrearOrigen(costo: 1000m), 100);
        derivado.ActualizarCostoYRecalcularPrecio(12.5m); // edición manual

        // Act
        derivado.RecalcularCostoDesdeOrigen(1000m);

        // Assert
        derivado.CostoReposicion.Should().Be(10m);
    }

    [Fact]
    public void RecalcularCostoDesdeOrigen_ArticuloNoDerivado_LanzaInvalidOperationException()
    {
        // Arrange
        var articulo = CrearOrigen();

        // Act
        var act = () => articulo.RecalcularCostoDesdeOrigen(500m);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void VincularCatalogoProveedor_ArticuloDerivado_LanzaDomainException()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad" };
        derivado.DefinirComoPresentacionDe(CrearOrigen(), 10);

        // Act
        var act = () => derivado.VincularCatalogoProveedor(42, 100m);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*catálogo de proveedor*");
        derivado.IdCatalogoProveedor.Should().BeNull();
    }

    [Fact]
    public void ActualizarDatos_DerivadoConCatalogo_LanzaDomainException()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad" };
        derivado.DefinirComoPresentacionDe(CrearOrigen(), 10);

        // Act
        var act = () => derivado.ActualizarDatos(
            descripcion: "Unidad",
            idCategoria: null,
            idMarca: null,
            codigoBarras: null,
            costoReposicion: 10m,
            porcentajeGanancia: 50m,
            stockActual: 0,
            stockMinimo: 0,
            esServicio: false,
            idCatalogoProveedor: 42);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*catálogo de proveedor*");
    }

    [Fact]
    public void ActualizarDatos_DerivadoComoServicio_LanzaDomainException()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad" };
        derivado.DefinirComoPresentacionDe(CrearOrigen(), 10);

        // Act
        var act = () => derivado.ActualizarDatos(
            descripcion: "Unidad",
            idCategoria: null,
            idMarca: null,
            codigoBarras: null,
            costoReposicion: 10m,
            porcentajeGanancia: 50m,
            stockActual: 0,
            stockMinimo: 0,
            esServicio: true);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*servicio*");
        derivado.EsServicio.Should().BeFalse();
    }

    [Fact]
    public void ActualizarDatos_DerivadoConCostoManual_PermiteEditarElCosto()
    {
        // Arrange
        var derivado = new Articulo { Descripcion = "Unidad" };
        derivado.DefinirComoPresentacionDe(CrearOrigen(costo: 1000m), 100);

        // Act
        derivado.ActualizarDatos(
            descripcion: "Unidad",
            idCategoria: null,
            idMarca: null,
            codigoBarras: null,
            costoReposicion: 12.5m,
            porcentajeGanancia: 100m,
            stockActual: 0,
            stockMinimo: 0,
            esServicio: false);

        // Assert
        derivado.CostoReposicion.Should().Be(12.5m);
        derivado.PrecioVenta.Should().Be(25m);
        derivado.EsDerivado.Should().BeTrue();
    }

    // ---------- Movimientos de stock ----------

    [Fact]
    public void DescontarStock_StockSuficiente_RestaLaCantidad()
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Lápiz", StockActual = 10 };

        // Act
        articulo.DescontarStock(4);

        // Assert
        articulo.StockActual.Should().Be(6);
    }

    [Fact]
    public void DescontarStock_TodoElStock_DejaStockEnCero()
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Lápiz", StockActual = 3 };

        // Act
        articulo.DescontarStock(3);

        // Assert
        articulo.StockActual.Should().Be(0);
    }

    [Fact]
    public void DescontarStock_StockInsuficiente_LanzaExcepcionSinModificarElStock()
    {
        // Arrange
        var articulo = new Articulo { Id = 8, Descripcion = "Lápiz", StockActual = 2 };

        // Act
        var act = () => articulo.DescontarStock(5);

        // Assert
        act.Should().Throw<StockInsuficienteException>()
            .Which.CantidadSolicitada.Should().Be(5);
        articulo.StockActual.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void DescontarStock_CantidadMenorAUno_LanzaArgumentOutOfRangeException(int cantidad)
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Lápiz", StockActual = 10 };

        // Act
        var act = () => articulo.DescontarStock(cantidad);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        articulo.StockActual.Should().Be(10);
    }

    [Fact]
    public void DescontarStock_Servicio_LanzaDomainException()
    {
        // Arrange
        var servicio = new Articulo { Descripcion = "Fotocopia", EsServicio = true };

        // Act
        var act = () => servicio.DescontarStock(1);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*servicio*");
    }

    [Fact]
    public void VerificarStockDisponible_StockSuficiente_NoLanzaNiModificaElStock()
    {
        // Arrange
        var articulo = new Articulo { Id = 8, Descripcion = "Lápiz", StockActual = 5 };

        // Act
        var act = () => articulo.VerificarStockDisponible(5);

        // Assert
        act.Should().NotThrow();
        articulo.StockActual.Should().Be(5);
    }

    [Fact]
    public void VerificarStockDisponible_StockInsuficiente_LanzaStockInsuficienteException()
    {
        // Arrange
        var articulo = new Articulo { Id = 8, Descripcion = "Lápiz", StockActual = 2 };

        // Act
        var act = () => articulo.VerificarStockDisponible(3);

        // Assert
        act.Should().Throw<StockInsuficienteException>()
            .Which.StockActual.Should().Be(2);
        articulo.StockActual.Should().Be(2);
    }

    [Fact]
    public void IncrementarStock_CantidadValida_SumaLaCantidad()
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Lápiz", StockActual = 10 };

        // Act
        articulo.IncrementarStock(5);

        // Assert
        articulo.StockActual.Should().Be(15);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void IncrementarStock_CantidadMenorAUno_LanzaArgumentOutOfRangeException(int cantidad)
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Lápiz", StockActual = 10 };

        // Act
        var act = () => articulo.IncrementarStock(cantidad);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        articulo.StockActual.Should().Be(10);
    }

    [Fact]
    public void IncrementarStock_Servicio_LanzaDomainException()
    {
        // Arrange
        var servicio = new Articulo { Descripcion = "Fotocopia", EsServicio = true };

        // Act
        var act = () => servicio.IncrementarStock(1);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*servicio*");
    }

    [Fact]
    public void IncrementarStock_Desborde_LanzaOverflowExceptionSinModificarElStock()
    {
        // Arrange
        var articulo = new Articulo { Descripcion = "Lápiz", StockActual = int.MaxValue };

        // Act
        var act = () => articulo.IncrementarStock(1);

        // Assert
        act.Should().Throw<OverflowException>();
        articulo.StockActual.Should().Be(int.MaxValue);
    }

    private static Articulo CrearOrigen(int id = 1, decimal costo = 1000m)
    {
        return new Articulo
        {
            Id = id,
            Descripcion = "Sobre manila (pack x100)",
            CostoReposicion = costo,
            PorcentajeGanancia = 40m,
            PrecioVenta = Articulo.CalcularPrecioVenta(costo, 40m),
            StockActual = 5
        };
    }
}
