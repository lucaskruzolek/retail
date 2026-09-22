using FluentAssertions;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class IncorporarArticulosModalViewModelTests
{
    private readonly IncorporarArticulosModalViewModel _sut = new();

    [Fact]
    public void Inicializar_ConArticulos_CreaItemsConMargenGlobalYCalculaPrecioFinal()
    {
        // Arrange
        var categorias = new List<CategoriaDto> { new() { IdCategoria = 1, NombreCategoria = "Librería" } };
        var marcas = new List<MarcaDto> { new() { IdMarca = 1, NombreMarca = "Bic" } };
        var items = new List<CatalogoProveedorDto>
        {
            new() { Id = 10, IdProveedor = 1, CodigoProveedor = "BIC-01", DescripcionProveedor = "Bolígrafo Azul", CostoReposicion = 1000m },
            new() { Id = 20, IdProveedor = 1, CodigoProveedor = "BIC-02", DescripcionProveedor = "Bolígrafo Negro", CostoReposicion = 2000m }
        };

        _sut.PorcentajeGananciaGlobal = 40m;

        // Act
        _sut.Inicializar(categorias, marcas, items);

        // Assert
        _sut.TotalArticulos.Should().Be(2);
        _sut.Items.Should().HaveCount(2);

        _sut.Items[0].IdCatalogo.Should().Be(10);
        _sut.Items[0].PorcentajeGanancia.Should().Be(40m);
        _sut.Items[0].PrecioVenta.Should().Be(1400m); // 1000 * 1.40 = 1400

        _sut.Items[1].IdCatalogo.Should().Be(20);
        _sut.Items[1].PorcentajeGanancia.Should().Be(40m);
        _sut.Items[1].PrecioVenta.Should().Be(2800m); // 2000 * 1.40 = 2800
    }

    [Fact]
    public void AplicarGananciaGlobal_ActualizaMargenYPrecioEnTodosLosItems()
    {
        // Arrange
        var items = new List<CatalogoProveedorDto>
        {
            new() { Id = 1, IdProveedor = 1, CodigoProveedor = "A1", DescripcionProveedor = "Item 1", CostoReposicion = 500m },
            new() { Id = 2, IdProveedor = 1, CodigoProveedor = "A2", DescripcionProveedor = "Item 2", CostoReposicion = 1000m }
        };
        _sut.Inicializar(Array.Empty<CategoriaDto>(), Array.Empty<MarcaDto>(), items);

        // Act
        _sut.PorcentajeGananciaGlobal = 50m;
        _sut.AplicarGananciaGlobalCommand.Execute(null);

        // Assert
        _sut.Items[0].PorcentajeGanancia.Should().Be(50m);
        _sut.Items[0].PrecioVenta.Should().Be(750m); // 500 * 1.50 = 750

        _sut.Items[1].PorcentajeGanancia.Should().Be(50m);
        _sut.Items[1].PrecioVenta.Should().Be(1500m); // 1000 * 1.50 = 1500
    }

    [Fact]
    public void EditarMargenIndividual_ActualizaUnicamentePrecioDeEseItem()
    {
        // Arrange
        var items = new List<CatalogoProveedorDto>
        {
            new() { Id = 1, IdProveedor = 1, CodigoProveedor = "A1", DescripcionProveedor = "Item 1", CostoReposicion = 1000m },
            new() { Id = 2, IdProveedor = 1, CodigoProveedor = "A2", DescripcionProveedor = "Item 2", CostoReposicion = 2000m }
        };
        _sut.PorcentajeGananciaGlobal = 40m;
        _sut.Inicializar(Array.Empty<CategoriaDto>(), Array.Empty<MarcaDto>(), items);

        // Act
        _sut.Items[0].PorcentajeGanancia = 25m;

        // Assert
        _sut.Items[0].PrecioVenta.Should().Be(1250m); // 1000 * 1.25 = 1250
        _sut.Items[1].PorcentajeGanancia.Should().Be(40m);
        _sut.Items[1].PrecioVenta.Should().Be(2800m); // Inalterado
    }

    [Fact]
    public void ObtenerDto_ConstruyeDtoConListaDeItemsIndividuales()
    {
        // Arrange
        var items = new List<CatalogoProveedorDto>
        {
            new() { Id = 5, IdProveedor = 1, CodigoProveedor = "C1", DescripcionProveedor = "Cuaderno", CostoReposicion = 800m }
        };
        _sut.Inicializar(Array.Empty<CategoriaDto>(), Array.Empty<MarcaDto>(), items);
        _sut.Items[0].PorcentajeGanancia = 30m;
        _sut.IdCategoriaDestino = 3;
        _sut.IdMarcaDestino = 7;

        // Act
        var dto = _sut.ObtenerDto();

        // Assert
        dto.Should().NotBeNull();
        dto.IdCategoria.Should().Be(3);
        dto.IdMarca.Should().Be(7);
        dto.Items.Should().HaveCount(1);
        dto.Items[0].IdCatalogo.Should().Be(5);
        dto.Items[0].PorcentajeGanancia.Should().Be(30m);
    }
}
