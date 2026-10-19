using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Retail.App.Helpers;

/// <summary>
/// Coordina los accesos a datos de una pantalla para que nunca usen su DbContext al mismo tiempo (H-19).
/// </summary>
/// <remarks>
/// <para>Cada pantalla tiene su propio DbContext, y EF Core no admite dos operaciones simultáneas sobre un
/// contexto: la segunda falla aunque la primera ya esté cancelada. Por eso todo acceso pasa por una fila:
/// cada uno espera a que el anterior termine y libere el contexto. Hay dos tipos de acceso:</para>
/// <list type="bullet">
/// <item><see cref="EjecutarAsync{T}"/>: cargas <b>reemplazables</b> (búsquedas, filtros, paginación). Una carga
/// nueva cancela la anterior, y solo la vigente aplica su resultado o informa su error (H-18).</item>
/// <item><see cref="EjecutarOperacionAsync{T}"/>: operaciones que <b>no se reemplazan</b> (leer el estado de la
/// caja, buscar el artículo escaneado). Esperan su turno, pero ninguna carga nueva las cancela.</item>
/// </list>
/// <para>Al descartarse la pantalla (su scope de DI llama a <see cref="Dispose"/>), todo lo pendiente se
/// cancela y ya no toca la pantalla ni informa errores.</para>
/// <para>Una cancelación no se informa al usuario, pero si la consulta llevaba ejecutándose al menos el umbral
/// configurado (5 s por defecto) se deja una advertencia en el log: EF Core solo registra "Executed DbCommand" cuando
/// la consulta termina, y sin esta traza una consulta lenta que se cancela no dejaría ningún rastro. El tiempo se
/// mide desde que el acceso obtiene su turno, sin contar el debounce ni la espera del acceso anterior.</para>
/// </remarks>
public sealed class CargaSerializada : IDisposable
{
    private readonly object _sincronizacion = new();
    private readonly CancellationTokenSource _cierrePantalla = new();
    private CancellationTokenSource? _cargaVigente;
    private Task _accesoAnterior = Task.CompletedTask;
    private bool _descartada;
    private readonly ILogger? _logger;
    private readonly TimeSpan _umbralAdvertencia;

    /// <param name="logger">Registra la advertencia de cargas lentas canceladas; si es nulo, no se registra nada.</param>
    /// <param name="umbralAdvertencia">Duración mínima de la consulta para advertir su cancelación (5 s por defecto).</param>
    public CargaSerializada(ILogger? logger = null, TimeSpan? umbralAdvertencia = null)
    {
        _logger = logger;
        _umbralAdvertencia = umbralAdvertencia ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>Indica si la carga reemplazable vigente todavía no terminó.</summary>
    public bool EnCurso
    {
        get
        {
            lock (_sincronizacion)
            {
                return _cargaVigente is not null;
            }
        }
    }

    /// <summary>
    /// Ejecuta una carga reemplazable: cancela la anterior, espera su turno, consulta y, si sigue vigente,
    /// aplica el resultado.
    /// </summary>
    /// <param name="consultar">Consulta a la base. Debe respetar el token que recibe.</param>
    /// <param name="aplicar">Actualiza la pantalla con el resultado; solo se invoca si la carga sigue vigente.</param>
    /// <param name="alFallar">Informa un error; solo se invoca si la carga sigue vigente y no fue cancelada.</param>
    /// <param name="espera">Espera previa (debounce): si llega otra carga antes de que venza, esta ni siquiera consulta.</param>
    /// <param name="cancellationToken">Cancelación externa (por ejemplo, la del comando que dispara la carga).</param>
    public Task EjecutarAsync<T>(
        Func<CancellationToken, Task<T>> consultar,
        Action<T> aplicar,
        Action<Exception> alFallar,
        TimeSpan espera = default,
        CancellationToken cancellationToken = default)
    {
        return Encolar(consultar, aplicar, alFallar, espera, reemplazable: true, cancellationToken);
    }

    /// <summary>
    /// Ejecuta una operación que espera su turno sobre el DbContext pero que ninguna carga nueva cancela.
    /// Solo se cancela si se descarta la pantalla o con el token externo.
    /// </summary>
    public Task EjecutarOperacionAsync<T>(
        Func<CancellationToken, Task<T>> consultar,
        Action<T> aplicar,
        Action<Exception> alFallar,
        CancellationToken cancellationToken = default)
    {
        return Encolar(consultar, aplicar, alFallar, TimeSpan.Zero, reemplazable: false, cancellationToken);
    }

    public void Dispose()
    {
        lock (_sincronizacion)
        {
            if (_descartada)
            {
                return;
            }

            _descartada = true;
            _cierrePantalla.Cancel();
            _cierrePantalla.Dispose();
        }
    }

    private Task Encolar<T>(
        Func<CancellationToken, Task<T>> consultar,
        Action<T> aplicar,
        Action<Exception> alFallar,
        TimeSpan espera,
        bool reemplazable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consultar);
        ArgumentNullException.ThrowIfNull(aplicar);
        ArgumentNullException.ThrowIfNull(alFallar);

        lock (_sincronizacion)
        {
            if (_descartada)
            {
                return Task.CompletedTask;
            }

            var acceso = CancellationTokenSource.CreateLinkedTokenSource(_cierrePantalla.Token, cancellationToken);
            if (reemplazable)
            {
                _cargaVigente?.Cancel();
                _cargaVigente = acceso;
            }

            _accesoAnterior = EjecutarEnTurnoAsync(acceso, _accesoAnterior, consultar, aplicar, alFallar, espera, reemplazable);
            return _accesoAnterior;
        }
    }

