using FluentAssertions;
using Retail.Domain.Entities;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias para la entidad de dominio Proveedor.
/// </summary>
public class ProveedorDomainTests
{
    [Fact]
    public void Proveedor_Creacion_EstablecePropiedadesCorrectamente()
    {
        // Arrange & Act
        var proveedor = new Proveedor
        {
            RazonSocial = "Librería Mayorista S.A.",
            Cuit = "30-12345678-9",
            Telefono = "3794123456",
            Email = "ventas@mayorista.com"
        };

        // Assert
        proveedor.RazonSocial.Should().Be("Librería Mayorista S.A.");
        proveedor.Cuit.Should().Be("30-12345678-9");
        proveedor.Telefono.Should().Be("3794123456");
        proveedor.Email.Should().Be("ventas@mayorista.com");
        proveedor.IsDeleted.Should().BeFalse();
        proveedor.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Proveedor_MarcarComoEliminado_ActualizaEstadoYFecha()
    {
        // Arrange
        var proveedor = new Proveedor
        {
            RazonSocial = "Distribuidora Test",
            Cuit = "30-98765432-1"
        };

        // Act
        proveedor.MarkAsDeleted();

        // Assert
        proveedor.IsDeleted.Should().BeTrue();
        proveedor.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void ActualizarDatos_ConParametrosValidos_ActualizaPropiedadesYRecortaEspacios()
    {
        // Arrange
        var proveedor = new Proveedor
        {
            RazonSocial = "Distribuidora Original",
            Cuit = "20-11111111-2"
        };

        // Act
        proveedor.ActualizarDatos("  Distribuidora Nueva S.R.L.  ", "  20-22222222-3  ", "  011-4567-8900  ", "  info@nueva.com  ");

        // Assert
        proveedor.RazonSocial.Should().Be("Distribuidora Nueva S.R.L.");
        proveedor.Cuit.Should().Be("20-22222222-3");
        proveedor.Telefono.Should().Be("011-4567-8900");
        proveedor.Email.Should().Be("info@nueva.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ActualizarDatos_ConRazonSocialInvalida_LanzaArgumentException(string? razonSocialInvalida)
    {
        // Arrange
        var proveedor = new Proveedor
        {
            RazonSocial = "Distribuidora Valida",
            Cuit = "20-11111111-2"
        };

        // Act
        var act = () => proveedor.ActualizarDatos(razonSocialInvalida!, "20-22222222-3", null, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("razonSocial");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ActualizarDatos_ConCuitInvalido_LanzaArgumentException(string? cuitInvalido)
    {
        // Arrange
        var proveedor = new Proveedor
        {
            RazonSocial = "Distribuidora Valida",
            Cuit = "20-11111111-2"
        };

        // Act
        var act = () => proveedor.ActualizarDatos("Distribuidora Modificada", cuitInvalido!, null, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cuit");
    }
}
