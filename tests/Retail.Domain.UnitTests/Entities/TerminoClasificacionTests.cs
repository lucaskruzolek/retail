using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class TerminoClasificacionTests
{
    [Fact]
    public void CrearConCategoria_DatosValidos_NormalizaElTerminoYNoEsAmbiguo()
    {
        var termino = TerminoClasificacion.CrearConCategoria(" cuadér ", 4);

        termino.Termino.Should().Be("CUADER");
        termino.IdCategoria.Should().Be(4);
        termino.EsAmbiguo.Should().BeFalse();
    }

    [Fact]
    public void CrearAmbiguo_TerminoValido_NoTieneCategoria()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("set");

        termino.Termino.Should().Be("SET");
        termino.IdCategoria.Should().BeNull();
        termino.EsAmbiguo.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void CrearConCategoria_CategoriaNoPositiva_LanzaDomainException(int idCategoria)
    {
        var act = () => TerminoClasificacion.CrearConCategoria("MOCHILA", idCategoria);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("SET ESCOLAR")]
    public void CrearAmbiguo_TerminoInvalido_LanzaDomainException(string termino)
    {
        var act = () => TerminoClasificacion.CrearAmbiguo(termino);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AsignarCategoria_TerminoAmbiguo_DejaDeSerAmbiguo()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("TACO");

        termino.AsignarCategoria(9);

        termino.IdCategoria.Should().Be(9);
        termino.EsAmbiguo.Should().BeFalse();
    }

    [Fact]
    public void AsignarCategoria_CategoriaInvalida_LanzaDomainExceptionYConservaLaAnterior()
    {
        var termino = TerminoClasificacion.CrearConCategoria("MOCHILA", 4);

        var act = () => termino.AsignarCategoria(0);

        act.Should().Throw<DomainException>();
        termino.IdCategoria.Should().Be(4);
    }

    [Fact]
    public void MarcarAmbiguo_TerminoConCategoria_QuitaLaCategoria()
    {
        var termino = TerminoClasificacion.CrearConCategoria("GOMA", 4);

        termino.MarcarAmbiguo();

        termino.IdCategoria.Should().BeNull();
        termino.EsAmbiguo.Should().BeTrue();
    }
}
