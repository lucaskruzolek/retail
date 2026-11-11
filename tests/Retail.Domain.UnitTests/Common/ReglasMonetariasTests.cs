using FluentAssertions;
using Retail.Domain.Common;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

public class ReglasMonetariasTests
{
    [Theory]
    [InlineData(1.005, 1.01)]
    [InlineData(1.004, 1.00)]
    [InlineData(-1.005, -1.01)]
    [InlineData(2.5, 2.5)]
    public void Redondear_ConImporte_DevuelveDosDecimalesAlejandoseDelCeroEnElPuntoMedio(double entrada, double esperado)
    {
        var resultado = ReglasMonetarias.Redondear((decimal)entrada);

        resultado.Should().Be((decimal)esperado);
    }

    // $ 1.663,40 con cada política (el ejemplo de RedondeoPrecioEnum); el mínimo (costo) no interviene.
    [Theory]
    [InlineData(RedondeoPrecioEnum.EnteroMasCercano, 1663)]
    [InlineData(RedondeoPrecioEnum.MultiploDe5Superior, 1665)]
    [InlineData(RedondeoPrecioEnum.MultiploDe5Inferior, 1660)]
    [InlineData(RedondeoPrecioEnum.MultiploDe10Superior, 1670)]
    [InlineData(RedondeoPrecioEnum.MultiploDe10Inferior, 1660)]
    [InlineData(RedondeoPrecioEnum.MultiploDe100Superior, 1700)]
    [InlineData(RedondeoPrecioEnum.MultiploDe100Inferior, 1600)]
    public void RedondearPrecio_ConCadaPolitica_DevuelveElMultiploCorrespondiente(RedondeoPrecioEnum redondeo, int esperado)
    {
        var resultado = ReglasMonetarias.RedondearPrecio(1663.40m, redondeo, minimo: 1000m);

        resultado.Should().Be(esperado);
    }

    [Theory]
    [InlineData(1663.50, 1664)]
    [InlineData(1663.49, 1663)]
    public void RedondearPrecio_EnteroMasCercanoEnElPuntoMedio_SeAlejaDelCero(double precio, int esperado)
    {
        var resultado = ReglasMonetarias.RedondearPrecio((decimal)precio, RedondeoPrecioEnum.EnteroMasCercano, minimo: 0m);

        resultado.Should().Be(esperado);
    }

    [Theory]
    [InlineData(RedondeoPrecioEnum.MultiploDe5Superior)]
    [InlineData(RedondeoPrecioEnum.MultiploDe5Inferior)]
    [InlineData(RedondeoPrecioEnum.MultiploDe10Superior)]
    [InlineData(RedondeoPrecioEnum.MultiploDe10Inferior)]
    [InlineData(RedondeoPrecioEnum.MultiploDe100Superior)]
    [InlineData(RedondeoPrecioEnum.MultiploDe100Inferior)]
    public void RedondearPrecio_PrecioYaMultiplo_NoLoCambia(RedondeoPrecioEnum redondeo)
    {
        var resultado = ReglasMonetarias.RedondearPrecio(1700m, redondeo, minimo: 0m);

        resultado.Should().Be(1700m);
    }

    [Theory]
    [InlineData(RedondeoPrecioEnum.MultiploDe100Inferior, 100)]
    [InlineData(RedondeoPrecioEnum.MultiploDe10Inferior, 50)]
    [InlineData(RedondeoPrecioEnum.EnteroMasCercano, 41)]
    public void RedondearPrecio_InferiorQueQuedaBajoElCosto_UsaElMultiploSuperior(RedondeoPrecioEnum redondeo, int esperado)
    {
        // Costo $ 40,30 con un markup mínimo = $ 40,40: el redondeo propio (0, 40 o 40) sería una venta a pérdida
        var resultado = ReglasMonetarias.RedondearPrecio(40.40m, redondeo, minimo: 40.30m);

        resultado.Should().Be(esperado);
    }

    [Fact]
    public void RedondearPrecio_PoliticaInexistente_LanzaArgumentOutOfRangeException()
    {
        var accion = () => ReglasMonetarias.RedondearPrecio(100m, (RedondeoPrecioEnum)99, minimo: 0m);

        accion.Should().Throw<ArgumentOutOfRangeException>();
    }
}
