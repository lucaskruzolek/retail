using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias para la entidad de dominio Categoria.
/// </summary>
public class CategoriaDomainTests
{
    [Fact]
    public void Crear_ConEspaciosSobrantes_NormalizaElNombre()
    {
        var entidad = Categoria.Crear("  Lápices   de  Color ");

        entidad.NombreCategoria.Should().Be("Lápices de Color");
        entidad.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Crear_ConMayusculasPropias_PreservaLaGrafia()
    {
        Categoria.Crear("3M").NombreCategoria.Should().Be("3M");
        Categoria.Crear("  BIC ").NombreCategoria.Should().Be("BIC");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Crear_ConNombreVacio_LanzaDomainException(string? nombre)
    {
        var act = () => Categoria.Crear(nombre!);

        act.Should().Throw<DomainException>().WithMessage("*nombre de la categoría*");
    }

    [Fact]
    public void Crear_ConCienCaracteres_Acepta()
    {
        var nombre = new string('a', 100);

        Categoria.Crear(nombre).NombreCategoria.Should().Be(nombre);
    }

    [Fact]
    public void Crear_ConCientoUnCaracteres_LanzaDomainException()
    {
        var act = () => Categoria.Crear(new string('a', 101));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Renombrar_ConNombreValido_NormalizaYReemplaza()
    {
        var entidad = Categoria.Crear("Original");

        entidad.Renombrar("  Nuevo   Nombre ");

        entidad.NombreCategoria.Should().Be("Nuevo Nombre");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Renombrar_ConNombreInvalido_LanzaDomainExceptionYNoModificaElNombre(string? nombre)
    {
        var entidad = Categoria.Crear("Original");

        var act = () => entidad.Renombrar(nombre!);

        act.Should().Throw<DomainException>();
        entidad.NombreCategoria.Should().Be("Original");
    }

    [Fact]
    public void Renombrar_ConCientoUnCaracteres_LanzaDomainException()
    {
        var entidad = Categoria.Crear("Original");

        var act = () => entidad.Renombrar(new string('a', 101));

        act.Should().Throw<DomainException>();
        entidad.NombreCategoria.Should().Be("Original");
    }
}
