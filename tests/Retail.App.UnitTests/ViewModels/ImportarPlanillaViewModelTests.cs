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

    private static ResultadoImportacionDto CrearResultado(int filasConError)
    {
        return new ResultadoImportacionDto
        {
            TotalFilasProcesadas = 10,
            NuevosRegistros = 7,
            PreciosActualizados = 3,
            FilasConError = filasConError,
            ErroresDetalle = filasConError > 0 ? new List<string> { "Fila 5: precio inválido." } : new List<string>(),
            TiempoTranscurrido = TimeSpan.FromSeconds(2.5)
        };
    }

    private async Task ImportarConResultadoAsync(ResultadoImportacionDto resultado, string ruta)
    {
        _sut.Inicializar(new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" });
        _sut.RutaArchivo = ruta;

        _proveedorService.ImportarPlanillaProveedorAsync(
                Arg.Any<Stream>(), Arg.Any<MapeoColumnasDto>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(resultado);

        await _sut.ImportarPlanillaCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task ImportarPlanillaCommand_ConExito_MarcaCompletadaYBloqueaReimportacion()
    {
        var ruta = Path.GetTempFileName();
        try
        {
            await ImportarConResultadoAsync(CrearResultado(0), ruta);

            _sut.ImportacionCompletada.Should().BeTrue();
            _sut.ImportacionConObservaciones.Should().BeFalse();
            _sut.PuedeImportar.Should().BeFalse();
            _sut.TextoBotonCerrar.Should().Be("Cerrar y ver catálogo");
            _sut.DialogResult.Should().BeTrue();
            _sut.MensajeExito.Should().Contain("7 artículos nuevos").And.Contain("3 precios");
            _sut.MensajeExito.Should().NotContain("omitieron");
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task ImportarPlanillaCommand_ConFilasConError_MarcaCompletadaConObservaciones()
    {
        var ruta = Path.GetTempFileName();
        try
        {
            await ImportarConResultadoAsync(CrearResultado(2), ruta);

            _sut.ImportacionCompletada.Should().BeTrue();
            _sut.ImportacionConObservaciones.Should().BeTrue();
            _sut.MensajeExito.Should().Contain("Se omitieron 2 filas con error");
            _sut.TieneErroresDetalle.Should().BeTrue();
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task ImportarPlanillaCommand_ConErrorDelServicio_NoMarcaCompletada()
    {
        var ruta = Path.GetTempFileName();
        try
        {
            _sut.Inicializar(new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" });
            _sut.RutaArchivo = ruta;
            _proveedorService.ImportarPlanillaProveedorAsync(
                    Arg.Any<Stream>(), Arg.Any<MapeoColumnasDto>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
                .Returns<ResultadoImportacionDto>(_ => throw new InvalidOperationException("falla"));

            await _sut.ImportarPlanillaCommand.ExecuteAsync(null);

            _sut.ImportacionCompletada.Should().BeFalse();
            _sut.MensajeExito.Should().BeNull();
            _sut.PuedeImportar.Should().BeTrue();
            _sut.TextoBotonCerrar.Should().Be("Cerrar");
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task ImportarPlanillaCommand_ConCancelacion_NoMarcaCompletada()
    {
        var ruta = Path.GetTempFileName();
        try
        {
            _sut.Inicializar(new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" });
            _sut.RutaArchivo = ruta;
            _proveedorService.ImportarPlanillaProveedorAsync(
                    Arg.Any<Stream>(), Arg.Any<MapeoColumnasDto>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
                .Returns<ResultadoImportacionDto>(_ => throw new OperationCanceledException());

            await _sut.ImportarPlanillaCommand.ExecuteAsync(null);

            _sut.ImportacionCompletada.Should().BeFalse();
            _sut.MensajeEstado.Should().Be("Importación cancelada.");
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task RutaArchivo_CambiaTrasExito_ReseteaElResultadoYPermiteReimportar()
    {
        var ruta = Path.GetTempFileName();
        try
        {
            await ImportarConResultadoAsync(CrearResultado(1), ruta);

            _sut.RutaArchivo = @"C:\dummy\otro.xlsx";

            _sut.ImportacionCompletada.Should().BeFalse();
            _sut.UltimoResultado.Should().BeNull();
            _sut.MensajeExito.Should().BeNull();
            _sut.TieneResultado.Should().BeFalse();
            _sut.PuedeImportar.Should().BeTrue();
        }
        finally
        {
            File.Delete(ruta);
        }
    }
}
