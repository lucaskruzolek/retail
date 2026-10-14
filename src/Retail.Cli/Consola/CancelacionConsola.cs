namespace Retail.Cli.Consola;

/// <summary>
/// Convierte Ctrl+C en una cancelación ordenada: la primera pulsación pide cancelar y deja que la operación se detenga
/// sola (por ejemplo, descartando la base temporal de una restauración); sin esto, Windows mata el proceso en el acto
/// y deja trabajo a medias. Las pulsaciones siguientes no terminan el proceso: pasado el punto de no retorno, el
/// servicio ignora la cancelación y debe poder completar el intercambio.
/// </summary>
internal sealed class CancelacionConsola : IDisposable
{
    private readonly CancellationTokenSource _fuente = new();

    public CancelacionConsola()
    {
        Console.CancelKeyPress += AlPresionarCtrlC;
    }

    public CancellationToken Token => _fuente.Token;

    public void Dispose()
    {
        Console.CancelKeyPress -= AlPresionarCtrlC;
        _fuente.Dispose();
    }

    private void AlPresionarCtrlC(object? sender, ConsoleCancelEventArgs e)
    {
        // Nunca se deja que Windows mate el proceso: un corte entre los dos renombrados del intercambio dejaría el
        // sistema sin base vigente (B-4). Las pulsaciones siguientes solo repiten el aviso.
        e.Cancel = true;

        if (_fuente.IsCancellationRequested)
        {
            Console.Error.WriteLine("La cancelación ya fue pedida. Espere: cerrar la ventana ahora puede dañar la base.");
            return;
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("Cancelando... espere a que la operación se detenga de forma segura.");

        try
        {
            _fuente.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // La operación terminó justo ahora: no hay nada que cancelar.
        }
    }
}
