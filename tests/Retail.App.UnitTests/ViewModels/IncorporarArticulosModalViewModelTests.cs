using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class IncorporarArticulosModalViewModelTests : IDisposable
{
    private readonly IncorporarArticulosModalViewModel _sut = new(
        Substitute.For<IClasificacionService>(),
        Substitute.For<ITerminoClasificacionService>(),
        Substitute.For<IEquivalenciaClasificacionService>(),
        Substitute.For<IRubroService>(),
        Substitute.For<ICurrentUserSession>(),
        NullLogger<IncorporarArticulosModalViewModel>.Instance);

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

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
        _sut.SugerirClasificacion = false;
        _sut.CategoriaDelLote = new OpcionDiccionario(3, "Escolar");
        _sut.MarcaDelLote = new OpcionDiccionario(7, "Bic");

        // Act
        var dto = _sut.ObtenerDto();

        // Assert
        // La categoría y la marca del modal se aplican a cada ítem, sin sugerencia (Track 6.6, R-7).
        dto.Should().NotBeNull();
        dto.Items.Should().ContainSingle();
        dto.Items[0].Should().BeEquivalentTo(new ItemIncorporacionArticuloDto
        {
            IdCatalogo = 5,
            PorcentajeGanancia = 30m,
            IdCategoria = 3,
            IdMarca = 7
        });
    }
}
