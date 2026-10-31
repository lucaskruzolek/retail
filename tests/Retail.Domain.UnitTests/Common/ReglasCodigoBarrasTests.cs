using FluentAssertions;
using Retail.Domain.Common;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

public class ReglasCodigoBarrasTests
{
    [Theory]
    [InlineData("4006381333931", "4006381333931")]
    [InlineData(" 4006381333931 ", "4006381333931")]
    [InlineData("7798100123455", "7798100123455")]
    [InlineData("96385074", "96385074")]
    public void NormalizarGtin_CodigoValido_DevuelveElMismoCodigo(string codigo, string esperado)
    {
        ReglasCodigoBarras.NormalizarGtin(codigo).Should().Be(esperado);
    }

    [Theory]
    [InlineData("036000291452")]
    [InlineData("36000291452")]
    public void NormalizarGtin_UpcA_DevuelveGtin13ConCeroInicial(string codigo)
    {
        // 11 dígitos: el UPC-A al que Excel le quitó el cero inicial.
        ReglasCodigoBarras.NormalizarGtin(codigo).Should().Be("0036000291452");
    }

    [Theory]
    [InlineData("00036000291452")]
    [InlineData("10036000291459")]
    public void NormalizarGtin_Gtin14Valido_DevuelveElGtin13DelProducto(string codigo)
    {
        // Con indicador 1 el verificador del GTIN-14 (9) no es el del GTIN-13 (2): hay que recalcularlo (M-28).
        ReglasCodigoBarras.NormalizarGtin(codigo).Should().Be("0036000291452");
    }

    [Theory]
    [InlineData("4006381333932")]
    [InlineData("96385075")]
    [InlineData("036000291453")]
    [InlineData("10036000291452")]
    public void NormalizarGtin_DigitoVerificadorIncorrecto_DevuelveNull(string codigo)
    {
        ReglasCodigoBarras.NormalizarGtin(codigo).Should().BeNull();
    }

    [Fact]
    public void NormalizarGtin_CirculacionRestringida_DevuelveNull()
    {
        // Verificador correcto, pero los GTIN-13 que empiezan con 2 son códigos internos de cada comercio.
        ReglasCodigoBarras.NormalizarGtin("2000000000015").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC1234567890")]
    [InlineData("4006381-333931")]
    [InlineData("1234567890")]
    [InlineData("123456789012345")]
    public void NormalizarGtin_ValorNoUtilizable_DevuelveNull(string? codigo)
    {
        ReglasCodigoBarras.NormalizarGtin(codigo).Should().BeNull();
    }

    [Fact]
    public void ObtenerPrefijoTitular_Gtin13_DevuelveLosSietePrimerosDigitos()
    {
        ReglasCodigoBarras.ObtenerPrefijoTitular("7798100123455").Should().Be("7798100");
    }

    [Theory]
    [InlineData("96385074")]
    [InlineData(null)]
    public void ObtenerPrefijoTitular_SinGtin13_DevuelveNull(string? gtin)
    {
        // Un EAN-8 se asigna producto por producto: no identifica a un titular.
        ReglasCodigoBarras.ObtenerPrefijoTitular(gtin).Should().BeNull();
    }

    [Theory]
    [InlineData("7798100", "7798100")]
    [InlineData(" 0036000 ", "0036000")]
    public void ExigirPrefijoTitular_PrefijoValido_DevuelveFormaCanonica(string prefijo, string esperado)
    {
        ReglasCodigoBarras.ExigirPrefijoTitular(prefijo).Should().Be(esperado);
        ReglasCodigoBarras.EsPrefijoTitularValido(prefijo).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("779810")]
    [InlineData("77981001")]
    [InlineData("779810A")]
    [InlineData("2000001")]
    public void ExigirPrefijoTitular_PrefijoInvalido_LanzaDomainException(string? prefijo)
    {
        var accion = () => ReglasCodigoBarras.ExigirPrefijoTitular(prefijo);

        accion.Should().Throw<DomainException>();
        ReglasCodigoBarras.EsPrefijoTitularValido(prefijo).Should().BeFalse();
    }

    [Theory]
    [InlineData("4006381333931", true)]
    [InlineData("10036000291459", true)]
    [InlineData("4006381333932", false)]
    [InlineData("7", false)]
    [InlineData("40063813339A1", false)]
    public void TieneDigitoVerificadorValido_SegunCodigo_DevuelveResultadoEsperado(string digitos, bool esperado)
    {
        ReglasCodigoBarras.TieneDigitoVerificadorValido(digitos).Should().Be(esperado);
    }

    [Theory]
    [InlineData("11", true)]
    [InlineData(" 512 ", true)]
    [InlineData("7791234567890", true)]
    [InlineData("7791234567891", true)] // verificador incorrecto: a propósito no se controla (M11)
    [InlineData("1234567", true)]       // largo no GS1: también se acepta
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("SOBRE-U", false)]
    [InlineData("779-1234", false)]
    [InlineData("12 34", false)]
    public void EsCodigoDeArticuloValido_SegunCodigo_DevuelveResultadoEsperado(string? codigo, bool esperado)
    {
        ReglasCodigoBarras.EsCodigoDeArticuloValido(codigo).Should().Be(esperado);
    }

    [Fact]
    public void EsCodigoDeArticuloValido_MasDeCincuentaDigitos_DevuelveFalso()
    {
        ReglasCodigoBarras.EsCodigoDeArticuloValido(new string('7', 51)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExigirCodigoDeArticulo_Vacio_DevuelveNulo(string? codigo)
    {
        ReglasCodigoBarras.ExigirCodigoDeArticulo(codigo).Should().BeNull();
    }

    [Fact]
    public void ExigirCodigoDeArticulo_ConLetras_LanzaDomainException()
    {
        var act = () => ReglasCodigoBarras.ExigirCodigoDeArticulo("EAN-13");

        act.Should().Throw<DomainException>().WithMessage("*solo admite dígitos*");
    }
}
