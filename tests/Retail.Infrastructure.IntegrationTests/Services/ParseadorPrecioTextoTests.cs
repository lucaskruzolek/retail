using FluentAssertions;
using Retail.Infrastructure.ExternalServices.Excel;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

public class ParseadorPrecioTextoTests
{
    [Theory]
    [InlineData("1.500", 1500)]
    [InlineData("1,50", 1.5)]
    [InlineData("1.500,50", 1500.50)]
    [InlineData("1,500.50", 1500.50)]
    [InlineData("1.500.000", 1500000)]
    [InlineData("1,500,000", 1500000)]
    [InlineData("12.500", 12500)]
    [InlineData("1.5", 1.5)]
    [InlineData("12.50", 12.50)]
    [InlineData("1500.5", 1500.5)]
    [InlineData("0.500", 0.5)]
    [InlineData("1500", 1500)]
    [InlineData("$ 1.234,56", 1234.56)]
    [InlineData(" 99,99 ", 99.99)]
    [InlineData("-10,5", -10.5)]
    public void TryParse_ConTextoValido_InterpretaConvencionEsAr(string texto, double esperado)
    {
        bool resultado = ParseadorPrecioTexto.TryParse(texto, out decimal valor);

        resultado.Should().BeTrue();
        valor.Should().Be((decimal)esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1,2,3.4,5")]
    [InlineData("12abc")]
    [InlineData("1.500,50,10")]
    public void TryParse_ConTextoInvalido_RetornaFalse(string? texto)
    {
        bool resultado = ParseadorPrecioTexto.TryParse(texto, out _);

        resultado.Should().BeFalse();
    }
}
