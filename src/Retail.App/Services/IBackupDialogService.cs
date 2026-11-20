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

    /// <summary>
    /// Avisa que hay terminales conectadas y pregunta cuántos minutos darles antes de restaurar (0 = ahora). Devuelve
    /// <c>null</c> si el operador cancela.
    /// </summary>
    int? SolicitarProgramacionMantenimiento(int terminalesConectadas);

    /// <summary>
    /// Vencido el plazo del mantenimiento, pregunta si se restaura aunque queden terminales conectadas (se desconectarán).
    /// </summary>
    bool ConfirmarRestaurarConTerminalesConectadas(int terminalesConectadas);

    void MostrarError(string titulo, string mensaje);

    void MostrarInformacion(string titulo, string mensaje);
}
