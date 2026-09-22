using FluentAssertions;
using Retail.Domain.Entities;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

/// <summary>
/// Pruebas unitarias para la entidad interna de catálogo de proveedor.
/// </summary>
public class CatalogoProveedorDomainTests
{
    [Fact]
    public void ActualizarPrecio_ConPrecioValido_ActualizaCostoYFechaActualizacionUtc()
    {
        // Arrange
        var fechaInicial = DateTime.UtcNow.AddDays(-5);
        var item = new CatalogoProveedor
        {
            IdProveedor = 1,
            CodigoProveedor = "COD-001",
            DescripcionProveedor = "Cuaderno Rivadavia ABC",
            CostoReposicion = 2500m,
            FechaActualizacion = fechaInicial
        };

        var fechaAntes = DateTime.UtcNow.AddSeconds(-1);

        // Act
        item.ActualizarPrecio(3200.50m, "Cuaderno Rivadavia Tapa Dura", "7791234567890");

        // Assert
        item.CostoReposicion.Should().Be(3200.50m);
        item.DescripcionProveedor.Should().Be("Cuaderno Rivadavia Tapa Dura");
        item.CodigoBarras.Should().Be("7791234567890");
        item.FechaActualizacion.Should().BeOnOrAfter(fechaAntes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-0.01)]
    public void ActualizarPrecio_ConPrecioMenorOIgualACero_LanzaArgumentOutOfRangeException(decimal precioInvalido)
    {
        // Arrange
        var item = new CatalogoProveedor
        {
            IdProveedor = 1,
            CodigoProveedor = "COD-001",
            DescripcionProveedor = "Resma A4",
            CostoReposicion = 4500m
        };

        // Act
        var act = () => item.ActualizarPrecio(precioInvalido);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*nuevoCosto*");
    }

    [Fact]
    public void ActualizarPrecio_ConDescripcionNulaOEnBlanco_ConservaDescripcionOriginal()
    {
        // Arrange
        var item = new CatalogoProveedor
        {
            IdProveedor = 1,
            CodigoProveedor = "COD-001",
            DescripcionProveedor = "Lapicera BIC Azul",
            CostoReposicion = 100m,
            CodigoBarras = "123456"
        };

        // Act
        item.ActualizarPrecio(150m, "   ", null);

        // Assert
        item.CostoReposicion.Should().Be(150m);
        item.DescripcionProveedor.Should().Be("Lapicera BIC Azul");
        item.CodigoBarras.Should().Be("123456");
    }
}
