namespace Retail.App.Services;

/// <summary>
/// Estado único (Singleton) del mantenimiento programado. El latido lo escribe con <see cref="Establecer"/> y
/// <see cref="Limpiar"/>; el resto de la aplicación lo lee a través de <see cref="IEstadoMantenimiento"/>.
/// </summary>
public sealed class EstadoMantenimiento : IEstadoMantenimiento
{
    /// <summary>Margen previo: desde un minuto antes de la hora indicada ya no se inician cobros.</summary>
    public static readonly TimeSpan MargenBloqueo = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _reloj;

    public EstadoMantenimiento(TimeProvider? reloj = null)
    {
        _reloj = reloj ?? TimeProvider.System;
    }

    public bool HayMantenimientoProgramado => ProgramadoParaLocal is not null;

    public DateTime? ProgramadoParaLocal { get; private set; }

    public string? Motivo { get; private set; }

    public bool CobrosBloqueados =>
        ProgramadoParaLocal is { } programado && _reloj.GetLocalNow().DateTime >= programado - MargenBloqueo;

    public event Action? Cambio;

    public void Establecer(DateTime programadoParaLocal, string? motivo)
    {
        ProgramadoParaLocal = programadoParaLocal;
        Motivo = motivo;
        Cambio?.Invoke();
    }

    public void Limpiar()
    {
        if (ProgramadoParaLocal is null)
        {
            return;
        }

        ProgramadoParaLocal = null;
        Motivo = null;
        Cambio?.Invoke();
    }
}
