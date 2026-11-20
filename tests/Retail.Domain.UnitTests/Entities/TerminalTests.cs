using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias de la raíz de agregado Terminal (puesto de trabajo del local).
/// </summary>
public class TerminalTests
{
    [Fact]
    public void Crear_DatosValidos_NormalizaElNombreYGuardaElEquipo()
    {
        // Act
        var terminal = Terminal.Crear("  Mostrador   1 ", " CAJA-01 ");

        // Assert
        terminal.Nombre.Should().Be("Mostrador 1");
        terminal.NombreEquipo.Should().Be("CAJA-01");
        terminal.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_NombreVacio_LanzaDomainException(string nombre)
    {
        var accion = () => Terminal.Crear(nombre, "CAJA-01");

        accion.Should().Throw<DomainException>().WithMessage("*nombre de la terminal*");
    }

    [Fact]
    public void Crear_NombreMasLargoQueElMaximo_LanzaDomainException()
    {
        var accion = () => Terminal.Crear(new string('A', Terminal.LongitudMaximaNombre + 1), "CAJA-01");

        accion.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Crear_EquipoVacio_LanzaDomainException(string equipo)
    {
        var accion = () => Terminal.Crear("Mostrador 1", equipo);

        accion.Should().Throw<DomainException>().WithMessage("*nombre del equipo*");
    }

    [Fact]
    public void Crear_EquipoMasLargoQueElMaximo_LanzaDomainException()
    {
        var accion = () => Terminal.Crear("Mostrador 1", new string('E', Terminal.LongitudMaximaNombreEquipo + 1));

        accion.Should().Throw<DomainException>();
    }

    [Fact]
    public void Renombrar_NombreValido_CambiaSoloElNombre()
    {
        // Arrange
        var terminal = Terminal.Crear("Mostrador 1", "CAJA-01");

        // Act
        terminal.Renombrar(" Caja  principal ");

        // Assert
        terminal.Nombre.Should().Be("Caja principal");
        terminal.NombreEquipo.Should().Be("CAJA-01");
    }

    [Fact]
    public void ReemplazarEquipo_EquipoNuevo_ConservaElNombreDeLaTerminal()
    {
        // Arrange
        var terminal = Terminal.Crear("Mostrador 1", "CAJA-01");

        // Act
        terminal.ReemplazarEquipo("CAJA-02");

        // Assert
        terminal.NombreEquipo.Should().Be("CAJA-02");
        terminal.Nombre.Should().Be("Mostrador 1");
    }

    [Fact]
    public void MarkAsDeleted_TerminalActiva_LaDaDeBajaLogicamente()
    {
        // Arrange
        var terminal = Terminal.Crear("Mostrador 1", "CAJA-01");

        // Act
        terminal.MarkAsDeleted();

        // Assert
        terminal.IsDeleted.Should().BeTrue();
    }
}
