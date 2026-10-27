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

    [Fact]
    public void AsignarPar_TerminoAmbiguo_CreaElParEnFormaDeComparacion()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("set");

        var par = termino.AsignarPar("geometría.", 7);

        par.SegundaPalabra.Should().Be("GEOMETRIA");
        par.IdCategoria.Should().Be(7);
        par.EsIgnorado.Should().BeFalse();
        termino.Pares.Should().ContainSingle().Which.Should().BeSameAs(par);
    }

    [Fact]
    public void AsignarPar_SinCategoria_QuedaIgnorado()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");

        var par = termino.AsignarPar("ESCOLAR", null);

        par.IdCategoria.Should().BeNull();
        par.EsIgnorado.Should().BeTrue();
    }

    [Fact]
    public void AsignarPar_ParExistente_ReemplazaElDestinoSinDuplicarlo()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        var original = termino.AsignarPar("GEOMETRIA", null);

        var reasignado = termino.AsignarPar("Geometria", 7);

        reasignado.Should().BeSameAs(original);
        reasignado.IdCategoria.Should().Be(7);
        termino.Pares.Should().ContainSingle();
    }

    [Fact]
    public void AsignarPar_TerminoConCategoria_LanzaDomainExceptionSinAgregarElPar()
    {
        var termino = TerminoClasificacion.CrearConCategoria("MOCHILA", 4);

        var accion = () => termino.AsignarPar("ESCOLAR", 7);

        accion.Should().Throw<DomainException>().WithMessage("*ambiguo*");
        termino.Pares.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AsignarPar_CategoriaNoPositiva_LanzaDomainExceptionYConservaElDestino(int idCategoria)
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        var par = termino.AsignarPar("GEOMETRIA", 7);

        var accion = () => termino.AsignarPar("GEOMETRIA", idCategoria);

        accion.Should().Throw<DomainException>();
        par.IdCategoria.Should().Be(7);
    }

    [Theory]
    [InlineData("DE")]
    [InlineData("A4")]
    [InlineData("TAPA DURA")]
    public void AsignarPar_SegundaPalabraInvalida_LanzaDomainException(string segundaPalabra)
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");

        var accion = () => termino.AsignarPar(segundaPalabra, 7);

        accion.Should().Throw<DomainException>();
        termino.Pares.Should().BeEmpty();
    }

    [Fact]
    public void QuitarPar_ParVigente_LoDaDeBajaYLiberaLaClave()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        var original = termino.AsignarPar("GEOMETRIA", 7);

        termino.QuitarPar("geometria");
        var nuevo = termino.AsignarPar("GEOMETRIA", 8);

        original.IsDeleted.Should().BeTrue();
        nuevo.Should().NotBeSameAs(original);
        nuevo.IdCategoria.Should().Be(8);
    }

    [Fact]
    public void QuitarPar_ParInexistente_LanzaDomainException()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");

        var accion = () => termino.QuitarPar("GEOMETRIA");

        accion.Should().Throw<DomainException>();
    }

    [Fact]
    public void QuitarParesHacia_Categoria_DaDeBajaSoloLosVigentesHaciaEllaYLosCuenta()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        var geometria = termino.AsignarPar("GEOMETRIA", 7);
        var compas = termino.AsignarPar("COMPAS", 7);
        var escolar = termino.AsignarPar("ESCOLAR", 8);
        var ignorado = termino.AsignarPar("REGALO", null);

        var cantidad = termino.QuitarParesHacia(7);

        cantidad.Should().Be(2);
        geometria.IsDeleted.Should().BeTrue();
        compas.IsDeleted.Should().BeTrue();
        escolar.IsDeleted.Should().BeFalse();
        ignorado.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void AsignarCategoria_TerminoAmbiguoConPares_DaDeBajaLosPares()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        var conCategoria = termino.AsignarPar("GEOMETRIA", 7);
        var ignorado = termino.AsignarPar("REGALO", null);

        termino.AsignarCategoria(4);

        conCategoria.IsDeleted.Should().BeTrue();
        ignorado.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void AsignarCategoria_CategoriaInvalida_ConservaLosPares()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        var par = termino.AsignarPar("GEOMETRIA", 7);

        var accion = () => termino.AsignarCategoria(0);

        accion.Should().Throw<DomainException>();
        par.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void MarkAsDeleted_TerminoConPares_DaDeBajaElTerminoYSusPares()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        var par = termino.AsignarPar("GEOMETRIA", 7);

        termino.MarkAsDeleted();

        termino.IsDeleted.Should().BeTrue();
        par.IsDeleted.Should().BeTrue();
    }
}
