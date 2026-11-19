using Retail.App.ViewModels.Terminales;

namespace Retail.App.Services;

/// <summary>
/// Cuadros que puede mostrar la verificación de la terminal al arrancar, antes de que exista la ventana principal.
/// Se abstraen para poder probar la decisión de <see cref="VerificacionTerminalArranque"/> sin abrir ventanas.
/// </summary>
public interface IDialogosArranqueTerminal
{
    /// <summary>Aviso informativo: la aplicación sigue abriéndose.</summary>
    void Informar(string mensaje);

    /// <summary>Aviso de bloqueo: tras cerrarlo, la aplicación termina.</summary>
    void Bloquear(string mensaje);

    /// <summary>Abre "Activar esta PC" y devuelve si el Gerente la activó (<c>false</c> si canceló o cerró el diálogo).</summary>
    bool ActivarTerminal(ActivarTerminalViewModel viewModel);
}
