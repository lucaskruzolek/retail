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
/// </remarks>
public sealed class CargaSerializada : IDisposable
{
    private readonly object _sincronizacion = new();
    private readonly CancellationTokenSource _cierrePantalla = new();
    private CancellationTokenSource? _cargaVigente;
    private Task _accesoAnterior = Task.CompletedTask;
    private bool _descartada;

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
        try
        {
            // WhenAny nunca lanza: lo único que importa es que el acceso anterior haya liberado el DbContext.
            await Task.WhenAny(anterior);

            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, token);
            }

            token.ThrowIfCancellationRequested();
            var resultado = await consultar(token);

            if (SigueVigente(acceso, reemplazable))
            {
                aplicar(resultado);
            }
        }
        catch (Exception) when (token.IsCancellationRequested || !SigueVigente(acceso, reemplazable))
        {
            // Acceso cancelado u obsoleto. Una consulta de SQL Server cancelada no lanza OperationCanceledException
            // sino SqlException ("Operation cancelled by user"), y una pantalla descartada puede producir
            // ObjectDisposedException: en todos los casos el token ya está cancelado, así que no hay nada que informar.
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

    private bool SigueVigente(CancellationTokenSource acceso, bool reemplazable)
    {
        lock (_sincronizacion)
        {
            return !_descartada && (!reemplazable || ReferenceEquals(_cargaVigente, acceso));
        }
    }
}
