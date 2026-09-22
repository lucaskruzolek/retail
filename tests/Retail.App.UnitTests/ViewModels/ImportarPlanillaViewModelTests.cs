using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ImportarPlanillaViewModelTests
{
    private readonly IProveedorService _proveedorService;
    private readonly ImportarPlanillaViewModel _sut;

    public ImportarPlanillaViewModelTests()
    {
        _proveedorService = Substitute.For<IProveedorService>();
        _sut = new ImportarPlanillaViewModel(
            _proveedorService,
            NullLogger<ImportarPlanillaViewModel>.Instance);
    }

    [Fact]
    public void Inicializar_ConProveedor_ConfiguraValoresPorDefecto()
    {
        // Arrange
        var proveedor = new ProveedorDto
        {
            IdProveedor = 10,
            RazonSocial = "Papelera Mayorista",
            Cuit = "30-10101010-1"
        };

        // Act
        _sut.Inicializar(proveedor);

        // Assert
        _sut.Proveedor.Should().Be(proveedor);
        _sut.ColumnaCodigo.Should().Be("CODIGO");
        _sut.ColumnaCodigoBarras.Should().Be("EAN");
        _sut.ColumnaDescripcion.Should().Be("DESCRIPCION");
        _sut.ColumnaPrecio.Should().Be("PRECIO");
        _sut.FilaInicial.Should().Be(2);
        _sut.HayArchivoSeleccionado.Should().BeFalse();
        _sut.PuedeImportar.Should().BeFalse();
    }

    [Fact]
    public void PuedeImportar_ConArchivoYProveedor_RetornaTrue()
    {
        // Arrange
        var proveedor = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
        _sut.Inicializar(proveedor);

        // Act
        _sut.RutaArchivo = @"C:\dummy\catalogo.xlsx";

        // Assert
        _sut.HayArchivoSeleccionado.Should().BeTrue();
        _sut.PuedeImportar.Should().BeTrue();
    }

    [Fact]
    public async Task ImportarPlanillaCommand_SinArchivo_NoEjecutaServicio()
    {
        // Arrange
        var proveedor = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
        _sut.Inicializar(proveedor);

        // Act
        await _sut.ImportarPlanillaCommand.ExecuteAsync(null);

        // Assert
        await _proveedorService.DidNotReceiveWithAnyArgs().ImportarPlanillaProveedorAsync(
            default!, default!, default, default);
    }
}
