namespace Retail.App.ViewModels.Terminales;

/// <summary>
/// Qué se hace al activar esta PC: dar de alta una terminal nueva o tomar el lugar de una existente (cambio de hardware).
/// </summary>
public enum ModoActivacionTerminal
{
    /// <summary>Un puesto nuevo: se crea la terminal con el nombre que se escriba.</summary>
    Nueva,

    /// <summary>La PC reemplaza al equipo de una terminal existente y hereda su historial de turnos.</summary>
    Reemplazo
}
