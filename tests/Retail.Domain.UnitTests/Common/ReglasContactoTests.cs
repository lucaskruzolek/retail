using FluentAssertions;
using Retail.Domain.Common;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

public class ReglasContactoTests
{
    [Fact]
    public void NormalizarEmail_ConMayusculasYEspacios_DevuelveMinusculasSinEspacios()
    {
        ReglasContacto.NormalizarEmail("  Ventas@Dist.COM ").Should().Be("ventas@dist.com");
    }

    [Theory]
    [InlineData("ventas@dist.com.ar")]
    [InlineData("a.b+compras@d.co")]
    [InlineData("Ventas@Dist.com")]
    public void EsEmailValido_DireccionReal_DevuelveTrue(string email)
    {
        ReglasContacto.EsEmailValido(email).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sinarroba")]
    [InlineData("a@b")]
    [InlineData("a b@c.com")]
    [InlineData("Nombre <a@b.com>")]
    [InlineData("a@b..com")]
    [InlineData("a@.com")]
    [InlineData("@dist.com")]
    [InlineData("ventas@")]
    public void EsEmailValido_FormatoInvalido_DevuelveFalse(string? email)
    {
        ReglasContacto.EsEmailValido(email).Should().BeFalse();
    }

    [Fact]
    public void EsEmailValido_SuperaLongitudMaxima_DevuelveFalse()
    {
        var email = new string('a', ReglasContacto.LongitudMaximaEmail) + "@dist.com";

        ReglasContacto.EsEmailValido(email).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void ExigirEmailOpcional_Vacio_DevuelveNull(string? email)
    {
        ReglasContacto.ExigirEmailOpcional(email).Should().BeNull();
    }

    [Fact]
    public void ExigirEmailOpcional_Valido_DevuelveNormalizado()
    {
        ReglasContacto.ExigirEmailOpcional(" Ventas@Dist.com ").Should().Be("ventas@dist.com");
    }

    [Fact]
    public void ExigirEmailOpcional_Invalido_LanzaDomainException()
    {
        var act = () => ReglasContacto.ExigirEmailOpcional("a@b");

        act.Should().Throw<DomainException>().WithMessage("*a@b*");
    }

    [Theory]
    [InlineData("(011) 4555-1234", "01145551234")]
    [InlineData("+54 9 11 4555-1234", "+5491145551234")]
    [InlineData("011.4555.1234", "01145551234")]
    public void NormalizarTelefono_ConSeparadores_DejaSoloDigitosYMasInicial(string entrada, string esperado)
    {
        ReglasContacto.NormalizarTelefono(entrada).Should().Be(esperado);
    }

    [Theory]
    [InlineData("(011) 4555-1234")]
    [InlineData("+54 9 11 4555-1234")]
    [InlineData("45551234")]
    public void EsTelefonoValido_TelefonoReal_DevuelveTrue(string telefono)
    {
        ReglasContacto.EsTelefonoValido(telefono).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("llamar a la tarde")]
    [InlineData("1234567")]
    [InlineData("1234567890123456")]
    [InlineData("54+91145551234")]
    [InlineData("++541145551234")]
    [InlineData("٣٣٣٣٣٣٣٣")]
    public void EsTelefonoValido_FormatoInvalido_DevuelveFalse(string? telefono)
    {
        ReglasContacto.EsTelefonoValido(telefono).Should().BeFalse();
    }

    [Fact]
    public void ExigirTelefonoOpcional_Vacio_DevuelveNull()
    {
        ReglasContacto.ExigirTelefonoOpcional("  ").Should().BeNull();
    }

    [Fact]
    public void ExigirTelefonoOpcional_ConLetras_LanzaDomainException()
    {
        var act = () => ReglasContacto.ExigirTelefonoOpcional("llamar a la tarde");

        act.Should().Throw<DomainException>();
    }
}
