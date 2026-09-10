using System.Globalization;
using System.IO;
using System.Windows;
using FluentAssertions;
using Retail.App.Views.Dialogs;
using Serilog;
using Xunit;

namespace Retail.App.UnitTests;

public class GlobalExceptionHandlingTests
{
    [Fact]
    public void Serilog_ConfiguracionArchivoRotativo_DebeEscribirLogEnDirectorio()
    {
        // Arrange
        var testLogDir = Path.Combine(Path.GetTempPath(), "RetailTestsLogs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testLogDir);
        var logFilePattern = Path.Combine(testLogDir, "retail-.log");

        try
        {
            using (var logger = new LoggerConfiguration()
                       .MinimumLevel.Information()
                       .WriteTo.File(
                           path: logFilePattern,
                           rollingInterval: RollingInterval.Day,
                           formatProvider: CultureInfo.InvariantCulture,
                           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                       .CreateLogger())
            {
                // Act
                logger.Information("Prueba unitaria de registro informativo");
                logger.Fatal(new InvalidOperationException("Falla crítica simulada"), "Prueba unitaria de excepción fatal");
            }

            // Assert
            var logFiles = Directory.GetFiles(testLogDir, "retail-*.log");
            logFiles.Should().NotBeEmpty("Serilog debe haber generado al menos un archivo con el patrón de fecha rotativo");

            var logContent = File.ReadAllText(logFiles[0]);
            logContent.Should().Contain("Prueba unitaria de registro informativo");
            logContent.Should().Contain("Prueba unitaria de excepción fatal");
            logContent.Should().Contain("InvalidOperationException");
            logContent.Should().Contain("Falla crítica simulada");
        }
        finally
        {
            if (Directory.Exists(testLogDir))
            {
                try
                {
                    Directory.Delete(testLogDir, recursive: true);
                }
                catch (IOException)
                {
                    // Limpieza preventiva si el archivo aún se encuentra en desbloqueo
                }
            }
        }
    }

    [Fact]
    public void UnhandledExceptionDialog_InstanciacionEnHiloSTA_DebeCargarDatosCorrectamente()
    {
        // Arrange
        var staThread = new System.Threading.Thread(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                try
                {
                    _ = new System.Windows.Application();
                }
                catch (InvalidOperationException)
                {
                    // Ignorar si ya fue instanciada en el AppDomain
                }
            }

            var dummyException = new InvalidOperationException("Falla simulada para verificación de diálogo");
            const string logFileSimulado = @"C:\Retail\logs\retail-20260909.log";

            // Act
            var dialog = new UnhandledExceptionDialog(dummyException, isFatal: false, logFileSimulado);

            // Assert
            dialog.Title.Should().Be("Incidencia en el Sistema - Retail POS");
            dialog.ExceptionType.Should().Be("System.InvalidOperationException");
            dialog.ExceptionMessage.Should().Be("Falla simulada para verificación de diálogo");
            dialog.StackTraceText.Should().Contain("InvalidOperationException");
            dialog.LogFilePath.Should().Be(logFileSimulado);
            dialog.BotonCerrarAppVisibility.Should().Be(Visibility.Collapsed);

            // Salvaguarda de pantalla (SISTEMA_DE_DISENO.md)
            dialog.MaxHeight.Should().Be(SystemParameters.WorkArea.Height);
            dialog.MaxWidth.Should().Be(SystemParameters.WorkArea.Width);
        });

        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();
    }

    [Fact]
    public void UnhandledExceptionDialog_ModoFatal_DebeExhibirAlertaCriticaYBotonCierre()
    {
        // Arrange
        var staThread = new System.Threading.Thread(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                try
                {
                    _ = new System.Windows.Application();
                }
                catch (InvalidOperationException)
                {
                    // Ignorar si ya fue instanciada en el AppDomain
                }
            }

            var dummyException = new InvalidOperationException("Error catastrófico irrecuperable");

            // Act
            var dialog = new UnhandledExceptionDialog(dummyException, isFatal: true);

            // Assert
            dialog.TituloAlerta.Should().Be("Error Crítico del Sistema");
            dialog.BotonCerrarAppVisibility.Should().Be(Visibility.Visible);
            dialog.MensajeOperador.Should().Contain("crítica");
        });

        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();
    }

    [Fact]
    public void UnhandledExceptionDialog_ObtenerReporteDiagnostico_DebeFormatearReporteCorrectamente()
    {
        // Arrange
        var staThread = new System.Threading.Thread(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                try
                {
                    _ = new System.Windows.Application();
                }
                catch (InvalidOperationException)
                {
                    // Ignorar si ya fue instanciada en el AppDomain
                }
            }

            var dummyException = new InvalidOperationException("Falla en mostrador");
            const string logFile = @"C:\logs\retail-20260909.log";

            // Act
            var dialog = new UnhandledExceptionDialog(dummyException, isFatal: false, logFile);
            var reporte = dialog.ObtenerReporteDiagnostico();

            // Assert
            reporte.Should().Contain("RETAIL POS - REPORTE DE INCIDENCIA DE MOSTRADOR");
            reporte.Should().Contain("System.InvalidOperationException");
            reporte.Should().Contain("Falla en mostrador");
            reporte.Should().Contain(logFile);
            reporte.Should().Contain("DETALLE DE LA PILA DE LLAMADAS");
        });

        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        staThread.Join();
    }
}