    private async Task EjecutarEnTurnoAsync<T>(
        CancellationTokenSource acceso,
        Task anterior,
        Func<CancellationToken, Task<T>> consultar,
        Action<T> aplicar,
        Action<Exception> alFallar,
        TimeSpan espera,
        bool reemplazable)
    {
        var token = acceso.Token;
        Stopwatch? cronometro = null;
        try
        {
            // WhenAny nunca lanza: lo único que importa es que el acceso anterior haya liberado el DbContext.
            await Task.WhenAny(anterior);

            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, token);
            }

            token.ThrowIfCancellationRequested();
            cronometro = Stopwatch.StartNew();
            var resultado = await consultar(token);

            if (SigueVigente(acceso, reemplazable))
            {
                aplicar(resultado);
            }
            else
            {
                AdvertirSiFueLenta(cronometro);
            }
        }
        catch (Exception) when (token.IsCancellationRequested || !SigueVigente(acceso, reemplazable))
        {
            // Acceso cancelado u obsoleto. Una consulta de SQL Server cancelada no lanza OperationCanceledException
            // sino SqlException ("Operation cancelled by user"), y una pantalla descartada puede producir
            // ObjectDisposedException: en todos los casos el token ya está cancelado, así que no hay nada que informar.
            AdvertirSiFueLenta(cronometro);
        }
        catch (Exception ex)
        {
            alFallar(ex);
        }
        finally
        {
            lock (_sincronizacion)
            {
                if (ReferenceEquals(_cargaVigente, acceso))
                {
                    _cargaVigente = null;
                }
            }

            acceso.Dispose();
        }
    }

    private void AdvertirSiFueLenta(Stopwatch? cronometro)
    {
        if (_logger is null || cronometro is null || cronometro.Elapsed < _umbralAdvertencia)
        {
            return;
        }

        bool cierrePantalla;
        lock (_sincronizacion)
        {
            cierrePantalla = _descartada;
        }

        _logger.LogWarning(
            "Carga descartada tras {Segundos:N1} s sin llegar a la pantalla (motivo: {Motivo}); la consulta pudo estar esperando recursos de SQL Server (memoria o bloqueos).",
            cronometro.Elapsed.TotalSeconds,
            cierrePantalla ? "cierre de pantalla" : "reemplazo por otra carga");
    }

    private bool SigueVigente(CancellationTokenSource acceso, bool reemplazable)
    {
        lock (_sincronizacion)
        {
            return !_descartada && (!reemplazable || ReferenceEquals(_cargaVigente, acceso));
        }
    }
}
