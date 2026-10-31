using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Estado de un filtro de columna con selección múltiple (control <c>FiltroColumna</c>). Separa dos cosas:
/// <list type="bullet">
/// <item><description>El <b>borrador</b>: las casillas que el usuario marca con el filtro abierto
/// (<see cref="OpcionFiltroColumna.Seleccionada"/>). No consulta nada.</description></item>
/// <item><description>Lo <b>aplicado</b> (<see cref="IdsAplicados"/>): cambia solo con "Aplicar", que avisa con
/// <see cref="Aplicado"/> para que la pantalla vuelva a consultar. Cancelar o cerrar haciendo clic afuera descartan el
/// borrador, y al volver a abrir se parte de lo aplicado.</description></item>
/// </list>
/// Sin casillas marcadas (o con todas) no se filtra: <see cref="IdsAplicados"/> queda vacío, igual que la lista vacía de
/// <c>ConsultaArticulosDto</c>. La búsqueda dentro del filtro solo oculta casillas de una lista que ya está en memoria y
/// es chica; los artículos se siguen filtrando en SQL Server (Ley 8).
/// </summary>
public partial class FiltroColumnaViewModel : ObservableObject
{
    private static readonly CompareInfo Comparacion = CultureInfo.GetCultureInfo("es-AR").CompareInfo;

    private IReadOnlyList<int> _idsAplicados = [];

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    /// <summary>El popup del filtro está abierto (enlazado en ambos sentidos con el botón de la cabecera).</summary>
    [ObservableProperty]
    private bool _estaAbierto;

    public FiltroColumnaViewModel(string titulo)
    {
        Titulo = titulo ?? throw new ArgumentNullException(nameof(titulo));
    }

    /// <summary>Lo aplicado cambió: la pantalla tiene que volver a consultar.</summary>
    public event EventHandler? Aplicado;

    public string Titulo { get; }

    public ObservableCollection<GrupoFiltroColumna> Grupos { get; } = new();

    public bool TieneOpciones => Grupos.Count > 0;

    /// <summary>Valores elegidos con "Aplicar"; vacío = sin filtro.</summary>
    public IReadOnlyList<int> IdsAplicados => _idsAplicados;

    public bool HayFiltroActivo => _idsAplicados.Count > 0;

    /// <summary>Falso cuando la búsqueda dentro del filtro no coincide con ninguna opción.</summary>
    public bool HayCoincidencias => Grupos.Any(g => g.Visible);

    /// <summary>
    /// Reemplaza las opciones (al cargar la pantalla o tras administrar la clasificación). Conserva lo aplicado que siga
    /// existiendo; si algo aplicado desapareció, avisa con <see cref="Aplicado"/>, porque la consulta en curso usó un
    /// valor que ya no existe. Las opciones se agrupan por <see cref="OpcionFiltroColumna.Grupo"/> en el orden en que llegan.
    /// </summary>
    public void CargarOpciones(IEnumerable<OpcionFiltroColumna> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        Grupos.Clear();
        foreach (var grupo in opciones.GroupBy(o => o.Grupo))
        {
            Grupos.Add(new GrupoFiltroColumna(grupo.Key, grupo.ToList()));
        }

        var existentes = TodasLasOpciones().Select(o => o.Id).ToHashSet();
        var vigentes = _idsAplicados.Where(existentes.Contains).ToList();
        bool cambio = vigentes.Count != _idsAplicados.Count;
        _idsAplicados = vigentes;

        RestaurarBorrador();
        AplicarBusqueda();
        OnPropertyChanged(nameof(TieneOpciones));
        NotificarAplicado();

        if (cambio)
        {
            Aplicado?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Vuelve a "sin filtro" sin avisar: lo usa "Limpiar filtros" de la pantalla, que consulta una sola vez.</summary>
    public void QuitarFiltro()
    {
        _idsAplicados = [];
        RestaurarBorrador();
        NotificarAplicado();
    }

    [RelayCommand]
    private void Aplicar()
    {
        var todas = TodasLasOpciones().ToList();
        var marcadas = todas.Where(o => o.Seleccionada).Select(o => o.Id).ToList();
        IReadOnlyList<int> nuevos = marcadas.Count == todas.Count ? [] : marcadas;
        bool cambio = !nuevos.SequenceEqual(_idsAplicados);

        _idsAplicados = nuevos;
        NotificarAplicado();
        EstaAbierto = false;

        if (cambio)
        {
            Aplicado?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        EstaAbierto = false;
    }

    /// <summary>Marca las casillas visibles: con una búsqueda escrita, solo las que coinciden.</summary>
    [RelayCommand]
    private void SeleccionarTodo()
    {
        foreach (var opcion in TodasLasOpciones().Where(o => o.Visible))
        {
            opcion.Seleccionada = true;
        }
    }

    /// <summary>Desmarca todo el borrador; hace falta "Aplicar" para quitar el filtro.</summary>
    [RelayCommand]
    private void Limpiar()
    {
        foreach (var opcion in TodasLasOpciones())
        {
            opcion.Seleccionada = false;
        }
    }

    partial void OnEstaAbiertoChanged(bool value)
    {
        // Al abrir se parte siempre de lo aplicado: así un borrador cancelado (o cerrado con un clic afuera) no reaparece.
        if (value)
        {
            TextoBusqueda = string.Empty;
            RestaurarBorrador();
        }
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        AplicarBusqueda();
    }

    private void AplicarBusqueda()
    {
        string termino = TextoBusqueda.Trim();
        foreach (var opcion in TodasLasOpciones())
        {
            // Sin distinguir mayúsculas ni tildes: "lapiz" encuentra "Lápiz", como el buscador del catálogo.
            opcion.Visible = termino.Length == 0 ||
                Comparacion.IndexOf(opcion.Nombre, termino, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        OnPropertyChanged(nameof(HayCoincidencias));
    }

    private void RestaurarBorrador()
    {
        var aplicados = _idsAplicados.ToHashSet();
        foreach (var opcion in TodasLasOpciones())
        {
            opcion.Seleccionada = aplicados.Contains(opcion.Id);
        }
    }

    private void NotificarAplicado()
    {
        OnPropertyChanged(nameof(IdsAplicados));
        OnPropertyChanged(nameof(HayFiltroActivo));
    }

    private IEnumerable<OpcionFiltroColumna> TodasLasOpciones()
    {
        return Grupos.SelectMany(g => g.Opciones);
    }
}
