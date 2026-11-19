using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Retail.App.Helpers;
using Retail.App.ViewModels.Listas;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Listas;
using Retail.Application.Interfaces.Services;

namespace Retail.App.Services;

/// <summary>
/// Implementación WPF de los diálogos del módulo de listas: el editor y los cuadros estándar de Windows.
/// </summary>
public class ListaDialogService : IListaDialogService
{
    private readonly IListaService _listaService;
    private readonly IVentaService _ventaService;
    private readonly ICurrentUserSession _sesion;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ListaDialogService> _logger;

    public ListaDialogService(
        IListaService listaService,
        IVentaService ventaService,
        ICurrentUserSession sesion,
        ILoggerFactory loggerFactory)
    {
        _listaService = listaService ?? throw new ArgumentNullException(nameof(listaService));
        _ventaService = ventaService ?? throw new ArgumentNullException(nameof(ventaService));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<ListaDialogService>();
    }

    public bool AbrirEditor(ListaDto? lista, CargaSerializada acceso)
    {
        var viewModel = new ListaFormViewModel(
            lista,
            _listaService,
            _ventaService,
            _sesion,
            acceso,
            _loggerFactory.CreateLogger<ListaFormViewModel>());

        var dialog = new ListaFormDialog(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        dialog.ShowDialog();

        return viewModel.Guardada;
    }

    public bool ConfirmarBaja(string nombre)
    {
        return Confirmar(
            "Dar de baja la lista",
            $"¿Dar de baja la lista «{nombre}»? Las ventas pasadas conservan la referencia.");
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        return MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public string? ElegirRutaPdf(string nombreSugerido)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Guardar lista en PDF",
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
        _logger.LogError("Diálogo de error en listas. Título: {Titulo}, Mensaje: {Mensaje}", titulo, mensaje);
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
