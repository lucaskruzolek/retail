using FluentAssertions;
using Retail.App.Helpers;
using Xunit;

namespace Retail.App.UnitTests.Helpers;

public class EntradaTextoHelperTests
{
    [Theory]
    [InlineData(TipoEntradaTexto.Entero)]
    [InlineData(TipoEntradaTexto.NumeroDecimal)]
    [InlineData(TipoEntradaTexto.Telefono)]
    [InlineData(TipoEntradaTexto.Documento)]
    public void EsTextoValido_ConTextoVacio_DevuelveVerdaderoParaTodoTipo(TipoEntradaTexto tipo)
    {
        EntradaTextoHelper.EsTextoValido(tipo, string.Empty).Should().BeTrue();
        EntradaTextoHelper.EsTextoValido(tipo, null).Should().BeTrue();
    }

    [Theory]
    [InlineData("7")]
    [InlineData("120")]
    public void EsTextoValido_EnteroConDigitos_DevuelveVerdadero(string texto)
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.Entero, texto).Should().BeTrue();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("1a")]
    [InlineData("-3")]
    [InlineData("1,5")]
    [InlineData("1 2")]
    public void EsTextoValido_EnteroConCaracteresNoNumericos_DevuelveFalso(string texto)
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.Entero, texto).Should().BeFalse();
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12,")]
    [InlineData(",5")]
    [InlineData("1234,56")]
    public void EsTextoValido_DecimalConUnSeparador_DevuelveVerdadero(string texto)
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.NumeroDecimal, texto).Should().BeTrue();
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("1.5")]
    [InlineData("-5")]
    [InlineData("12a")]
    [InlineData("1 000")]
    public void EsTextoValido_DecimalConDobleSeparadorSignoOLetras_DevuelveFalso(string texto)
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.NumeroDecimal, texto).Should().BeFalse();
    }

    [Fact]
    public void EsTextoValido_DecimalConSeparadorPunto_RespetaElSeparadorRecibido()
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.NumeroDecimal, "1.5", ".").Should().BeTrue();
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.NumeroDecimal, "1,5", ".").Should().BeFalse();
    }

    [Theory]
    [InlineData("+54 11 4567-8901")]
    [InlineData("(011) 4567-8901")]
    [InlineData("1145678901")]
    public void EsTextoValido_TelefonoConSimbolosPermitidos_DevuelveVerdadero(string texto)
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.Telefono, texto).Should().BeTrue();
    }

    [Theory]
    [InlineData("11-abcd")]
    [InlineData("4567.8901")]
    [InlineData("int. 23")]
    [InlineData("11/4567")]
    public void EsTextoValido_TelefonoConLetrasOSimbolosNoPermitidos_DevuelveFalso(string texto)
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.Telefono, texto).Should().BeFalse();
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("20-12345678-6")]
    [InlineData("12.345.678")]
    [InlineData("AB123456")]
    public void EsTextoValido_DocumentoConFormatosHabituales_DevuelveVerdadero(string texto)
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.Documento, texto).Should().BeTrue();
    }

    [Theory]
    [InlineData("20_12345678_6")]
    [InlineData("1234/5678")]
    [InlineData("ñandú")]
    [InlineData("12,345")]
    public void EsTextoValido_DocumentoConSimbolosNoPermitidos_DevuelveFalso(string texto)
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.Documento, texto).Should().BeFalse();
    }

    [Fact]
    public void EsTextoValido_TipoNinguno_NoRestringeNada()
    {
        EntradaTextoHelper.EsTextoValido(TipoEntradaTexto.Ninguno, "cualquier cosa / 123").Should().BeTrue();
    }
}
