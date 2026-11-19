using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Editor de renglones reutilizable (módulo 5.4, PE-09): buscador de artículos vendibles (el mismo servicio que el del
/// POS), renglones con cantidad, subtotal a los precios de los renglones y aviso de artículos dados de baja. Lo componen
/// el editor de listas y el de presupuestos. El anfitrión (el ViewModel del diálogo) se entera de cualquier cambio por
/// <see cref="ContenidoCambiado"/> para recalcular sus totales y sus comandos, y de los avisos por <see cref="MensajeEmitido"/>.
/// </summary>
public partial class EditorRenglonesViewModel : ObservableObject
{
    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    private const int LongitudMinimaBusqueda = 2;
    private const int CantidadMaxima = 99999;

    private readonly IVentaService _ventaService;
    private readonly CargaSerializada _acceso;
    private readonly ILogger _logger;
    private readonly string _textoQuitar;
    private readonly string _textoUltimoRenglon;

    /// <param name="acceso">Fila de accesos a datos de la pantalla que abre el editor, para no usar su DbContext en paralelo (H-19).</param>
    /// <param name="exigirAlMenosUnRenglon">Si es <c>true</c>, la última fila no se puede quitar (una lista tiene al menos un artículo).</param>
    /// <param name="textoQuitar">Tooltip del botón de quitar cuando se puede.</param>
    /// <param name="textoUltimoRenglon">Tooltip del botón de quitar cuando es la última fila y no se puede.</param>
    public EditorRenglonesViewModel(
        IVentaService ventaService,
        CargaSerializada acceso,
        ILogger logger,
        bool exigirAlMenosUnRenglon = false,
        string textoQuitar = "Quitar el artículo",
        string textoUltimoRenglon = "Debe quedar al menos un artículo")
    {
        _ventaService = ventaService ?? throw new ArgumentNullException(nameof(ventaService));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ExigirAlMenosUnRenglon = exigirAlMenosUnRenglon;
        _textoQuitar = textoQuitar;
        _textoUltimoRenglon = textoUltimoRenglon;

        Renglones.CollectionChanged += AlCambiarRenglones;
    }

    /// <summary>Se produjo algún cambio que afecta a los totales: renglones agregados o quitados, cantidad o precio modificados.</summary>
    public event EventHandler? ContenidoCambiado;

    /// <summary>
    /// Aviso para el usuario (error de búsqueda, código inexistente) o <c>null</c> para limpiar el aviso anterior (se emite
    /// al agregar un artículo). Lo muestra el anfitrión donde corresponda.
    /// </summary>
    public event EventHandler<string?>? MensajeEmitido;

    public bool ExigirAlMenosUnRenglon { get; }

    // ------------------------------------------------------------------ //
    // Renglones                                                           //
    // ------------------------------------------------------------------ //

    public ObservableCollection<RenglonEditableFila> Renglones { get; } = new();

    [ObservableProperty]
    private RenglonEditableFila? _renglonSeleccionado;

    public bool SinRenglones => Renglones.Count == 0;

    public string TextoQuitarRenglon => PuedeQuitarRenglon() ? _textoQuitar : _textoUltimoRenglon;

    /// <summary>Suma de los subtotales de los renglones disponibles.</summary>
    public decimal Subtotal => Renglones.Sum(r => r.SubtotalQueSuma);

    public int CantidadNoDisponibles => Renglones.Count(r => r.EstaDadoDeBaja);

    public bool HayNoDisponibles => CantidadNoDisponibles > 0;

    public string TextoNoDisponibles => CantidadNoDisponibles == 1
        ? "1 artículo dado de baja no suma al total. Quítelo o déjelo: no se vende ni se imprime."
        : $"{CantidadNoDisponibles} artículos dados de baja no suman al total. Quítelos o déjelos: no se venden ni se imprimen.";

    /// <summary>Reemplaza los renglones por los iniciales (al abrir el editor con algo ya cargado).</summary>
    public void CargarRenglones(IEnumerable<RenglonEditableFila> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);

        foreach (var existente in Renglones)
        {
            existente.PropertyChanged -= AlCambiarRenglon;
        }

        Renglones.Clear();
        RenglonSeleccionado = null;

        foreach (var fila in filas)
        {
            AgregarFila(fila);
        }
    }

    /// <summary>Pone el precio unitario de los renglones cuyo artículo figura en <paramref name="preciosPorArticulo"/> (los demás no cambian).</summary>
    public void ActualizarPrecios(IReadOnlyDictionary<int, decimal> preciosPorArticulo)
    {
        ArgumentNullException.ThrowIfNull(preciosPorArticulo);

        foreach (var renglon in Renglones)
        {
            if (preciosPorArticulo.TryGetValue(renglon.IdArticulo, out var precio))
            {
                renglon.PrecioUnitario = precio;
            }
        }
    }

    /// <summary>Agrega el artículo con cantidad 1; si ya está, suma 1 a su cantidad en lugar de repetir el renglón.</summary>
    [RelayCommand]
    private void AgregarArticulo(ArticuloVentaDto? articulo)
    {
        if (articulo is null)
        {
            return;
        }

        MensajeEmitido?.Invoke(this, null);

        var existente = Renglones.FirstOrDefault(r => r.IdArticulo == articulo.IdArticulo);
        if (existente is not null)
        {
            existente.Cantidad = Math.Min(existente.Cantidad + 1, CantidadMaxima);
            RenglonSeleccionado = existente;
        }
        else
        {
            var renglon = RenglonEditableFila.Desde(articulo);
            AgregarFila(renglon);
            RenglonSeleccionado = renglon;
        }

        TextoBusqueda = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(PuedeQuitarRenglon))]
    private void QuitarRenglon(RenglonEditableFila? renglon)
    {
        if (renglon is null || !PuedeQuitarRenglon())
        {
            return;
        }

        renglon.PropertyChanged -= AlCambiarRenglon;
        Renglones.Remove(renglon);
    }

    private bool PuedeQuitarRenglon()
    {
        return !ExigirAlMenosUnRenglon || Renglones.Count > 1;
    }

    private void AgregarFila(RenglonEditableFila fila)
    {
        fila.PropertyChanged += AlCambiarRenglon;
        Renglones.Add(fila);
    }

    private void AlCambiarRenglon(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RenglonEditableFila.Cantidad) or nameof(RenglonEditableFila.PrecioUnitario))
        {
            OnPropertyChanged(nameof(Subtotal));
            ContenidoCambiado?.Invoke(this, EventArgs.Empty);
        }
    }

    private void AlCambiarRenglones(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SinRenglones));
        OnPropertyChanged(nameof(CantidadNoDisponibles));
        OnPropertyChanged(nameof(HayNoDisponibles));
        OnPropertyChanged(nameof(TextoNoDisponibles));
        OnPropertyChanged(nameof(TextoQuitarRenglon));
        QuitarRenglonCommand.NotifyCanExecuteChanged();
        ContenidoCambiado?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------ //
    // Buscador de artículos                                               //
    // ------------------------------------------------------------------ //

    public ObservableCollection<ArticuloVentaDto> ResultadosBusqueda { get; } = new();

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private ArticuloVentaDto? _resultadoSeleccionado;

    [ObservableProperty]
    private bool _mostrarResultados;

    [ObservableProperty]
    private bool _sinResultados;

    partial void OnTextoBusquedaChanged(string value)
    {
        var texto = value.Trim();

        if (texto.Length < LongitudMinimaBusqueda)
        {
            // Una carga vacía reemplaza a la búsqueda en curso, cuyos resultados ya no corresponden al texto.
            _ = _acceso.EjecutarAsync(
                _ => Task.FromResult<IReadOnlyList<ArticuloVentaDto>>(Array.Empty<ArticuloVentaDto>()),
                _ => { },
                _ => { });
            OcultarResultados();
            return;
        }

        _ = _acceso.EjecutarAsync(
            token => Task.Run(() => BuscarSugerenciasAsync(texto, token), token),
            MostrarResultadosBusqueda,
            ex =>
            {
                _logger.LogError(ex, "Error al buscar artículos para el editor de renglones");
                MensajeEmitido?.Invoke(this, "No se pudo buscar en el catálogo de artículos.");
            },
            EsperaBusqueda);
    }

    private async Task<IReadOnlyList<ArticuloVentaDto>> BuscarSugerenciasAsync(string texto, CancellationToken token)
    {
        var lista = new List<ArticuloVentaDto>();

        // Si son dígitos, primero el código de barras exacto (el lector escribe el código completo)
        if (texto.All(char.IsDigit))
        {
            var porCodigo = await _ventaService.BuscarPorCodigoBarrasAsync(texto, token);
            if (porCodigo is not null)
            {
                lista.Add(porCodigo);
            }
        }

        var coincidencias = await _ventaService.BuscarPorTextoAsync(texto, 10, token);
        foreach (var articulo in coincidencias)
        {
            if (lista.All(a => a.IdArticulo != articulo.IdArticulo))
            {
                lista.Add(articulo);
            }
        }

        return lista;
    }

    private void MostrarResultadosBusqueda(IReadOnlyList<ArticuloVentaDto> resultados)
    {
        ResultadosBusqueda.Clear();
        foreach (var articulo in resultados)
        {
            ResultadosBusqueda.Add(articulo);
        }

        ResultadoSeleccionado = ResultadosBusqueda.FirstOrDefault();
        SinResultados = ResultadosBusqueda.Count == 0;
        MostrarResultados = true;
    }

    private void OcultarResultados()
    {
        ResultadosBusqueda.Clear();
        ResultadoSeleccionado = null;
        SinResultados = false;
        MostrarResultados = false;
    }

    /// <summary>
    /// Enter en el buscador: agrega el resultado resaltado. Si todavía no hay resultados (el lector termina cada código
    /// con Enter antes de que venza la espera) resuelve el texto como el POS: código de barras o mejor coincidencia.
    /// </summary>
    [RelayCommand]
    private async Task AgregarResultadoSeleccionadoAsync()
    {
        if (MostrarResultados && ResultadoSeleccionado is { } seleccionado)
        {
            AgregarArticulo(seleccionado);
            return;
        }

        var entrada = TextoBusqueda.Trim();
        if (entrada.Length == 0)
        {
            return;
        }

        // Limpiar el buscador reemplaza la búsqueda predictiva en curso: la consulta del artículo espera su turno.
        TextoBusqueda = string.Empty;

        try
        {
            var (completado, articulo) = await _acceso.EjecutarEnFilaAsync(token => _ventaService.BuscarArticuloParaVentaAsync(entrada, token));
            if (!completado)
            {
                return;
            }

            if (articulo is null)
            {
                MensajeEmitido?.Invoke(this, $"No se encontró ningún artículo que coincida con «{entrada}».");
                return;
            }

            AgregarArticulo(articulo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar el artículo ingresado en el editor de renglones: {Entrada}", entrada);
            MensajeEmitido?.Invoke(this, "No se pudo buscar en el catálogo de artículos.");
        }
    }

    [RelayCommand]
    private void MoverSeleccionBusquedaAbajo()
    {
        MoverSeleccionBusqueda(1);
    }

    [RelayCommand]
    private void MoverSeleccionBusquedaArriba()
    {
        MoverSeleccionBusqueda(-1);
    }

    /// <summary>Esc: cierra el desplegable; si ya estaba cerrado, borra el texto.</summary>
    [RelayCommand]
    private void CerrarBusqueda()
    {
        if (MostrarResultados)
        {
            MostrarResultados = false;
        }
        else
        {
            TextoBusqueda = string.Empty;
        }
    }

    private void MoverSeleccionBusqueda(int paso)
    {
        if (ResultadosBusqueda.Count == 0)
        {
            return;
        }

        var indice = ResultadoSeleccionado is null ? -1 : ResultadosBusqueda.IndexOf(ResultadoSeleccionado);
        ResultadoSeleccionado = ResultadosBusqueda[Math.Clamp(indice + paso, 0, ResultadosBusqueda.Count - 1)];
    }
}
