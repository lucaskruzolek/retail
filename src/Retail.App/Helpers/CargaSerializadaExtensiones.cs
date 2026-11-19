using System.Runtime.ExceptionServices;

namespace Retail.App.Helpers;

/// <summary>
/// Convierte los callbacks de <see cref="CargaSerializada"/> en un flujo secuencial (<c>await</c>) para quien necesita el
/// resultado de un acceso a datos antes de seguir: el cobro del POS y los diálogos de presupuestos que se abren desde él.
/// </summary>
public static class CargaSerializadaExtensiones
{
    /// <summary>
    /// Ejecuta un acceso a datos en la fila de la pantalla, para no usar el DbContext al mismo tiempo que otro acceso (H-19),
    /// y fuera del hilo de la UI (Ley 4). Si el acceso falla relanza el error original para que lo trate quien llama.
    /// </summary>
    /// <returns><c>Completado = false</c> si la pantalla se descartó y el acceso se canceló.</returns>
    public static async Task<(bool Completado, T Resultado)> EjecutarEnFilaAsync<T>(
        this CargaSerializada carga,
        Func<CancellationToken, Task<T>> acceso)
    {
        ArgumentNullException.ThrowIfNull(carga);
        ArgumentNullException.ThrowIfNull(acceso);

        bool completado = false;
        T resultado = default!;
        ExceptionDispatchInfo? error = null;

        await carga.EjecutarOperacionAsync(
            token => Task.Run(() => acceso(token), token),
            valor =>
            {
                resultado = valor;
                completado = true;
            },
            ex => error = ExceptionDispatchInfo.Capture(ex));

        error?.Throw();
        return (completado, resultado);
    }

    /// <summary>Igual que <see cref="EjecutarEnFilaAsync{T}"/> para una operación sin resultado.</summary>
    /// <returns><c>false</c> si la pantalla se descartó y la operación se canceló.</returns>
    public static async Task<bool> EjecutarOperacionEnFilaAsync(
        this CargaSerializada carga,
        Func<CancellationToken, Task> operacion)
    {
        ArgumentNullException.ThrowIfNull(operacion);

        var (completado, _) = await carga.EjecutarEnFilaAsync(async token =>
        {
            await operacion(token);
            return true;
        });

        return completado;
    }
}
