using FluentAssertions;
using Retail.Domain.Common;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

public class ReglasTextoTests
{
    [Theory]
    [InlineData("  juan   carlos  ", "Juan Carlos")]
    [InlineData("MARÍA DE LOS ÁNGELES", "María de los Ángeles")]
    [InlineData("de la fuente", "De la Fuente")]
    [InlineData("o’connor", "O'Connor")]
    [InlineData("maría-josé", "María-José")]
    [InlineData("ñandú", "Ñandú")]
    [InlineData("juan\tpérez", "Juan Pérez")]
    public void NormalizarNombreDePersona_TextoSinFormato_DevuelveFormaCanonica(string entrada, string esperado)
    {
        ReglasTexto.NormalizarNombreDePersona(entrada).Should().Be(esperado);
    }

    [Fact]
    public void NormalizarNombreDePersona_TildeCombinadaNfd_LaComponeEnUnSoloCaracter()
    {
        // Arrange: "José" escrito como "e" + tilde combinada (U+0301), como llega al pegar desde otros programas.
        var descompuesto = "josé";

        // Act
        var normalizado = ReglasTexto.NormalizarNombreDePersona(descompuesto);

        // Assert
        normalizado.Should().Be("José");
        normalizado.Should().HaveLength(4);
    }

    [Theory]
    [InlineData("José")]
    [InlineData("O'Connor")]
    [InlineData("María-José")]
    [InlineData("Ñandú")]
    [InlineData("María de los Ángeles")]
    [InlineData("Li")]
    public void EsNombreDePersonaValido_NombreReal_DevuelveTrue(string nombre)
    {
        ReglasTexto.EsNombreDePersonaValido(nombre).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Juan123")]
    [InlineData("1234")]
    [InlineData("@@@")]
    [InlineData("😀😀😀")]
    [InlineData("a--b")]
    [InlineData("-Juan")]
    [InlineData("Juan-")]
    [InlineData("J")]
    public void EsNombreDePersonaValido_TextoConDigitosSimbolosOMuyCorto_DevuelveFalse(string? nombre)
    {
        ReglasTexto.EsNombreDePersonaValido(nombre).Should().BeFalse();
    }

    [Fact]
    public void EsNombreDePersonaValido_SuperaLongitudMaxima_DevuelveFalse()
    {
        var nombre = new string('a', ReglasTexto.LongitudMaximaNombrePersona + 1);

        ReglasTexto.EsNombreDePersonaValido(nombre).Should().BeFalse();
    }

    [Fact]
    public void EsNombreDePersonaValido_LongitudMaximaPropiaMayor_AceptaNombreLargo()
    {
        // Arrange: un "Apellido y Nombre" de cliente supera los 50 caracteres de un nombre suelto.
        var nombreCompleto = string.Join(' ', Enumerable.Repeat("Bartolomé", 8));

        // Act & Assert
        ReglasTexto.EsNombreDePersonaValido(nombreCompleto).Should().BeFalse();
        ReglasTexto.EsNombreDePersonaValido(nombreCompleto, 150).Should().BeTrue();
    }

    [Fact]
    public void ExigirNombreDePersona_NombreValido_DevuelveNormalizado()
    {
        ReglasTexto.ExigirNombreDePersona("  PÉREZ ", "apellido").Should().Be("Pérez");
    }

    [Fact]
    public void ExigirNombreDePersona_NombreConDigitos_LanzaDomainExceptionConElCampo()
    {
        var act = () => ReglasTexto.ExigirNombreDePersona("Juan123", "nombre");

        act.Should().Throw<DomainException>().WithMessage("*nombre*Juan123*");
    }

    [Fact]
    public void NormalizarRazonSocial_TextoConEspacios_ColapsaEspaciosYRespetaMayusculas()
    {
        ReglasTexto.NormalizarRazonSocial("  LIBRERÍA   el  Ateneo S.R.L. ").Should().Be("LIBRERÍA el Ateneo S.R.L.");
    }

    [Theory]
    [InlineData("3M Argentina S.A.")]
    [InlineData("López & Hnos. S.R.L.")]
    [InlineData("Distribuidora Nº 1")]
    [InlineData("Papelera (Sucursal Centro)")]
    [InlineData("O’Higgins Insumos")]
    public void EsRazonSocialValida_RazonSocialReal_DevuelveTrue(string razonSocial)
    {
        ReglasTexto.EsRazonSocialValida(razonSocial).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("@@@")]
    [InlineData("😀 S.A.")]
    [InlineData("Librería \"El Ateneo\"")]
    [InlineData("A")]
    public void EsRazonSocialValida_SinLetrasOConSimbolosNoAdmitidos_DevuelveFalse(string? razonSocial)
    {
        ReglasTexto.EsRazonSocialValida(razonSocial).Should().BeFalse();
    }

    [Fact]
    public void EsRazonSocialValida_SuperaLongitudMaxima_DevuelveFalse()
    {
        var razonSocial = new string('a', ReglasTexto.LongitudMaximaRazonSocial + 1);

        ReglasTexto.EsRazonSocialValida(razonSocial).Should().BeFalse();
    }

    [Fact]
    public void ExigirRazonSocial_SoloDigitos_LanzaDomainException()
    {
        var act = () => ReglasTexto.ExigirRazonSocial("12345", "razón social");

        act.Should().Throw<DomainException>().WithMessage("*razón social*12345*");
    }

    [Theory]
    [InlineData("Av. Rivadavia 1234 3º B")]
    [InlineData("Calle 7 N° 845, La Plata")]
    public void EsDomicilioValido_DomicilioReal_DevuelveTrue(string domicilio)
    {
        ReglasTexto.EsDomicilioValido(domicilio).Should().BeTrue();
    }

    [Theory]
    [InlineData("----")]
    [InlineData("1234")]
    [InlineData("Av")]
    public void EsDomicilioValido_SinLetrasOMuyCorto_DevuelveFalse(string domicilio)
    {
        ReglasTexto.EsDomicilioValido(domicilio).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExigirDomicilioOpcional_Vacio_DevuelveNull(string? domicilio)
    {
        ReglasTexto.ExigirDomicilioOpcional(domicilio).Should().BeNull();
    }

    [Fact]
    public void ExigirDomicilioOpcional_ConEspaciosMultiples_DevuelveNormalizado()
    {
        ReglasTexto.ExigirDomicilioOpcional("  Av.   Rivadavia  1234 ").Should().Be("Av. Rivadavia 1234");
    }

    [Fact]
    public void ExigirDomicilioOpcional_Invalido_LanzaDomainException()
    {
        var act = () => ReglasTexto.ExigirDomicilioOpcional("----");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void NormalizarNombreUsuario_ConMayusculasYEspacios_DevuelveMinusculasSinEspaciosExternos()
    {
        ReglasTexto.NormalizarNombreUsuario("  JPerez ").Should().Be("jperez");
    }

    [Theory]
    [InlineData("jperez")]
    [InlineData("JPerez")]
    [InlineData("j.perez")]
    [InlineData("caja_2")]
    [InlineData("abc")]
    public void EsNombreUsuarioValido_LoginValido_DevuelveTrue(string nombreUsuario)
    {
        ReglasTexto.EsNombreUsuarioValido(nombreUsuario).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("...")]
    [InlineData("___")]
    [InlineData(".a.")]
    [InlineData("a..b")]
    [InlineData("jperez.")]
    [InlineData("ab")]
    [InlineData("j perez")]
    [InlineData("jpérez")]
    public void EsNombreUsuarioValido_FormatoInvalido_DevuelveFalse(string? nombreUsuario)
    {
        ReglasTexto.EsNombreUsuarioValido(nombreUsuario).Should().BeFalse();
    }

    [Fact]
    public void EsNombreUsuarioValido_SuperaLongitudMaxima_DevuelveFalse()
    {
        var nombreUsuario = new string('a', ReglasTexto.LongitudMaximaNombreUsuario + 1);

        ReglasTexto.EsNombreUsuarioValido(nombreUsuario).Should().BeFalse();
    }

    [Fact]
    public void ExigirNombreUsuario_LoginValido_DevuelveNormalizado()
    {
        ReglasTexto.ExigirNombreUsuario(" J.Perez ").Should().Be("j.perez");
    }

    [Fact]
    public void ExigirNombreUsuario_LoginInvalido_LanzaDomainException()
    {
        var act = () => ReglasTexto.ExigirNombreUsuario("123");

        act.Should().Throw<DomainException>();
    }
}
