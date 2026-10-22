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

    [Fact]
    public void AgregarAlias_TextoValido_LoGuardaNormalizado()
    {
        var entidad = Marca.Crear("Husares");

        var alias = entidad.AgregarAlias(" husar ");

        alias.Alias.Should().Be("HUSAR");
        entidad.Alias.Should().ContainSingle().Which.Should().BeSameAs(alias);
    }

    [Theory]
    [InlineData("Artística Dibu", "artistica dibu")]
    [InlineData("BIC", "bic")]
    public void AgregarAlias_IgualAlNombre_LanzaDomainException(string nombre, string alias)
    {
        var entidad = Marca.Crear(nombre);

        var act = () => entidad.AgregarAlias(alias);

        act.Should().Throw<DomainException>().WithMessage("*nombre de la marca*");
        entidad.Alias.Should().BeEmpty();
    }

    [Fact]
    public void AgregarAlias_Repetido_LanzaDomainException()
    {
        var entidad = Marca.Crear("Husares");
        entidad.AgregarAlias("HUSAR");

        var act = () => entidad.AgregarAlias("húsar");

        act.Should().Throw<DomainException>();
        entidad.Alias.Should().HaveCount(1);
    }

    [Fact]
    public void QuitarAlias_Existente_LoDaDeBajaYPermiteVolverAAgregarlo()
    {
        var entidad = Marca.Crear("Husares");
        var original = entidad.AgregarAlias("HUSAR");

        entidad.QuitarAlias("husar");
        var nuevo = entidad.AgregarAlias("HUSAR");

        original.IsDeleted.Should().BeTrue();
        nuevo.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void QuitarAlias_Inexistente_LanzaDomainException()
    {
        var entidad = Marca.Crear("Husares");

        var act = () => entidad.QuitarAlias("HUSAR");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Renombrar_ConUnAliasVigente_LanzaDomainExceptionYNoModificaElNombre()
    {
        var entidad = Marca.Crear("Husares");
        entidad.AgregarAlias("HUSAR");

        var act = () => entidad.Renombrar("Húsar");

        act.Should().Throw<DomainException>().WithMessage("*alias*");
        entidad.NombreMarca.Should().Be("Husares");
    }

    [Fact]
    public void AgregarPrefijo_PrefijoValido_LoGuarda()
    {
        var entidad = Marca.Crear("Mooving");

        var prefijo = entidad.AgregarPrefijo(" 7798100 ");

        prefijo.Prefijo.Should().Be("7798100");
        entidad.Prefijos.Should().ContainSingle();
    }

    [Theory]
    [InlineData("779810")]
    [InlineData("2000001")]
    public void AgregarPrefijo_PrefijoInvalido_LanzaDomainException(string prefijo)
    {
        var entidad = Marca.Crear("Mooving");

        var act = () => entidad.AgregarPrefijo(prefijo);

        act.Should().Throw<DomainException>();
        entidad.Prefijos.Should().BeEmpty();
    }

    [Fact]
    public void AgregarPrefijo_Repetido_LanzaDomainException()
    {
        var entidad = Marca.Crear("Mooving");
        entidad.AgregarPrefijo("7798100");

        var act = () => entidad.AgregarPrefijo("7798100");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void QuitarPrefijo_Existente_LoDaDeBaja()
    {
        var entidad = Marca.Crear("Mooving");
        var prefijo = entidad.AgregarPrefijo("7798100");

        entidad.QuitarPrefijo("7798100");

        prefijo.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void MarkAsDeleted_ConAliasYPrefijos_DaDeBajaTambienSusDiccionarios()
    {
        var entidad = Marca.Crear("Mooving");
        var alias = entidad.AgregarAlias("MOOV");
        var prefijo = entidad.AgregarPrefijo("7798100");

        entidad.MarkAsDeleted();

        entidad.IsDeleted.Should().BeTrue();
        alias.IsDeleted.Should().BeTrue();
        prefijo.IsDeleted.Should().BeTrue();
    }
}
