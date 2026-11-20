namespace Retail.App.Services;

/// <summary>
/// Aviso de mantenimiento programado tal como lo ve la UI (MT-12): solo lectura. Lo actualiza el latido de la sesión.
/// </summary>
public interface IEstadoMantenimiento
{
    bool HayMantenimientoProgramado { get; }

    /// <summary>Hora local en que empieza el mantenimiento, o <c>null</c> si no hay ninguno.</summary>
    DateTime? ProgramadoParaLocal { get; }

    string? Motivo { get; }

    /// <summary>
    /// Verdadero desde el minuto previo al mantenimiento: ya no se deben iniciar cobros. Se calcula en el momento de la
    /// consulta, así que no hace falta esperar al próximo latido.
    /// </summary>
    bool CobrosBloqueados { get; }

    /// <summary>Se dispara cuando aparece, cambia o desaparece el aviso. Puede llegar desde un hilo de fondo.</summary>
    event Action? Cambio;
}
