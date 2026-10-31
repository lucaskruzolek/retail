using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
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
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };
        articulo.Clasificar(1, OrigenClasificacionEnum.Manual, 1, OrigenClasificacionEnum.Manual);

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
    public void QuitarFuentePrincipal_ArticuloConPrincipal_QuedaSinPrincipalYConservaElCosto()
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Resma A4",
            CostoReposicion = 5000m,
            PorcentajeGanancia = 30m,
            PrecioVenta = 6500m
        };
        var fuente = CrearFuente(id: 15, costo: 5000m);
        fuente.AsignarArticulo(articulo);
        articulo.MarcarFuentePrincipal(fuente);

        // Act
        articulo.QuitarFuentePrincipal();

        // Assert
        articulo.IdCatalogoProveedor.Should().BeNull();
        articulo.CostoReposicion.Should().Be(5000m); // Preserva el último costo
        articulo.PrecioVenta.Should().Be(6500m);
    }

    // ---------- Fuentes múltiples y fuente principal (Track 4.3, A-16, J1) ----------

    [Fact]
    public void MarcarFuentePrincipal_FuenteDelArticulo_LaHacePrincipalYTomaSuCosto()
    {
        var articulo = CrearArticuloConFuentes(id: 7);
        var fuente = CrearFuente(id: 42, costo: 1200m);
        fuente.AsignarArticulo(articulo);

        articulo.MarcarFuentePrincipal(fuente);

        articulo.IdCatalogoProveedor.Should().Be(42);
        articulo.CostoReposicion.Should().Be(1200m);
        articulo.PrecioVenta.Should().Be(1800m); // 1200 + 50%
    }

    [Fact]
    public void MarcarFuentePrincipal_ArticuloNuevoSinId_ReconoceLaFuentePorLaInstancia()
    {
        var articulo = CrearArticuloConFuentes(id: 0);
        var fuente = CrearFuente(id: 42, costo: 1200m);
        fuente.AsignarArticulo(articulo);

        articulo.MarcarFuentePrincipal(fuente);

        articulo.IdCatalogoProveedor.Should().Be(42);
    }

    [Fact]
    public void MarcarFuentePrincipal_CambioDePrincipal_LaAnteriorSigueSiendoFuente()
    {
        var articulo = CrearArticuloConFuentes(id: 7);
        var anterior = CrearFuente(id: 42, costo: 1200m);
        var nueva = CrearFuente(id: 43, costo: 1100m);
        anterior.AsignarArticulo(articulo);
        nueva.AsignarArticulo(articulo);
        articulo.MarcarFuentePrincipal(anterior);

        articulo.MarcarFuentePrincipal(nueva);

        articulo.IdCatalogoProveedor.Should().Be(43);
        articulo.CostoReposicion.Should().Be(1100m);
        anterior.EsFuenteDe(articulo).Should().BeTrue();
    }

    [Fact]
    public void MarcarFuentePrincipal_ItemQueNoEsFuente_LanzaDomainExceptionSinModificarElArticulo()
    {
        var articulo = CrearArticuloConFuentes(id: 7);
        var ajena = CrearFuente(id: 42, costo: 1200m);

        var act = () => articulo.MarcarFuentePrincipal(ajena);

        act.Should().Throw<DomainException>().WithMessage("*no es fuente de este artículo*");
        articulo.IdCatalogoProveedor.Should().BeNull();
        articulo.CostoReposicion.Should().Be(1000m);
    }

    [Fact]
    public void MarcarFuentePrincipal_Servicio_LanzaDomainExceptionSinModificarElCosto()
    {
        // Un servicio con un ítem ya vinculado solo puede venir de datos anteriores a la regla: igual no toma principal
        var servicio = CrearArticuloConFuentes(id: 7);
        var fuente = CrearFuente(id: 42, costo: 1200m);
        fuente.AsignarArticulo(servicio);
        servicio.EsServicio = true;

        var act = () => servicio.MarcarFuentePrincipal(fuente);

        act.Should().Throw<DomainException>().WithMessage("*no puede tener fuente principal*");
        servicio.IdCatalogoProveedor.Should().BeNull();
        servicio.CostoReposicion.Should().Be(1000m);
    }

    [Fact]
    public void MarcarFuentePrincipal_ItemFuenteDeOtroArticulo_LanzaDomainException()
    {
        var articulo = CrearArticuloConFuentes(id: 7);
        var fuente = CrearFuente(id: 42, costo: 1200m);
        fuente.AsignarArticulo(CrearArticuloConFuentes(id: 8));

        var act = () => articulo.MarcarFuentePrincipal(fuente);

        act.Should().Throw<DomainException>();
        articulo.IdCatalogoProveedor.Should().BeNull();
    }

    [Fact]
    public void MarcarFuentePrincipal_ItemSinGuardar_LanzaArgumentOutOfRangeException()
    {
        var articulo = CrearArticuloConFuentes(id: 7);
        var fuente = CrearFuente(id: 0, costo: 1200m);
        fuente.AsignarArticulo(articulo);

        var act = () => articulo.MarcarFuentePrincipal(fuente);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarcarFuentePrincipal_FuenteNula_LanzaArgumentNullException()
    {
        var articulo = CrearArticuloConFuentes(id: 7);

        var act = () => articulo.MarcarFuentePrincipal(null!);

        act.Should().Throw<ArgumentNullException>();
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
    public void ActualizarDatos_CodigoConLetras_LanzaDomainExceptionSinModificarNingunCampo()
    {
        // Arrange
        var articulo = new Articulo
        {
            Descripcion = "Original",
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m
        };

        // Act
        var act = () => articulo.ActualizarDatos(
            descripcion: "Modificado",
            idCategoria: null,
            idMarca: null,
            codigoBarras: "SOBRE-U",
            costoReposicion: 200m,
            porcentajeGanancia: 50m,
            stockActual: 0,
            stockMinimo: 1,
            esServicio: false);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*solo admite dígitos*");
        articulo.Descripcion.Should().Be("Original");
        articulo.CostoReposicion.Should().Be(100m);
    }

    [Fact]
    public void ActualizarDatos_CodigoInternoCorto_LoGuardaSinEspacios()
    {
        // Arrange: "11" es el código interno de fotocopias que el cajero tipea en el POS (M11)
        var servicio = new Articulo();

        // Act
        servicio.ActualizarDatos(
            descripcion: "Fotocopia simple",
            idCategoria: null,
            idMarca: null,
            codigoBarras: " 11 ",
            costoReposicion: 20m,
            porcentajeGanancia: 50m,
            stockActual: 0,
            stockMinimo: 0,
            esServicio: true);

        // Assert
        servicio.CodigoBarras.Should().Be("11");
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
        var derivado = new Articulo { Descripcion = "Unidad" };
        var fuente = CrearFuente(id: 15, costo: 100m);
        fuente.AsignarArticulo(derivado);
        derivado.MarcarFuentePrincipal(fuente);

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

    [Fact]
    public void Clasificar_SugerenciasConfirmadas_AsignaIdentificadoresYOrigenes()
    {
        var articulo = new Articulo { Descripcion = "Mochila" };

        articulo.Clasificar(4, OrigenClasificacionEnum.SugerenciaAceptada, 7, OrigenClasificacionEnum.SugerenciaCorregida);

        articulo.IdCategoria.Should().Be(4);
        articulo.OrigenCategoria.Should().Be(OrigenClasificacionEnum.SugerenciaAceptada);
        articulo.IdMarca.Should().Be(7);
        articulo.OrigenMarca.Should().Be(OrigenClasificacionEnum.SugerenciaCorregida);
    }

    [Fact]
    public void Clasificar_SinMarcaConOrigen_RegistraSinMarcaConfirmada()
    {
        var articulo = new Articulo { Descripcion = "Pelota" };

        articulo.Clasificar(4, OrigenClasificacionEnum.Manual, null, OrigenClasificacionEnum.SugerenciaAceptada);

        articulo.IdMarca.Should().BeNull();
        articulo.OrigenMarca.Should().Be(OrigenClasificacionEnum.SugerenciaAceptada);
    }

    [Fact]
    public void Clasificar_SinOrigenes_DejaElArticuloSinClasificar()
    {
        var articulo = new Articulo { Descripcion = "Pelota" };
        articulo.Clasificar(4, OrigenClasificacionEnum.Manual, 7, OrigenClasificacionEnum.Manual);

        articulo.Clasificar(null, null, null, null);

        articulo.IdCategoria.Should().BeNull();
        articulo.OrigenCategoria.Should().BeNull();
        articulo.IdMarca.Should().BeNull();
        articulo.OrigenMarca.Should().BeNull();
    }

    [Theory]
    [InlineData(4, null)]
    [InlineData(null, OrigenClasificacionEnum.Manual)]
    public void Clasificar_CategoriaYOrigenNoVanJuntos_LanzaDomainException(int? idCategoria, OrigenClasificacionEnum? origen)
    {
        var articulo = new Articulo { Descripcion = "Pelota" };

        var accion = () => articulo.Clasificar(idCategoria, origen, null, null);

        accion.Should().Throw<DomainException>();
        articulo.OrigenCategoria.Should().BeNull();
    }

    [Fact]
    public void Clasificar_MarcaSinOrigen_LanzaDomainException()
    {
        var articulo = new Articulo { Descripcion = "Pelota" };

        var accion = () => articulo.Clasificar(null, null, 7, null);

        accion.Should().Throw<DomainException>();
        articulo.IdMarca.Should().BeNull();
    }

    [Fact]
    public void Clasificar_OrigenFueraDelEnum_LanzaArgumentOutOfRangeException()
    {
        var articulo = new Articulo { Descripcion = "Pelota" };

        var accion = () => articulo.Clasificar(4, (OrigenClasificacionEnum)99, null, null);

        accion.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, -1)]
    public void Clasificar_IdentificadorNoPositivo_LanzaArgumentOutOfRangeException(int? idCategoria, int? idMarca)
    {
        var articulo = new Articulo { Descripcion = "Pelota" };

        var accion = () => articulo.Clasificar(
            idCategoria,
            OrigenClasificacionEnum.Manual,
            idMarca,
            OrigenClasificacionEnum.Manual);

        accion.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ActualizarDatos_ClasificacionSinCambios_ConservaElOrigen()
    {
        var articulo = new Articulo { Descripcion = "Mochila" };
        articulo.Clasificar(4, OrigenClasificacionEnum.SugerenciaAceptada, 7, OrigenClasificacionEnum.SugerenciaCorregida);

        ActualizarClasificacion(articulo, idCategoria: 4, idMarca: 7);

        articulo.OrigenCategoria.Should().Be(OrigenClasificacionEnum.SugerenciaAceptada);
        articulo.OrigenMarca.Should().Be(OrigenClasificacionEnum.SugerenciaCorregida);
    }

    [Fact]
    public void ActualizarDatos_ClasificacionCambiada_PasaAOrigenManual()
    {
        var articulo = new Articulo { Descripcion = "Mochila" };
        articulo.Clasificar(4, OrigenClasificacionEnum.SugerenciaAceptada, 7, OrigenClasificacionEnum.SugerenciaAceptada);

        ActualizarClasificacion(articulo, idCategoria: 5, idMarca: 8);

        articulo.OrigenCategoria.Should().Be(OrigenClasificacionEnum.Manual);
        articulo.OrigenMarca.Should().Be(OrigenClasificacionEnum.Manual);
    }

    [Fact]
    public void ActualizarDatos_QuitaCategoriaYMarca_CategoriaSinClasificarYSinMarcaConfirmada()
    {
        var articulo = new Articulo { Descripcion = "Mochila" };
        articulo.Clasificar(4, OrigenClasificacionEnum.SugerenciaAceptada, 7, OrigenClasificacionEnum.SugerenciaAceptada);

        ActualizarClasificacion(articulo, idCategoria: null, idMarca: null);

        articulo.IdCategoria.Should().BeNull();
        articulo.OrigenCategoria.Should().BeNull();
        articulo.IdMarca.Should().BeNull();
        articulo.OrigenMarca.Should().Be(OrigenClasificacionEnum.Manual);
    }

    [Fact]
    public void ActualizarDatos_ArticuloNuevoSinClasificacion_QuedaSinClasificar()
    {
        var articulo = new Articulo();

        ActualizarClasificacion(articulo, idCategoria: null, idMarca: null);

        articulo.OrigenCategoria.Should().BeNull();
        articulo.OrigenMarca.Should().BeNull();
    }

    private static void ActualizarClasificacion(Articulo articulo, int? idCategoria, int? idMarca)
    {
        articulo.ActualizarDatos(
            descripcion: "Mochila escolar",
            idCategoria: idCategoria,
            idMarca: idMarca,
            codigoBarras: null,
            costoReposicion: 100m,
            porcentajeGanancia: 50m,
            stockActual: 1,
            stockMinimo: 0,
            esServicio: false);
    }

    private static Articulo CrearArticuloConFuentes(int id)
    {
        return new Articulo
        {
            Id = id,
            Descripcion = "Cuaderno Rivadavia",
            CostoReposicion = 1000m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 1500m
        };
    }

    private static CatalogoProveedor CrearFuente(int id, decimal costo)
    {
        return new CatalogoProveedor
        {
            Id = id,
            IdProveedor = 1,
            CodigoProveedor = $"COD-{id}",
            DescripcionProveedor = "Cuaderno Rivadavia ABC",
            CostoReposicion = costo
        };
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
