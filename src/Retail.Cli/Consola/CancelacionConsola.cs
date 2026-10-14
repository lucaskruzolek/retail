namespace Retail.Cli.Consola;

/// <summary>
/// Convierte Ctrl+C en una cancelación ordenada: la primera pulsación pide cancelar y deja que la operación se detenga
/// sola (por ejemplo, descartando la base temporal de una restauración); sin esto, Windows mata el proceso en el acto
/// y deja trabajo a medias.
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
        // Una segunda pulsación, con la cancelación ya pedida, termina el proceso de inmediato.
        if (_fuente.IsCancellationRequested)
        {
            return;
        }

        e.Cancel = true;
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
