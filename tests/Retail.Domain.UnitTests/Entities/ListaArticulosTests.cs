using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class ListaArticulosTests
{
    private const int IdUsuario = 4;

    private static ListaArticulos ListaConDosRenglones()
    {
        return ListaArticulos.Crear("Lista", IdUsuario, new[] { (10, 2), (11, 3) });
    }

    [Fact]
    public void Crear_DatosValidos_NormalizaNombreYCargaRenglones()
    {
        // Act
        var lista = ListaArticulos.Crear("  3.º grado   Colegio N ", IdUsuario, new[] { (10, 2), (11, 1) });

        // Assert
        lista.Nombre.Should().Be("3.º grado Colegio N");
        lista.IdUsuarioCreador.Should().Be(IdUsuario);
        lista.FechaModificacion.Should().Be(lista.CreatedAt);
        lista.Renglones.Select(r => (r.IdArticulo, r.Cantidad)).Should().Equal((10, 2), (11, 1));
    }

    [Fact]
    public void Crear_ArticulosRepetidos_AgrupaYSumaCantidades()
    {
        // Act
        var lista = ListaArticulos.Crear("Lista", IdUsuario, new[] { (10, 2), (11, 1), (10, 3) });

        // Assert
        lista.Renglones.Select(r => (r.IdArticulo, r.Cantidad)).Should().Equal((10, 5), (11, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_NombreVacio_LanzaDomainException(string nombre)
    {
        // Act
        var act = () => ListaArticulos.Crear(nombre, IdUsuario, new[] { (10, 1) });

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Crear_NombreDe101Caracteres_LanzaDomainException()
    {
        // Act
        var act = () => ListaArticulos.Crear(new string('a', 101), IdUsuario, new[] { (10, 1) });

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*100*");
    }

    [Fact]
    public void Crear_NombreDe100Caracteres_LoAcepta()
    {
        // Act
        var lista = ListaArticulos.Crear(new string('a', 100), IdUsuario, new[] { (10, 1) });

        // Assert
        lista.Nombre.Should().HaveLength(100);
    }

    [Fact]
    public void Crear_UsuarioNoPositivo_LanzaArgumentOutOfRangeException()
    {
        // Act
        var act = () => ListaArticulos.Crear("Lista", 0, new[] { (10, 1) });

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Crear_SinRenglones_LanzaDomainException()
    {
        // Act
        var act = () => ListaArticulos.Crear("Lista", IdUsuario, Array.Empty<(int, int)>());

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_CantidadMenorAUno_LanzaDomainException(int cantidad)
    {
        // Act
        var act = () => ListaArticulos.Crear("Lista", IdUsuario, new[] { (10, cantidad) });

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Crear_ArticuloNoPositivo_LanzaArgumentOutOfRangeException()
    {
        // Act
        var act = () => ListaArticulos.Crear("Lista", IdUsuario, new[] { (0, 1) });

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Renombrar_NombreValido_ActualizaNombreYFechaModificacion()
    {
        // Arrange
        var lista = ListaConDosRenglones();
        var antes = lista.FechaModificacion;

        // Act
        lista.Renombrar("  Nuevo   nombre ");

        // Assert
        lista.Nombre.Should().Be("Nuevo nombre");
        lista.FechaModificacion.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public void Renombrar_NombreVacio_LanzaDomainException()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        var act = () => lista.Renombrar("  ");

        // Assert
        act.Should().Throw<DomainException>();
        lista.Nombre.Should().Be("Lista");
    }

    [Fact]
    public void AgregarRenglon_ArticuloNuevo_LoAgrega()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        lista.AgregarRenglon(12, 4);

        // Assert
        lista.Renglones.Should().HaveCount(3);
        lista.Renglones.Single(r => r.IdArticulo == 12).Cantidad.Should().Be(4);
    }

    [Fact]
    public void AgregarRenglon_ArticuloExistente_SumaCantidad()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        lista.AgregarRenglon(10, 5);

        // Assert
        lista.Renglones.Should().HaveCount(2);
        lista.Renglones.Single(r => r.IdArticulo == 10).Cantidad.Should().Be(7);
    }

    [Fact]
    public void CambiarCantidad_RenglonExistente_ActualizaCantidad()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        lista.CambiarCantidad(11, 9);

        // Assert
        lista.Renglones.Single(r => r.IdArticulo == 11).Cantidad.Should().Be(9);
    }

    [Fact]
    public void CambiarCantidad_RenglonInexistente_LanzaDomainException()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        var act = () => lista.CambiarCantidad(99, 1);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*99*");
    }

    [Fact]
    public void QuitarRenglon_ConDosRenglones_LoQuita()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        lista.QuitarRenglon(10);

        // Assert
        lista.Renglones.Select(r => r.IdArticulo).Should().Equal(11);
    }

    [Fact]
    public void QuitarRenglon_UnicoRenglon_LanzaDomainException()
    {
        // Arrange
        var lista = ListaArticulos.Crear("Lista", IdUsuario, new[] { (10, 1) });

        // Act
        var act = () => lista.QuitarRenglon(10);

        // Assert
        act.Should().Throw<DomainException>();
        lista.Renglones.Should().HaveCount(1);
    }

    [Fact]
    public void QuitarRenglon_RenglonInexistente_LanzaDomainException()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        var act = () => lista.QuitarRenglon(99);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ReemplazarRenglones_ContenidoDistinto_ConciliaConservandoIdentidad()
    {
        // Arrange
        var lista = ListaConDosRenglones();
        var renglonConservado = lista.Renglones.Single(r => r.IdArticulo == 10);

        // Act
        lista.ReemplazarRenglones(new[] { (10, 8), (12, 1) });

        // Assert
        lista.Renglones.Select(r => (r.IdArticulo, r.Cantidad)).Should().BeEquivalentTo(new[] { (10, 8), (12, 1) });
        lista.Renglones.Single(r => r.IdArticulo == 10).Should().BeSameAs(renglonConservado);
    }

    [Fact]
    public void ReemplazarRenglones_ArticulosRepetidos_AgrupaYSuma()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        lista.ReemplazarRenglones(new[] { (12, 1), (12, 2) });

        // Assert
        lista.Renglones.Select(r => (r.IdArticulo, r.Cantidad)).Should().Equal((12, 3));
    }

    [Fact]
    public void ReemplazarRenglones_SinRenglones_LanzaDomainExceptionYNoCambiaLaLista()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        var act = () => lista.ReemplazarRenglones(Array.Empty<(int, int)>());

        // Assert
        act.Should().Throw<DomainException>();
        lista.Renglones.Select(r => (r.IdArticulo, r.Cantidad)).Should().Equal((10, 2), (11, 3));
    }

    [Fact]
    public void ReemplazarRenglones_UnRenglonInvalido_NoCambiaLaLista()
    {
        // Arrange
        var lista = ListaConDosRenglones();

        // Act
        var act = () => lista.ReemplazarRenglones(new[] { (10, 5), (12, 0) });

        // Assert
        act.Should().Throw<DomainException>();
        lista.Renglones.Select(r => (r.IdArticulo, r.Cantidad)).Should().Equal((10, 2), (11, 3));
    }
}
