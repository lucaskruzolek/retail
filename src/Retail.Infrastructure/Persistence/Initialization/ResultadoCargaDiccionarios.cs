namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Resumen de <see cref="SemilleroDiccionarios.CargarAsync"/>: una cuenta por tipo de diccionario y los conflictos con
/// lo que el comercio ya había cargado, para mostrarlos en la consola.
/// </summary>
public sealed class ResultadoCargaDiccionarios
{
    private readonly List<string> _conflictos = [];

    public ConteoCarga Rubros { get; } = new();

    public ConteoCarga Categorias { get; } = new();

    public ConteoCarga Marcas { get; } = new();

    public ConteoCarga Alias { get; } = new();

    public ConteoCarga Prefijos { get; } = new();

    public ConteoCarga Terminos { get; } = new();

    public ConteoCarga Pares { get; } = new();

    public IReadOnlyList<string> Conflictos => _conflictos;

    public int TotalCreados =>
        Rubros.Creados + Categorias.Creados + Marcas.Creados + Alias.Creados + Prefijos.Creados + Terminos.Creados +
        Pares.Creados;

    /// <summary>
    /// La fila no se carga porque choca con lo que el comercio cargó.
    /// </summary>
    internal void Omitir(ConteoCarga conteo, string motivo)
    {
        conteo.SumarOmitido();
        _conflictos.Add(motivo);
    }

    /// <summary>
    /// La fila ya existía, pero distinta de como la trae la semilla: se deja como está y se avisa (S-3).
    /// </summary>
    internal void ExistenteConDiferencias(ConteoCarga conteo, string motivo)
    {
        conteo.SumarExistente();
        _conflictos.Add(motivo);
    }
}
