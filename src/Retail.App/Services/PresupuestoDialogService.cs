using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Retail.App.ViewModels.Presupuestos;
using Retail.App.Views.Dialogs;
using Retail.Application.Interfaces.Services;

namespace Retail.App.Services;

/// <summary>
/// Implementación WPF de los diálogos del módulo de presupuestos: cuadros estándar de Windows y el modal de detalle.
/// </summary>
public class PresupuestoDialogService : IPresupuestoDialogService
{
    private readonly IPresupuestoService _presupuestoService;
    private readonly TimeProvider _reloj;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PresupuestoDialogService> _logger;

    public PresupuestoDialogService(
        IPresupuestoService presupuestoService,
        TimeProvider reloj,
        ILoggerFactory loggerFactory)
    {
        _presupuestoService = presupuestoService ?? throw new ArgumentNullException(nameof(presupuestoService));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PresupuestoDialogService>();
    }

    public void MostrarDetalle(int idPresupuesto)
    {
        var viewModel = new DetallePresupuestoViewModel(
            idPresupuesto,
            _presupuestoService,
            this,
            _reloj,
            _loggerFactory.CreateLogger<DetallePresupuestoViewModel>());

        var dialog = new DetallePresupuestoDialog(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        dialog.ShowDialog();
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        return MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public string? ElegirRutaPdf(string nombreSugerido)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Guardar presupuesto en PDF",
            FileName = nombreSugerido,
            Filter = "Documento PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            OverwritePrompt = true
        };

        var propietario = System.Windows.Application.Current?.MainWindow is { IsVisible: true } ventana ? ventana : null;

        return dialogo.ShowDialog(propietario) == true ? dialogo.FileName : null;
    }

    public void MostrarInformacion(string titulo, string mensaje)
    {
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void MostrarAlerta(string titulo, string mensaje)
    {
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void MostrarError(string titulo, string mensaje)
    {
        _logger.LogError("Diálogo de error en presupuestos. Título: {Titulo}, Mensaje: {Mensaje}", titulo, mensaje);
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
