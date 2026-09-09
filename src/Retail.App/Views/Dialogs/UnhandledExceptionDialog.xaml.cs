using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Diálogo modal moderno para despliegue amigable de excepciones no controladas en mostrador.
/// Diseñado bajo las especificaciones de SISTEMA_DE_DISENO.md (Fluent Windows 11 + Mica).
/// </summary>
public partial class UnhandledExceptionDialog : FluentWindow
{
    private readonly Exception _exception;
    private readonly string _logFilePath;

    public string ExceptionType => TxtExceptionType.Text;
    public string ExceptionMessage => TxtExceptionMessage.Text;
    public string StackTraceText => TxtStackTrace.Text;
    public string LogFilePath => TxtLogFilePath.Text;
    public string TituloAlerta => TxtTituloAlerta.Text;
    public string MensajeOperador => TxtMensajeOperador.Text;
    public Visibility BotonCerrarAppVisibility => BtnCerrarApp.Visibility;

    public UnhandledExceptionDialog(Exception exception, bool isFatal = false, string? logFilePath = null)
    {
        InitializeComponent();

        _exception = exception;
        _logFilePath = logFilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", $"retail-{DateTime.Now:yyyyMMdd}.log");

        // Salvaguarda de pantalla para monitores de baja resolución o DPI escalado (SISTEMA_DE_DISENO.md)
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        CargarDetallesExcepcion(isFatal);
    }

    private void CargarDetallesExcepcion(bool isFatal)
    {
        TxtExceptionType.Text = _exception.GetType().FullName ?? "System.Exception";
        TxtExceptionMessage.Text = _exception.Message;
        TxtStackTrace.Text = _exception.ToString();
        TxtLogFilePath.Text = _logFilePath;

        if (isFatal)
        {
            TxtTituloAlerta.Text = "Error Crítico del Sistema";
            TxtMensajeOperador.Text = "Ha ocurrido una falla crítica irrecuperable. Por favor informe al equipo técnico con el detalle registrado antes de reiniciar el punto de venta.";
            BtnCerrarApp.Visibility = Visibility.Visible;
        }
    }

    public string ObtenerReporteDiagnostico()
    {
        return $"""
            ============================================================
            RETAIL POS - REPORTE DE INCIDENCIA DE MOSTRADOR
            ============================================================
            Fecha y Hora : {DateTime.Now:yyyy-MM-dd HH:mm:ss}
            Tipo         : {_exception.GetType().FullName}
            Mensaje      : {_exception.Message}
            Archivo Log  : {_logFilePath}
            ============================================================
            DETALLE DE LA PILA DE LLAMADAS (STACK TRACE):
            {_exception}
            ============================================================
            """;
    }

    private void BtnCopiarDetalles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var contenidoParaCopiar = ObtenerReporteDiagnostico();
            Clipboard.SetText(contenidoParaCopiar);
            TxtBtnCopiar.Text = "¡Detalle Copiado!";
        }
        catch (ExternalException)
        {
            // En entornos bloqueados de portapapeles, evitar lanzar excepción secundaria
            TxtBtnCopiar.Text = "No se pudo acceder al portapapeles";
        }
    }

    private void BtnContinuar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnCerrarApp_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        System.Windows.Application.Current.Shutdown();
    }
}
