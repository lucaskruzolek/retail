using System.Globalization;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Filtro de columna cuyas opciones son los valores de texto que aparecen en la propia lista (el operador de una copia, quien
/// ejecutó una restauración). Envuelve un <see cref="FiltroColumnaViewModel"/> —que trabaja con ids— asignando a cada valor
/// distinto su posición como id, y traduce de vuelta: <see cref="Coincide"/> dice si un valor pasa el filtro aplicado.
/// Al <see cref="Actualizar"/> las opciones conserva lo aplicado por <b>nombre</b>, no por posición, así que recargar la
/// lista no cambia en silencio qué valores se filtran.
/// </summary>
public sealed class FiltroPorValoresViewModel
{
    private static readonly StringComparer Comparador = StringComparer.Create(CultureInfo.GetCultureInfo("es-AR"), ignoreCase: true);

    private List<string> _valores = [];

    public FiltroPorValoresViewModel(string titulo)
    {
        Filtro = new FiltroColumnaViewModel(titulo);
    }

    public FiltroColumnaViewModel Filtro { get; }

    /// <summary>Reemplaza las opciones por los valores distintos de la lista (ordenados) y conserva lo aplicado que siga existiendo.</summary>
    public void Actualizar(IEnumerable<string> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        var elegidos = Filtro.IdsAplicados
            .Where(id => id >= 0 && id < _valores.Count)
            .Select(id => _valores[id])
            .ToList();

        _valores = valores.Distinct(Comparador).OrderBy(v => v, Comparador).ToList();
        Filtro.CargarOpciones(_valores.Select((valor, indice) => new OpcionFiltroColumna(indice, valor)));
        Filtro.AplicarIds(elegidos.Select(nombre => _valores.FindIndex(v => Comparador.Equals(v, nombre))).Where(i => i >= 0));
    }

    /// <summary>Si un valor pasa el filtro: siempre, si no hay ninguno aplicado.</summary>
    public bool Coincide(string valor)
    {
        if (!Filtro.HayFiltroActivo)
        {
            return true;
        }

        return Filtro.IdsAplicados.Any(id => id >= 0 && id < _valores.Count && Comparador.Equals(_valores[id], valor));
    }
}
