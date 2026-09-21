using Retail.Domain.Entities;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias para la entidad de dominio Proveedor.
/// Verifica la correcta inicialización de propiedades y las reglas de borrado lógico.
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
        Assert.Equal("Librería Mayorista S.A.", proveedor.RazonSocial);
        Assert.Equal("30-12345678-9", proveedor.Cuit);
        Assert.Equal("3794123456", proveedor.Telefono);
        Assert.Equal("ventas@mayorista.com", proveedor.Email);
        Assert.False(proveedor.IsDeleted);
        Assert.Null(proveedor.DeletedAt);
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
        Assert.True(proveedor.IsDeleted);
        Assert.NotNull(proveedor.DeletedAt);
    }
}