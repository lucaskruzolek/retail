using FluentAssertions;
using Retail.Application.DTOs.Reportes;
using Retail.Infrastructure.Reportes;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Reportes;

public class ExportadorReportesTests
{
    private readonly ExportadorReportes _exportador = new();

    [Fact]
    public async Task ExportarAsync_FormatoInvalido_LanzaArgumentOutOfRange()
    {
        using var flujo = new MemoryStream();

        var accion = () => _exportador.ExportarAsync(DocumentoReportePrueba.Completo(), (FormatoExportacionEnum)99, flujo);

        await accion.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ExportarAsync_DocumentoNull_LanzaArgumentNull()
    {
        using var flujo = new MemoryStream();

        var accion = () => _exportador.ExportarAsync(null!, FormatoExportacionEnum.Excel, flujo);

        await accion.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportarAsync_DestinoNull_LanzaArgumentNull()
    {
        var accion = () => _exportador.ExportarAsync(DocumentoReportePrueba.Completo(), FormatoExportacionEnum.Excel, null!);

        await accion.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportarAsync_StreamDeSoloLectura_LanzaArgumentException()
    {
        using var flujo = new MemoryStream([1, 2, 3], writable: false);

        var accion = () => _exportador.ExportarAsync(DocumentoReportePrueba.Completo(), FormatoExportacionEnum.Pdf, flujo);

        await accion.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(FormatoExportacionEnum.Excel)]
    [InlineData(FormatoExportacionEnum.Pdf)]
    public async Task ExportarAsync_FormatoValido_EscribeYNoCierraElStream(FormatoExportacionEnum formato)
    {
        using var flujo = new MemoryStream();

        await _exportador.ExportarAsync(DocumentoReportePrueba.Completo(), formato, flujo);

        flujo.CanWrite.Should().BeTrue();
        flujo.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportarAsync_CancelacionPrevia_LanzaOperationCanceled()
    {
        using var flujo = new MemoryStream();
        using var cancelacion = new CancellationTokenSource();
        await cancelacion.CancelAsync();

        var accion = () => _exportador.ExportarAsync(DocumentoReportePrueba.Completo(), FormatoExportacionEnum.Excel, flujo, cancelacion.Token);

        await accion.Should().ThrowAsync<OperationCanceledException>();
        flujo.Length.Should().Be(0);
    }

    [Fact]
    public async Task ExportarAsync_ConVariableDeEntorno_GuardaEjemplosParaInspeccion()
    {
        if (Environment.GetEnvironmentVariable("RETAIL_GUARDAR_EJEMPLOS") != "1")
        {
            return;
        }

        var carpeta = Path.Combine(Path.GetTempPath(), "retail-fase4");
        Directory.CreateDirectory(carpeta);

        await using (var excel = File.Create(Path.Combine(carpeta, "ejemplo.xlsx")))
        {
            await _exportador.ExportarAsync(DocumentoReportePrueba.Completo(), FormatoExportacionEnum.Excel, excel);
        }

        await using (var pdf = File.Create(Path.Combine(carpeta, "ejemplo.pdf")))
        {
            await _exportador.ExportarAsync(DocumentoReportePrueba.Completo(), FormatoExportacionEnum.Pdf, pdf);
        }

        await using (var pdfLargo = File.Create(Path.Combine(carpeta, "ejemplo-largo.pdf")))
        {
            await _exportador.ExportarAsync(DocumentoReportePrueba.ConFilas(200), FormatoExportacionEnum.Pdf, pdfLargo);
        }
    }
}
