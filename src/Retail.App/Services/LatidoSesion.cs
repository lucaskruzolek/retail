using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Retail.Application.DTOs.Sesiones;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services;

namespace Retail.App.Services;

/// <summary>
/// Latido de la sesión única (MT-6): cada minuto renueva la sesión de este usuario en esta terminal. Con la respuesta
/// detecta que la sesión se perdió (la tomó otra terminal o la cerró un Gerente) y recoge el aviso de mantenimiento
/// programado (MT-12). Es Singleton, como el Shell que lo arranca.
/// </summary>
/// <remarks>
/// Corre en una tarea de fondo con <see cref="PeriodicTimer"/> (Ley 4) y en cada pulso abre un scope de DI nuevo: el
/// latido nunca comparte el DbContext de una pantalla (H-19). Un fallo de red o de SQL no lo detiene: se registra y
/// reintenta en el próximo pulso, porque la sesión recién vence a los 3 minutos sin latido.
/// </remarks>
public sealed class LatidoSesion : IDisposable
{
    /// <summary>Tiempo máximo de espera al cerrar la sesión al salir: no se demora el cierre de la aplicación por una red caída.</summary>
    public static readonly TimeSpan EsperaCierre = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EstadoMantenimiento _estadoMantenimiento;
    private readonly IAvisosSesion _avisos;
    private readonly ILogger<LatidoSesion> _logger;
    private readonly TimeSpan _intervalo;
    private readonly TimeProvider _reloj;

    private CancellationTokenSource? _cancelacion;
    private Task? _tarea;
    private int _idUsuario;
    private int _idTerminal;
    private DateTime? _ultimoMantenimientoUtc;
    private string? _ultimoMotivo;
    private bool _avisoRelojMostrado;
    private bool _avisoEsquemaMostrado;
    private int _latidosDesdeElControlDeEsquema;

    public LatidoSesion(
        IServiceScopeFactory scopeFactory,
        EstadoMantenimiento estadoMantenimiento,
        IAvisosSesion avisos,
        ILogger<LatidoSesion> logger,
        TimeSpan? intervalo = null,
        TimeProvider? reloj = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _estadoMantenimiento = estadoMantenimiento ?? throw new ArgumentNullException(nameof(estadoMantenimiento));
        _avisos = avisos ?? throw new ArgumentNullException(nameof(avisos));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _intervalo = intervalo ?? ParametrosSesion.IntervaloLatido;
        _reloj = reloj ?? TimeProvider.System;
    }

    /// <summary>Verdadero mientras el bucle de latidos está corriendo.</summary>
    public bool EstaActivo => _tarea is { IsCompleted: false };

    /// <summary>Arranca el bucle de latidos para esta sesión. Llamarlo de nuevo con el bucle activo no hace nada.</summary>
    public void Iniciar(int idUsuario, int idTerminal)
    {
        if (_tarea is not null)
        {
            return;
        }

        _idUsuario = idUsuario;
        _idTerminal = idTerminal;
        _cancelacion = new CancellationTokenSource();
        var token = _cancelacion.Token;
        _tarea = Task.Run(() => BucleAsync(token), token);
    }

