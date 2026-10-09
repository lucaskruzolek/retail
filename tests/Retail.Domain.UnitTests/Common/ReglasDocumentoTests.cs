using FluentAssertions;
using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

public class ReglasDocumentoTests
{
    [Theory]
    [InlineData(TipoDocumentoEnum.Dni, true)]
    [InlineData(TipoDocumentoEnum.Cuil, true)]
    [InlineData(TipoDocumentoEnum.Pasaporte, true)]
    [InlineData(TipoDocumentoEnum.Cuit, false)]
    public void IdentificaPersonaFisica_SegunTipo_DevuelveResultadoEsperado(TipoDocumentoEnum tipo, bool esperado)
    {
        ReglasDocumento.IdentificaPersonaFisica(tipo).Should().Be(esperado);
    }

    [Theory]
    [InlineData(TipoDocumentoEnum.Dni, "12.345.678", "12345678")]
    [InlineData(TipoDocumentoEnum.Dni, " 12 345 678 ", "12345678")]
    [InlineData(TipoDocumentoEnum.Cuit, "20-12345678-6", "20123456786")]
    [InlineData(TipoDocumentoEnum.Cuil, "27-12345678-0", "27123456780")]
    [InlineData(TipoDocumentoEnum.Pasaporte, " aaa 123456", "AAA123456")]
    public void Normalizar_ConSeparadores_DevuelveFormaCanonica(TipoDocumentoEnum tipo, string entrada, string esperado)
    {
        ReglasDocumento.Normalizar(tipo, entrada).Should().Be(esperado);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567")]
    [InlineData("12.345.678")]
    public void EsValido_DniValido_DevuelveTrue(string dni)
    {
        ReglasDocumento.EsValido(TipoDocumentoEnum.Dni, dni).Should().BeTrue();
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("123456789")]
    [InlineData("ABC1234")]
    [InlineData("")]
    public void EsValido_DniInvalido_DevuelveFalse(string dni)
    {
        ReglasDocumento.EsValido(TipoDocumentoEnum.Dni, dni).Should().BeFalse();
    }

    [Theory]
    [InlineData("20123456786")]
    [InlineData("20-12345678-6")]
    [InlineData("30712345671")]
    [InlineData("33-69345023-9")]
    [InlineData("27123456780")]
    public void EsCuitValido_DigitoVerificadorCorrecto_DevuelveTrue(string cuit)
    {
        ReglasDocumento.EsCuitValido(cuit).Should().BeTrue();
    }

    [Theory]
    [InlineData("20123456780")]
    [InlineData("11111111111")]
    [InlineData("40123456789")]
    [InlineData("2012345678")]
    [InlineData("201234567861")]
    [InlineData("20A23456786")]
    [InlineData(null)]
    public void EsCuitValido_DigitoPrefijoOLongitudIncorrectos_DevuelveFalse(string? cuit)
    {
        ReglasDocumento.EsCuitValido(cuit).Should().BeFalse();
    }

    [Theory]
    [InlineData("20000000010")]
    [InlineData("20000000013")]
    [InlineData("20000000019")]
    public void EsCuitValido_RestoDaDiez_NingunDigitoEsValido(string cuit)
    {
        // Para estos 10 primeros dígitos, 11 - (suma % 11) = 10: ARCA nunca emite esa combinación.
        ReglasDocumento.EsCuitValido(cuit).Should().BeFalse();
    }

    [Fact]
    public void EsCuitValido_RestoDaOnce_ElDigitoVerificadorEsCero()
    {
        // La suma ponderada de "2000000006" es 22: 11 - (22 % 11) = 11, que equivale a 0.
        ReglasDocumento.EsCuitValido("20000000060").Should().BeTrue();
    }

    [Fact]
    public void EsCuilValido_PrefijoDePersonaJuridica_DevuelveFalse()
    {
        // Arrange: 30-71234567-1 es un CUIT de empresa con dígito verificador correcto.
        const string cuitDeEmpresa = "30712345671";

        // Act & Assert
        ReglasDocumento.EsCuitValido(cuitDeEmpresa).Should().BeTrue();
        ReglasDocumento.EsCuilValido(cuitDeEmpresa).Should().BeFalse();
    }

    [Theory]
    [InlineData("AAA123456")]
    [InlineData("ab1234")]
    public void EsValido_PasaporteValido_DevuelveTrue(string pasaporte)
    {
        ReglasDocumento.EsValido(TipoDocumentoEnum.Pasaporte, pasaporte).Should().BeTrue();
    }

    [Theory]
    [InlineData("ab-12")]
    [InlineData("AAA12345678")]
    [InlineData("AAA#2345")]
    public void EsValido_PasaporteInvalido_DevuelveFalse(string pasaporte)
    {
        ReglasDocumento.EsValido(TipoDocumentoEnum.Pasaporte, pasaporte).Should().BeFalse();
    }

    [Fact]
    public void Exigir_CuitConGuiones_DevuelveSoloDigitos()
    {
        ReglasDocumento.Exigir(TipoDocumentoEnum.Cuit, "20-12345678-6").Should().Be("20123456786");
    }

    [Fact]
    public void Exigir_CuitConDigitoVerificadorIncorrecto_LanzaDomainExceptionDescriptiva()
    {
        var act = () => ReglasDocumento.Exigir(TipoDocumentoEnum.Cuit, "20123456780");

        act.Should().Throw<DomainException>().WithMessage("*20123456780*CUIT*verificador*");
    }
}
