using FluentAssertions;
using Retail.Domain.Common;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Common;

public class ReglasClasificacionTests
{
    [Theory]
    [InlineData("lápiz", "LAPIZ")]
    [InlineData("  Cuaderno   Rivadavia ", "CUADERNO RIVADAVIA")]
    [InlineData("ÚTILES\tescolares", "UTILES ESCOLARES")]
    [InlineData("AÑO", "ANO")]
    [InlineData("CUADER.C/E", "CUADER.C/E")]
    public void NormalizarTexto_ConTildesYEspacios_DevuelveFormaDeComparacion(string entrada, string esperado)
    {
        ReglasClasificacion.NormalizarTexto(entrada).Should().Be(esperado);
    }

    [Fact]
    public void NormalizarTexto_TildeCombinada_LaQuitaIgualQueLaCompuesta()
    {
        // "e" + tilde combinada (U+0301), como llega al pegar desde otros programas.
        ReglasClasificacion.NormalizarTexto("lépiz").Should().Be("LEPIZ");
    }

    [Theory]
    [InlineData("**Cuaderno  tapa dura", "CUADERNO TAPA DURA")]
    [InlineData("  * lápiz negro ", "LÁPIZ NEGRO")]
    [InlineData("***", "")]
    public void LimpiarDescripcion_ConPrefijosYEspacios_QuitaElControlYConservaLasTildes(string descripcion, string esperado)
    {
        ReglasClasificacion.LimpiarDescripcion(descripcion).Should().Be(esperado);
    }

    [Fact]
    public void TerminoDeDescripcion_PrimeraPalabraConTilde_DevuelveLaFormaDeComparacion()
    {
        ReglasClasificacion.TerminoDeDescripcion("**Lápiz negro HB").Should().Be("LAPIZ");
    }

    [Fact]
    public void PalabrasParaMarca_MarcaPegadaConBarraYPuntoFinal_LasSeparaSinElTermino()
    {
        ReglasClasificacion.PalabrasParaMarca("LAPIZ NEGRO KOBY/DATA.").Should().Equal("NEGRO", "KOBY", "DATA");
    }

    [Theory]
    [InlineData("")]
    [InlineData("MOCHILA")]
    public void PalabrasParaMarca_SinPalabrasDespuesDelTermino_DevuelveVacio(string descripcion)
    {
        ReglasClasificacion.PalabrasParaMarca(descripcion).Should().BeEmpty();
    }

    [Theory]
    [InlineData("mochila", "MOCHILA")]
    [InlineData(" Cuadér ", "CUADER")]
    [InlineData("100", "100")]
    public void ExigirTermino_UnaPalabra_DevuelveFormaNormalizada(string termino, string esperado)
    {
        ReglasClasificacion.ExigirTermino(termino).Should().Be(esperado);
        ReglasClasificacion.EsTerminoValido(termino).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SET ESCOLAR")]
    public void ExigirTermino_VacioOConEspacios_LanzaDomainException(string? termino)
    {
        var accion = () => ReglasClasificacion.ExigirTermino(termino);

        accion.Should().Throw<DomainException>();
        ReglasClasificacion.EsTerminoValido(termino).Should().BeFalse();
    }

    [Fact]
    public void ExigirTermino_SuperaLongitudMaxima_LanzaDomainException()
    {
        var termino = new string('A', ReglasClasificacion.LongitudMaximaTermino + 1);

        var accion = () => ReglasClasificacion.ExigirTermino(termino);

        accion.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("husar", "HUSAR")]
    [InlineData(" Artística  Dibu ", "ARTISTICA DIBU")]
    public void ExigirAlias_TextoValido_DevuelveFormaNormalizada(string alias, string esperado)
    {
        ReglasClasificacion.ExigirAlias(alias).Should().Be(esperado);
        ReglasClasificacion.EsAliasValido(alias).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExigirAlias_Vacio_LanzaDomainException(string? alias)
    {
        var accion = () => ReglasClasificacion.ExigirAlias(alias);

        accion.Should().Throw<DomainException>();
        ReglasClasificacion.EsAliasValido(alias).Should().BeFalse();
    }

    [Fact]
    public void ExigirAlias_SuperaLongitudMaxima_LanzaDomainException()
    {
        var alias = new string('A', ReglasClasificacion.LongitudMaximaAlias + 1);

        var accion = () => ReglasClasificacion.ExigirAlias(alias);

        accion.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("Cuadernos  t/d")]
    [InlineData("ESCOLAR")]
    public void EsTextoDeProveedorValido_TextoValido_CoincideConExigir(string texto)
    {
        ReglasClasificacion.EsTextoDeProveedorValido(texto).Should().BeTrue();
        var accion = () => ReglasClasificacion.ExigirTextoDeProveedor(texto, "texto");
        accion.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EsTextoDeProveedorValido_Vacio_DevuelveFalse(string? texto)
    {
        ReglasClasificacion.EsTextoDeProveedorValido(texto).Should().BeFalse();
    }

    [Fact]
    public void EsTextoDeProveedorValido_SuperaLongitudMaxima_DevuelveFalse()
    {
        var texto = new string('A', ReglasClasificacion.LongitudMaximaTextoDeProveedor + 1);

        ReglasClasificacion.EsTextoDeProveedorValido(texto).Should().BeFalse();
    }
}
