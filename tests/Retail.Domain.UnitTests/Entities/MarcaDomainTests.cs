using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias para la entidad de dominio Marca.
/// </summary>
public class MarcaDomainTests
{
    [Fact]
    public void Crear_ConEspaciosSobrantes_NormalizaElNombre()
    {
        var entidad = Marca.Crear("  Lápices   de  Color ");

        entidad.NombreMarca.Should().Be("Lápices de Color");
        entidad.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Crear_ConMayusculasPropias_PreservaLaGrafia()
    {
        Marca.Crear("3M").NombreMarca.Should().Be("3M");
        Marca.Crear("  BIC ").NombreMarca.Should().Be("BIC");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_ConNombreVacio_LanzaDomainException(string? nombre)
    {
        var act = () => Marca.Crear(nombre!);

        act.Should().Throw<DomainException>().WithMessage("*nombre de la marca*");
    }

    [Fact]
    public void Crear_ConCienCaracteres_Acepta()
    {
        var nombre = new string('a', 100);

        Marca.Crear(nombre).NombreMarca.Should().Be(nombre);
    }

    [Fact]
    public void Crear_ConCientoUnCaracteres_LanzaDomainException()
    {
        var act = () => Marca.Crear(new string('a', 101));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Renombrar_ConNombreValido_NormalizaYReemplaza()
    {
        var entidad = Marca.Crear("Original");

        entidad.Renombrar("  Nuevo   Nombre ");

        entidad.NombreMarca.Should().Be("Nuevo Nombre");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Renombrar_ConNombreInvalido_LanzaDomainExceptionYNoModificaElNombre(string? nombre)
    {
        var entidad = Marca.Crear("Original");

        var act = () => entidad.Renombrar(nombre!);

        act.Should().Throw<DomainException>();
        entidad.NombreMarca.Should().Be("Original");
    }

    [Fact]
    public void Renombrar_ConCientoUnCaracteres_LanzaDomainException()
    {
        var entidad = Marca.Crear("Original");

        var act = () => entidad.Renombrar(new string('a', 101));

        act.Should().Throw<DomainException>();
        entidad.NombreMarca.Should().Be("Original");
    }
}
