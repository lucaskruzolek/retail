using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.ViewModels.Backups;
using Retail.App.Views.Dialogs;

namespace Retail.App.Services;

/// <summary>
/// Implementación de los diálogos del módulo de respaldos con ventanas Fluent modales.
/// </summary>
public class BackupDialogService : IBackupDialogService
{
    private readonly ILogger<BackupDialogService> _logger;

    public BackupDialogService(ILogger<BackupDialogService>? logger = null)
    {
        _logger = logger ?? NullLogger<BackupDialogService>.Instance;
    }

    public ResultadoCredencial SolicitarCredencial(ConfirmarCredencialViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var dialog = new ConfirmarCredencialDialog(viewModel);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();

        return new ResultadoCredencial(viewModel.Completada, viewModel.FalloOperacion);
    }

    public void MostrarError(string titulo, string mensaje)
    {
        _logger.LogError("Diálogo de error presentado en el módulo de respaldos. Título: {Titulo}, Mensaje: {Mensaje}", titulo, mensaje);

        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void MostrarInformacion(string titulo, string mensaje)
    {
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
