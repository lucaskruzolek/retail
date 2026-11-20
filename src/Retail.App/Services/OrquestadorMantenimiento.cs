using System.Globalization;
using Retail.Application.Interfaces.Services;

namespace Retail.App.Services;

/// <summary>
/// Cómo terminó la preparación de la restauración (MT-12).
/// </summary>
/// <param name="Continuar">Verdadero si ya se puede restaurar; falso si el operador canceló o decidió no forzar.</param>
/// <param name="MantenimientoProgramado">Verdadero si se programó un aviso de mantenimiento que sigue vigente y que quien
/// llama debe cancelar si finalmente no restaura.</param>
public sealed record ResultadoPreparacionMantenimiento(bool Continuar, bool MantenimientoProgramado);

/// <summary>
/// Orquesta la espera previa a restaurar la base con otras terminales conectadas (MT-12, §4.7 de la auditoría): cuenta las
/// terminales, pide al operador cuánto tiempo darles, programa el aviso, espera (sondeando cada 5 segundos) a que se vayan
/// y, vencido el plazo, pregunta si se restaura igual. No restaura: devuelve la decisión y la pantalla abre la restauración
/// con control dual.
/// </summary>
/// <remarks>
/// Vive aparte del ViewModel para poder probar los plazos sin esperar de verdad: la espera y el reloj se inyectan. Las
/// consultas a los servicios corren en <see cref="Task.Run(Func{Task})"/> (Ley 4); los diálogos, en el hilo desde el que se
/// llama (el de la interfaz).
/// </remarks>
public sealed class OrquestadorMantenimiento
{
    /// <summary>Cada cuánto se vuelve a contar las terminales conectadas.</summary>
    public static readonly TimeSpan IntervaloDeSondeo = TimeSpan.FromSeconds(5);

    /// <summary>Gracia que se suma al plazo programado antes de ofrecer restaurar igual.</summary>
    public static readonly TimeSpan GraciaPostPlazo = TimeSpan.FromMinutes(1);

    private const string MotivoDelAviso = "Restauración de la base de datos";

    private readonly IRestauracionService _restauracion;
    private readonly ISesionService _sesiones;
    private readonly IBackupDialogService _dialogos;
    private readonly TimeProvider _reloj;
    private readonly Func<TimeSpan, CancellationToken, Task> _esperar;

    public OrquestadorMantenimiento(
        IRestauracionService restauracion,
        ISesionService sesiones,
        IBackupDialogService dialogos,
        TimeProvider? reloj = null,
        Func<TimeSpan, CancellationToken, Task>? esperar = null)
    {
        _restauracion = restauracion ?? throw new ArgumentNullException(nameof(restauracion));
        _sesiones = sesiones ?? throw new ArgumentNullException(nameof(sesiones));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        _reloj = reloj ?? TimeProvider.System;
        _esperar = esperar ?? Task.Delay;
    }

    /// <summary>
    /// Deja la base lista para restaurar. Sin otras terminales conectadas devuelve de inmediato, sin programar nada.
    /// </summary>
    public async Task<ResultadoPreparacionMantenimiento> PrepararAsync(
        int idActor,
        IProgress<string>? progreso,
        CancellationToken cancellationToken)
    {
        var conectadas = await ContarAsync(idActor, cancellationToken);
        if (conectadas == 0)
        {
            return new ResultadoPreparacionMantenimiento(true, false);
        }

        var minutos = _dialogos.SolicitarProgramacionMantenimiento(conectadas);
        if (minutos is null)
        {
            return new ResultadoPreparacionMantenimiento(false, false);
        }

        await Task.Run(() => _sesiones.ProgramarMantenimientoAsync(idActor, minutos.Value, MotivoDelAviso, cancellationToken), cancellationToken);

        var plazo = _reloj.GetUtcNow().AddMinutes(minutos.Value);
        var limite = plazo + GraciaPostPlazo;
        var hasta = plazo.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

        try
        {
            while (true)
            {
                conectadas = await ContarAsync(idActor, cancellationToken);
                if (conectadas == 0)
                {
                    return new ResultadoPreparacionMantenimiento(true, true);
                }

                if (_reloj.GetUtcNow() >= limite)
                {
                    // Vencido el plazo: decide el operador. Restaurar igual desconecta a las terminales que queden.
                    return _dialogos.ConfirmarRestaurarConTerminalesConectadas(conectadas)
                        ? new ResultadoPreparacionMantenimiento(true, true)
                        : await CancelarYResponderAsync(idActor);
                }

                progreso?.Report($"Esperando que se desconecten {conectadas} terminal(es)… (hasta las {hasta})");
                await _esperar(IntervaloDeSondeo, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // El operador canceló la espera: el aviso no debe quedar vigente en las terminales.
            return await CancelarYResponderAsync(idActor);
        }
        catch (Exception)
        {
            // Una falla al contar no debe dejar el aviso vigente: se cancela y se propaga para que la pantalla la informe.
            await CancelarAsync(idActor);
            throw;
        }
    }

    /// <summary>
    /// Cancela el aviso de mantenimiento. Para cuando ya se programó y la restauración no llegó a ejecutarse (por ejemplo, el
    /// operador cerró el diálogo de credencial). Es un esfuerzo razonable: si falla, el aviso vence solo y no se propaga.
    /// </summary>
    public async Task CancelarAsync(int idActor)
    {
        try
        {
            await Task.Run(() => _sesiones.CancelarMantenimientoAsync(idActor, CancellationToken.None));
        }
        catch (Exception)
        {
            // Ver el resumen: el aviso tiene vencimiento propio, no se enmascara el motivo original con este error.
        }
    }

    private async Task<ResultadoPreparacionMantenimiento> CancelarYResponderAsync(int idActor)
    {
        await CancelarAsync(idActor);
        return new ResultadoPreparacionMantenimiento(false, false);
    }

    private Task<int> ContarAsync(int idActor, CancellationToken cancellationToken)
    {
        return Task.Run(() => _restauracion.ContarTerminalesConectadasAsync(idActor, cancellationToken), cancellationToken);
    }
}
