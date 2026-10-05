using System.IO;
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
        _sut.FilaEncabezado.Should().Be(1);
        _sut.TieneErroresDetalle.Should().BeFalse();
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
    public void PuedeImportar_ConFilaEncabezadoMenorAUno_RetornaFalse()
    {
        // Arrange
        var proveedor = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
        _sut.Inicializar(proveedor);
        _sut.RutaArchivo = @"C:\dummy\catalogo.xlsx";

        // Act
        _sut.FilaEncabezado = 0;

        // Assert
        _sut.PuedeImportar.Should().BeFalse();
    }

    [Fact]
    public async Task ImportarPlanillaCommand_ConFilasConError_ExponeElDetalleYTransmiteFilaEncabezado()
    {
        // Arrange
        var rutaTemporal = Path.GetTempFileName();
        try
        {
            var proveedor = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
            _sut.Inicializar(proveedor);
            _sut.RutaArchivo = rutaTemporal;
            _sut.FilaEncabezado = 3;

            _proveedorService.ImportarPlanillaProveedorAsync(
                    Arg.Any<Stream>(), Arg.Any<MapeoColumnasDto>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
                .Returns(new ResultadoImportacionDto
                {
                    TotalFilasProcesadas = 1,
                    PreciosActualizados = 0,
                    NuevosRegistros = 1,
                    FilasConError = 1,
                    ErroresDetalle = new List<string> { "Fila 5: el precio 'abc' no es un importe válido." },
                    TiempoTranscurrido = TimeSpan.FromSeconds(1)
                });

            // Act
            await _sut.ImportarPlanillaCommand.ExecuteAsync(null);

            // Assert
            _sut.TieneErroresDetalle.Should().BeTrue();
            _sut.UltimoResultado!.ErroresDetalle.Should().ContainSingle().Which.Should().StartWith("Fila 5:");
            await _proveedorService.Received(1).ImportarPlanillaProveedorAsync(
                Arg.Any<Stream>(),
                Arg.Is<MapeoColumnasDto>(m => m.FilaEncabezado == 3),
                Arg.Any<IProgress<int>?>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(rutaTemporal);
        }
    }

    [Fact]
    public async Task ImportarPlanillaCommand_ConPlanillaIncompatible_MuestraElMensajeDeLaExcepcion()
    {
        // Arrange
        var rutaTemporal = Path.GetTempFileName();
        try
        {
            var proveedor = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
            _sut.Inicializar(proveedor);
            _sut.RutaArchivo = rutaTemporal;

            _proveedorService.ImportarPlanillaProveedorAsync(
                    Arg.Any<Stream>(), Arg.Any<MapeoColumnasDto>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
                .Returns<ResultadoImportacionDto>(_ => throw new InvalidDataException("No se encontró la columna 'PRECIO'."));

            // Act
            await _sut.ImportarPlanillaCommand.ExecuteAsync(null);

            // Assert
            _sut.MensajeError.Should().Be("No se encontró la columna 'PRECIO'.");
            _sut.TieneResultado.Should().BeFalse();
        }
        finally
        {
            File.Delete(rutaTemporal);
        }
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
