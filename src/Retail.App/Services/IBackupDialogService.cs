using Retail.App.ViewModels.Backups;

namespace Retail.App.Services;

/// <summary>
/// Diálogos del módulo de respaldos. Desacopla el ViewModel de la instanciación de ventanas WPF para poder probarlo.
/// </summary>
public interface IBackupDialogService
{
    /// <summary>
    /// Abre el diálogo de credencial, que ejecuta la operación del <paramref name="viewModel"/> al confirmar y muestra
    /// su progreso. Devuelve cuando el diálogo se cierra.
    /// </summary>
    ResultadoCredencial SolicitarCredencial(ConfirmarCredencialViewModel viewModel);

    void MostrarError(string titulo, string mensaje);

    void MostrarInformacion(string titulo, string mensaje);
}
