using FluentAssertions;
using Retail.App.UnitTests.Snapshots;
using Retail.App.ViewModels.Presupuestos;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class AlertaPreciosPresupuestoViewModelTests
{
    [Fact]
    public void Constructor_PresupuestoVencidoConNovedades_MuestraLasTresSecciones()
    {
        // Arrange + Act
        var sut = new AlertaPreciosPresupuestoViewModel(DatosDeMuestra.PresupuestoParaVenta(vigente: false, conNovedades: true));

        // Assert
        sut.TituloVentana.Should().Be("Presupuesto N.º 00000123");
        sut.TieneDiscrepancias.Should().BeTrue();
        sut.Discrepancias.Should().HaveCount(2);
        sut.TieneFaltantes.Should().BeTrue();
        sut.TieneArticulosNoDisponibles.Should().BeTrue();
        sut.ArticulosNoDisponibles.Should().ContainSingle().Which.Should().Contain("Block de dibujo");
        sut.TextoIntroduccion.Should().Contain("venció");
        sut.Confirmado.Should().BeFalse();
    }

    [Fact]
    public void Constructor_Discrepancias_MarcanElAumentoYFormateanLosImportes()
    {
        var sut = new AlertaPreciosPresupuestoViewModel(DatosDeMuestra.PresupuestoParaVenta(vigente: false));

        var aumento = sut.Discrepancias[0];
        aumento.Descripcion.Should().Contain("Cuaderno");
        aumento.HuboAumento.Should().BeTrue();
        aumento.Pactado.Should().Contain("2.450");
        aumento.Actual.Should().Contain("2.700");
        aumento.Diferencia.Should().StartWith("+").And.Contain("250");

        var baja = sut.Discrepancias[1];
        baja.HuboAumento.Should().BeFalse();
        baja.Diferencia.Should().NotStartWith("+").And.Contain("30");
    }

    [Fact]
    public void Constructor_PresupuestoVigente_NoMuestraDiferenciasDePrecio()
    {
        // Vigente se cobra lo pactado (PS-02): una diferencia con el catálogo no es una novedad para el cajero
        var presupuesto = DatosDeMuestra.PresupuestoParaVenta(vigente: true) with
        {
            Discrepancias =
            [
                new DiscrepanciaPrecioPresupuestoDto
                {
                    IdArticulo = 1,
                    Descripcion = "Cuaderno",
                    PrecioPactado = 100m,
                    PrecioActualCatalogo = 200m
                }
            ]
        };

        var sut = new AlertaPreciosPresupuestoViewModel(presupuesto);

        sut.TieneDiscrepancias.Should().BeFalse();
        sut.TextoIntroduccion.Should().Contain("vigente");
    }

    [Fact]
    public void Constructor_FaltanteQuePuedeFraccionarse_IncluyeLaSugerencia()
    {
        var sut = new AlertaPreciosPresupuestoViewModel(DatosDeMuestra.PresupuestoParaVenta(vigente: true, conNovedades: true));

        var faltante = sut.Faltantes.Should().ContainSingle().Subject;
        faltante.Texto.Should().Contain("Lapicera azul Cristal").And.Contain("5").And.Contain("2");
        faltante.TieneSugerencia.Should().BeTrue();
        faltante.Sugerencia.Should().Contain("fraccionar").And.Contain("Lapicera azul Cristal x 12");
    }

    [Fact]
    public void Constructor_FaltanteSinFormaDeCubrirse_NoSugiereFraccionar()
    {
        var presupuesto = DatosDeMuestra.PresupuestoParaVenta(vigente: true) with
        {
            Faltantes =
            [
                new FaltanteStockDto { IdArticulo = 1, Descripcion = "Cuaderno", CantidadSolicitada = 5, StockActual = 1 }
            ]
        };

        var sut = new AlertaPreciosPresupuestoViewModel(presupuesto);

        sut.Faltantes.Should().ContainSingle().Which.TieneSugerencia.Should().BeFalse();
    }

    [Fact]
    public void Constructor_SinNovedades_NoMuestraNingunaSeccion()
    {
        var sut = new AlertaPreciosPresupuestoViewModel(DatosDeMuestra.PresupuestoParaVenta(vigente: true));

        sut.TieneDiscrepancias.Should().BeFalse();
        sut.TieneFaltantes.Should().BeFalse();
        sut.TieneArticulosNoDisponibles.Should().BeFalse();
    }
}
