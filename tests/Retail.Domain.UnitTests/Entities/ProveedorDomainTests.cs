using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias para la entidad de dominio Proveedor.
/// </summary>
public class ProveedorDomainTests
{
    [Fact]
    public void Proveedor_Creacion_EstablecePropiedadesEnFormaCanonica()
    {
        // Arrange & Act
        var proveedor = Proveedor.Crear("Librería Mayorista S.A.", "30-12345678-1", telefono: "3794-123456", email: "Ventas@Mayorista.com");

        // Assert
        proveedor.RazonSocial.Should().Be("Librería Mayorista S.A.");
        proveedor.Cuit.Should().Be("30123456781");
        proveedor.Telefono.Should().Be("3794123456");
        proveedor.Email.Should().Be("ventas@mayorista.com");
        proveedor.IsDeleted.Should().BeFalse();
        proveedor.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Proveedor_MarcarComoEliminado_ActualizaEstadoYFecha()
    {
        // Arrange
        var proveedor = Proveedor.Crear("Distribuidora Test", "30-98765432-1");

        // Act
        proveedor.MarkAsDeleted();

        // Assert
        proveedor.IsDeleted.Should().BeTrue();
        proveedor.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void ActualizarDatos_ConParametrosValidos_NormalizaTodosLosValores()
    {
        // Arrange
        var proveedor = Proveedor.Crear("Distribuidora Original", "20-11111111-2");

        // Act
        proveedor.ActualizarDatos("  Distribuidora   Nueva S.R.L.  ", "  20-22222222-3  ", "  011-4567-8900  ", "  info@nueva.com  ");

        // Assert
        proveedor.RazonSocial.Should().Be("Distribuidora Nueva S.R.L.");
        proveedor.Cuit.Should().Be("20222222223");
        proveedor.Telefono.Should().Be("01145678900");
        proveedor.Email.Should().Be("info@nueva.com");
    }

    [Fact]
    public void Crear_MismoCuitConYSinGuiones_GuardaElMismoValor()
    {
        // Regresión de P-1: la misma clave escrita de dos formas debe producir el mismo valor persistido, que es
        // lo que permite al índice único detectar el duplicado.
        var conGuiones = Proveedor.Crear("Distribuidora Sur", "20-12345678-6");
        var sinGuiones = Proveedor.Crear("Distribuidora Sur", "20123456786");

        conGuiones.Cuit.Should().Be(sinGuiones.Cuit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("@@@")]
    public void ActualizarDatos_ConRazonSocialInvalida_LanzaDomainException(string? razonSocialInvalida)
    {
        // Arrange
        var proveedor = Proveedor.Crear("Distribuidora Valida", "20-11111111-2");

        // Act
        var act = () => proveedor.ActualizarDatos(razonSocialInvalida!, "20-22222222-3", null, null);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*razón social del proveedor*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("20-22222222-0")]
    [InlineData("11111111111")]
    public void ActualizarDatos_ConCuitInvalido_LanzaDomainException(string? cuitInvalido)
    {
        // Arrange
        var proveedor = Proveedor.Crear("Distribuidora Valida", "20-11111111-2");

        // Act
        var act = () => proveedor.ActualizarDatos("Distribuidora Modificada", cuitInvalido!, null, null);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*CUIT*");
    }

    [Fact]
    public void ActualizarDatos_EmailInvalido_NoModificaNingunDato()
    {
        // Arrange
        var proveedor = Proveedor.Crear("Distribuidora Valida", "20-11111111-2");

        // Act
        var act = () => proveedor.ActualizarDatos("Otra Razón Social", "20-22222222-3", null, "a@b");

        // Assert: el agregado valida todo antes de asignar.
        act.Should().Throw<DomainException>();
        proveedor.RazonSocial.Should().Be("Distribuidora Valida");
        proveedor.Cuit.Should().Be("20111111112");
    }
}
