using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias para la entidad de dominio Rubro.
/// </summary>
public class RubroDomainTests
{
    [Fact]
    public void Crear_ConEspaciosSobrantes_NormalizaElNombre()
    {
        var entidad = Rubro.Crear("  Lápices   de  Color ");

        entidad.NombreRubro.Should().Be("Lápices de Color");
        entidad.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Crear_ConMayusculasPropias_PreservaLaGrafia()
    {
        Rubro.Crear("3M").NombreRubro.Should().Be("3M");
        Rubro.Crear("  BIC ").NombreRubro.Should().Be("BIC");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_ConNombreVacio_LanzaDomainException(string? nombre)
    {
        var act = () => Rubro.Crear(nombre!);

        act.Should().Throw<DomainException>().WithMessage("*nombre del rubro*");
    }

    [Fact]
    public void Crear_ConCienCaracteres_Acepta()
    {
        var nombre = new string('a', 100);

        Rubro.Crear(nombre).NombreRubro.Should().Be(nombre);
    }

    [Fact]
    public void Crear_ConCientoUnCaracteres_LanzaDomainException()
    {
        var act = () => Rubro.Crear(new string('a', 101));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Renombrar_ConNombreValido_NormalizaYReemplaza()
    {
        var entidad = Rubro.Crear("Original");

        entidad.Renombrar("  Nuevo   Nombre ");

        entidad.NombreRubro.Should().Be("Nuevo Nombre");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Renombrar_ConNombreInvalido_LanzaDomainExceptionYNoModificaElNombre(string? nombre)
    {
        var entidad = Rubro.Crear("Original");

        var act = () => entidad.Renombrar(nombre!);

        act.Should().Throw<DomainException>();
        entidad.NombreRubro.Should().Be("Original");
    }

    [Fact]
    public void Renombrar_ConCientoUnCaracteres_LanzaDomainException()
    {
        var entidad = Rubro.Crear("Original");

        var act = () => entidad.Renombrar(new string('a', 101));

        act.Should().Throw<DomainException>();
        entidad.NombreRubro.Should().Be("Original");
    }
}
