using FluentAssertions;
using Retail.App.Helpers;
using Xunit;

namespace Retail.App.UnitTests.Helpers;

public class EntradaNumericaHelperTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("120")]
    public void SonSoloDigitos_ConDigitos_DevuelveVerdadero(string texto)
    {
        EntradaNumericaHelper.SonSoloDigitos(texto).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    [InlineData("-3")]
    [InlineData("1,5")]
    [InlineData("1.5")]
    [InlineData("12 ")]
    [InlineData("٣")]
    public void SonSoloDigitos_ConOtrosCaracteres_DevuelveFalso(string? texto)
    {
        EntradaNumericaHelper.SonSoloDigitos(texto).Should().BeFalse();
    }

    [Theory]
    [InlineData("40", ".")]
    [InlineData("40.5", ".")]
    [InlineData("40.", ".")]
    [InlineData(".5", ".")]
    [InlineData("642,90", ",")]
    public void EsDecimalEnConstruccion_ConDigitosYUnSeparador_DevuelveVerdadero(string texto, string separador)
    {
        EntradaNumericaHelper.EsDecimalEnConstruccion(texto, separador).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, ".")]
    [InlineData("", ".")]
    [InlineData("4.0.5", ".")]
    [InlineData("6,5", ".")]
    [InlineData("-3", ".")]
    [InlineData("1 000", ".")]
    [InlineData("4a", ".")]
    public void EsDecimalEnConstruccion_ConOtroSeparadorOCaracteres_DevuelveFalso(string? texto, string separador)
    {
        EntradaNumericaHelper.EsDecimalEnConstruccion(texto, separador).Should().BeFalse();
    }
}
