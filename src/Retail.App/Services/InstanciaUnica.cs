namespace Retail.App.Services;

/// <summary>
/// Una sola instancia de la aplicación por PC (MT-2). Dos copias abiertas compartirían el archivo de la terminal y
/// competirían por la sesión del mismo usuario, así que la segunda avisa a la primera y se cierra.
/// </summary>
/// <remarks>
/// El mutex vive en el espacio <c>Global\</c>: vale para toda la PC, aunque haya varias sesiones de Windows (cambio
/// rápido de usuario o escritorio remoto). El evento de activación vive en <c>Local\</c>: solo puede avisarse a una
/// instancia de la misma sesión de Windows, que es la única cuya ventana se puede traer al frente. Los nombres se
/// reciben por constructor para poder probar la clase sin chocar con la aplicación real.
/// </remarks>
public sealed class InstanciaUnica : IDisposable
{
    public const string NombreMutexPorDefecto = @"Global\Retail.App";
    public const string NombreEventoPorDefecto = @"Local\Retail.App.Activar";

    private readonly string _nombreMutex;
    private readonly string _nombreEvento;
    private Mutex? _mutex;
    private bool _esDuena;
    private EventWaitHandle? _evento;
    private RegisteredWaitHandle? _espera;

    public InstanciaUnica(string nombreMutex = NombreMutexPorDefecto, string nombreEvento = NombreEventoPorDefecto)
    {
        _nombreMutex = nombreMutex ?? throw new ArgumentNullException(nameof(nombreMutex));
        _nombreEvento = nombreEvento ?? throw new ArgumentNullException(nameof(nombreEvento));
    }

    /// <summary>
    /// Intenta quedarse con el mutex. Devuelve <c>true</c> si esta es la primera instancia. Un mutex abandonado (la
    /// instancia anterior se cayó sin soltarlo) cuenta como adquirido. Debe llamarse en el mismo hilo que después
    /// llama a <see cref="Dispose"/>, porque un mutex pertenece al hilo que lo adquiere.
    /// </summary>
    public bool TryAdquirir()
    {
        if (_esDuena)
        {
            return true;
        }

        _mutex ??= new Mutex(initiallyOwned: false, _nombreMutex);

        try
        {
            _esDuena = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            _esDuena = true;
        }

        return _esDuena;
    }

    /// <summary>
    /// Avisa a la primera instancia (si corre en esta sesión de Windows) que traiga su ventana al frente. Devuelve
    /// <c>false</c> si no hay quien escuche en esta sesión: la otra copia está en otra sesión de Windows.
    /// </summary>
    public bool SenalarInstanciaExistente()
    {
        if (!EventWaitHandle.TryOpenExisting(_nombreEvento, out var evento))
        {
            return false;
        }

        using (evento)
        {
            evento.Set();
        }

        return true;
    }

    /// <summary>
    /// La primera instancia queda escuchando: cada vez que otra copia la señala, ejecuta <paramref name="alActivar"/>
    /// en un hilo del pool (quien lo recibe se encarga de pasar al Dispatcher de la UI).
    /// </summary>
    public void EscucharActivacion(Action alActivar)
    {
        ArgumentNullException.ThrowIfNull(alActivar);

        _evento = new EventWaitHandle(false, EventResetMode.AutoReset, _nombreEvento);
        _espera = ThreadPool.RegisterWaitForSingleObject(
            _evento,
            (_, _) => alActivar(),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _espera?.Unregister(null);
        _espera = null;
        _evento?.Dispose();
        _evento = null;

        if (_mutex is not null)
        {
            if (_esDuena)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Se liberó desde otro hilo: el sistema lo suelta igual al terminar el proceso.
                }
            }

            _mutex.Dispose();
            _mutex = null;
        }

        _esDuena = false;
    }
}
