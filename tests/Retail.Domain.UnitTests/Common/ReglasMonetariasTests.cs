using FluentAssertions;
using Retail.Domain.Common;
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
}
