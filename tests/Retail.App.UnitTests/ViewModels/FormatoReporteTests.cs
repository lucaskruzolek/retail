using FluentAssertions;
using Retail.App.ViewModels.Reportes;
using Retail.Application.DTOs.Reportes;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class FormatoReporteTests
{
    [Theory]
    [InlineData(1234567.5, "$ 1.234.567,50")]
    [InlineData(0, "$ 0,00")]
    [InlineData(-25.3, "-$ 25,30")]
    public void Moneda_UsaLaCulturaArgentinaSinDependerDelEquipo(double valor, string esperado)
    {
        // Act + Assert
        FormatoReporte.Moneda((decimal)valor).Should().Be(esperado);
    }

    [Fact]
    public void Variacion_ConValorPositivo_LlevaFlechaHaciaArribaSignoMasYEstadoSube()
    {
        // Act
        var variacion = FormatoReporte.Variacion(12.5m);

        // Assert
        variacion.Texto.Should().Be("▲ +12,50 % vs período anterior");
        variacion.Estado.Should().Be(EstadoVariacionEnum.Sube);
    }

    [Fact]
    public void Variacion_ConValorNegativo_LlevaFlechaHaciaAbajoSignoMenosYEstadoBaja()
    {
        // Act
        var variacion = FormatoReporte.Variacion(-3.1m);

        // Assert
        variacion.Texto.Should().Be("▼ −3,10 % vs período anterior");
        variacion.Estado.Should().Be(EstadoVariacionEnum.Baja);
    }

    [Fact]
    public void Variacion_SinBaseDeComparacion_InformaQueNoHayDatosDelPeriodoAnterior()
    {
        // Act
        var variacion = FormatoReporte.Variacion(null);

        // Assert
        variacion.Texto.Should().Be("Sin datos del período anterior");
        variacion.Estado.Should().Be(EstadoVariacionEnum.SinBase);
    }

    [Fact]
    public void Variacion_Cero_EsNeutra()
    {
        // Act
        var variacion = FormatoReporte.Variacion(0m);

        // Assert
        variacion.Texto.Should().Be("= 0,00 % vs período anterior");
        variacion.Estado.Should().Be(EstadoVariacionEnum.SinBase);
    }


    [Theory]
    [InlineData(20_000_000, 40_000_000, true, "20 M")]
    [InlineData(2_500_000, 40_000_000, true, "2,5 M")]
    [InlineData(0, 40_000_000, true, "0")]
    [InlineData(1_000_000, 1_000_000, true, "1 M")]
    [InlineData(15_000, 40_000, true, "15 mil")]
    [InlineData(12_500, 40_000, true, "12,5 mil")]
    [InlineData(5_000, 9_000, true, "5.000")]
    [InlineData(2, 3, true, "2")]
    [InlineData(2.5, 3, false, "2,50")]
    public void EtiquetaEje_SegunLaEscalaDelMaximo_UsaFormatoCompactoOCompleto(double valor, double maximo, bool datosEnteros, string esperado)
    {
        // Act + Assert
        FormatoReporte.EtiquetaEje(valor, maximo, datosEnteros).Should().Be(esperado);
    }

    [Fact]
    public void EtiquetaPeriodo_SegunLaAgrupacion_MuestraDiaOMes()
    {
        // Arrange
        var fecha = new DateOnly(2026, 10, 5);

        // Act + Assert
        FormatoReporte.EtiquetaPeriodo(fecha, AgrupacionTemporalEnum.Dia).Should().Be("05/10");
        FormatoReporte.EtiquetaPeriodo(fecha, AgrupacionTemporalEnum.Semana).Should().Be("05/10");
        FormatoReporte.EtiquetaPeriodo(fecha, AgrupacionTemporalEnum.Mes).Should().Be("10/2026");
    }
}