    /// <summary>
    /// Detiene el bucle y, si había una sesión iniciada, la cierra en la base (mejor esfuerzo, con espera acotada):
    /// libera la sesión de inmediato para que el usuario pueda abrir otra terminal sin esperar el vencimiento.
    /// </summary>
    public async Task FinalizarAsync()
    {
        await DetenerAsync();

        if (_idUsuario == 0)
        {
            return;
        }

        try
        {
            using var espera = new CancellationTokenSource(EsperaCierre);
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider
                .GetRequiredService<ISesionService>()
                .CerrarAsync(_idUsuario, _idTerminal, espera.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cerrar la sesión del usuario {IdUsuario} al salir; vencerá sola", _idUsuario);
        }
    }

    /// <summary>Detiene el bucle de latidos y espera a que termine el pulso en curso.</summary>
    public async Task DetenerAsync()
    {
        if (_cancelacion is null || _tarea is null)
        {
            return;
        }

        await _cancelacion.CancelAsync();
        try
        {
            await _tarea;
        }
        catch (OperationCanceledException)
        {
            // Cancelación pedida por el propio cierre.
        }
    }

    public void Dispose()
    {
        _cancelacion?.Cancel();
        _cancelacion?.Dispose();
        _cancelacion = null;
    }

    private async Task BucleAsync(CancellationToken token)
    {
        using var temporizador = new PeriodicTimer(_intervalo);

        try
        {
            while (await temporizador.WaitForNextTickAsync(token))
            {
                if (!await LatirAsync(token))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cierre ordenado de la aplicación.
        }
    }

    /// <summary>Un pulso. Devuelve <c>false</c> si la sesión se perdió y el bucle debe terminar.</summary>
    private async Task<bool> LatirAsync(CancellationToken token)
    {
        EstadoLatidoDto estado;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            estado = await scope.ServiceProvider
                .GetRequiredService<ISesionService>()
                .RenovarAsync(_idUsuario, _idTerminal, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo renovar la sesión del usuario {IdUsuario}; se reintenta en el próximo latido", _idUsuario);
            return true;
        }

        if (!estado.SesionVigente)
        {
            _logger.LogWarning("La sesión del usuario {IdUsuario} en la terminal {IdTerminal} ya no está vigente", _idUsuario, _idTerminal);
            await _avisos.AvisarSesionPerdidaYSalirAsync();
            return false;
        }

        ActualizarMantenimiento(estado);
        VerificarRelojDeLaPc(estado);
        await VerificarEsquemaPeriodicamenteAsync(token);
        return true;
    }

    /// <summary>
    /// H10: las fechas de negocio se registran con la hora de esta PC, así que un reloj corrido desordena el historial.
    /// Con cada latido se compara con la hora del servidor (la demora de una consulta de red es despreciable frente al
    /// umbral de minutos) y se avisa una sola vez; si el reloj se corrige y vuelve a desviarse, avisa de nuevo.
    /// </summary>
    private void VerificarRelojDeLaPc(EstadoLatidoDto estado)
    {
        var desfase = estado.AhoraServidorUtc - _reloj.GetUtcNow().UtcDateTime;
        if (desfase.Duration() <= ParametrosSesion.UmbralDesfaseReloj)
        {
            _avisoRelojMostrado = false;
            return;
        }

        if (_avisoRelojMostrado)
        {
            return;
        }

        _avisoRelojMostrado = true;
        var minutos = (int)Math.Round(desfase.Duration().TotalMinutes);
        var sentido = desfase < TimeSpan.Zero ? "adelantado" : "atrasado";
        _logger.LogWarning("El reloj de esta PC está {Minutos} minutos {Sentido} respecto del servidor", minutos, sentido);
        _avisos.AvisarAdvertencia(string.Create(
            CultureInfo.CurrentCulture,
            $"El reloj de esta PC está {minutos} minutos {sentido} respecto del servidor. Las fechas de las ventas se registran con la hora de esta PC: pida que se corrija la hora de Windows (sincronización automática)."));
    }

    /// <summary>
    /// H12: cada <see cref="ParametrosSesion.LatidosEntreControlesDeEsquema"/> latidos comprueba, en un scope propio, si
    /// la base se migró a una versión más nueva que esta aplicación (el chequeo de arranque no alcanza a una terminal ya
    /// abierta). Avisa una sola vez y, una vez avisado, deja de consultar. Un fallo se registra y se ignora, como el del
    /// resto del latido.
    /// </summary>
    private async Task VerificarEsquemaPeriodicamenteAsync(CancellationToken token)
    {
        if (_avisoEsquemaMostrado)
        {
            return;
        }

        _latidosDesdeElControlDeEsquema++;
        if (_latidosDesdeElControlDeEsquema < ParametrosSesion.LatidosEntreControlesDeEsquema)
        {
            return;
        }

        _latidosDesdeElControlDeEsquema = 0;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var esquema = await scope.ServiceProvider
                .GetRequiredService<IEsquemaBaseDatos>()
                .ObtenerEstadoAsync(token);

            if (esquema.MigracionesDesconocidas.Count > 0)
            {
                _avisoEsquemaMostrado = true;
                _logger.LogWarning(
                    "La base tiene migraciones que esta versión no conoce: {Migraciones}",
                    string.Join(", ", esquema.MigracionesDesconocidas));
                _avisos.AvisarAdvertencia(
                    "La base de datos se actualizó a una versión más nueva de Retail. Termine la operación en curso, cierre la aplicación y actualícela en esta PC.");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo verificar el esquema de la base; se reintenta en unos latidos");
        }
    }

    private void ActualizarMantenimiento(EstadoLatidoDto estado)
    {
        if (estado.MantenimientoProgramadoParaUtc is not { } programadoUtc)
        {
            _ultimoMantenimientoUtc = null;
            _ultimoMotivo = null;
            _estadoMantenimiento.Limpiar();
            return;
        }

        if (programadoUtc == _ultimoMantenimientoUtc && estado.MotivoMantenimiento == _ultimoMotivo)
        {
            return;
        }

        // La hora local se arma sumando a "ahora local" lo que falta según el reloj del servidor. Así el reloj de esta PC
        // (que puede estar adelantado o atrasado) nunca decide cuándo empieza el mantenimiento: solo se usa para mostrarlo.
        var faltante = programadoUtc - estado.AhoraServidorUtc;
        var programadoLocal = _reloj.GetLocalNow().DateTime + faltante;

        _ultimoMantenimientoUtc = programadoUtc;
        _ultimoMotivo = estado.MotivoMantenimiento;
        _estadoMantenimiento.Establecer(programadoLocal, estado.MotivoMantenimiento);

        var motivo = string.IsNullOrWhiteSpace(estado.MotivoMantenimiento) ? string.Empty : $": {estado.MotivoMantenimiento}";
        _avisos.AvisarMantenimiento(string.Create(
            CultureInfo.CurrentCulture,
            $"Mantenimiento programado a las {programadoLocal:HH:mm}{motivo}. Termine la venta en curso; desde ese momento no se podrán iniciar cobros."));
    }
}
