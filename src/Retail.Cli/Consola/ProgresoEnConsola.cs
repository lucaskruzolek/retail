namespace Retail.Cli.Consola;

/// <summary>
/// Muestra el avance en la misma línea, en el orden en que llega.
/// </summary>
/// <remarks>
/// No se usa <see cref="Progress{T}"/>: sin un contexto de sincronización (la consola no tiene uno), publica cada
/// aviso en un hilo del pool y los porcentajes pueden imprimirse desordenados. En WPF sí sirve, porque el contexto de
/// la UI los ordena.
/// </remarks>
internal sealed class ProgresoEnConsola : IProgress<int>
{
    private int _ultimo = -1;

    public void Report(int value)
    {
        if (value <= _ultimo)
        {
            return;
        }

        _ultimo = value;
        Console.Write($" {value}%");
    }
}
