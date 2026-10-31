using FluentAssertions;
using NSubstitute;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class VincularArticuloModalViewModelTests
{
    private static CatalogoProveedorDto CrearItem(string? codigoBarras)
    {
        return new CatalogoProveedorDto
        {
            Id = 1,
            IdProveedor = 3,
            CodigoProveedor = "CP-01",
            CodigoBarras = codigoBarras,
            DescripcionProveedor = "Resma A4",
            CostoReposicion = 7200m
        };
    }

    [Fact]
    public async Task InicializarAsync_ItemConCodigoDeBarras_DejaElBuscadorVacioYNoConsulta()
    {
        // Arrange
        var inventario = Substitute.For<IInventarioService>();
        var viewModel = new VincularArticuloModalViewModel(inventario);

        // Act
        await viewModel.InicializarAsync(CrearItem("7790001000011"));

        // Assert
        viewModel.TextoBusqueda.Should().BeEmpty();
        viewModel.ArticulosEncontrados.Should().BeEmpty();
        await inventario.DidNotReceiveWithAnyArgs().BuscarArticulosParaVentaAsync(default!, default);
    }

    [Fact]
    public async Task InicializarAsync_ItemConCodigoDeBarras_ExponeElCodigoComoDato()
    {
        // Arrange
        var viewModel = new VincularArticuloModalViewModel(Substitute.For<IInventarioService>());

        // Act
        await viewModel.InicializarAsync(CrearItem("7790001000011"));

        // Assert
        viewModel.CodigoBarrasItem.Should().Be("7790001000011");
    }

    [Fact]
    public void CodigoBarrasItem_SinItem_EsNulo()
    {
        // Arrange & Act
        var viewModel = new VincularArticuloModalViewModel(Substitute.For<IInventarioService>());

        // Assert
        viewModel.CodigoBarrasItem.Should().BeNull();
    }
}
