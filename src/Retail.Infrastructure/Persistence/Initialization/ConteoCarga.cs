namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Qué pasó con las filas de un tipo de diccionario al cargar la semilla. Cada fila cae en una sola de las cuatro
/// cuentas, así que su suma es la cantidad de filas de la semilla de ese tipo.
/// </summary>
public sealed class ConteoCarga
{
    public int Creados { get; private set; }

    /// <summary>
    /// Ya estaban en la base: no se modifican (M-60).
    /// </summary>
    public int Existentes { get; private set; }

    /// <summary>
    /// El comercio los dio de baja: no se vuelven a crear (S-2).
    /// </summary>
    public int DadosDeBaja { get; private set; }

    /// <summary>
    /// Chocan con lo que el comercio cargó: no se cargan y el motivo queda en
    /// <see cref="ResultadoCargaDiccionarios.Conflictos"/>.
    /// </summary>
    public int Omitidos { get; private set; }

    internal void SumarCreado()
    {
        Creados++;
    }

    internal void SumarExistente()
    {
        Existentes++;
    }

    internal void SumarDadoDeBaja()
    {
        DadosDeBaja++;
    }

    internal void SumarOmitido()
    {
        Omitidos++;
    }
}
