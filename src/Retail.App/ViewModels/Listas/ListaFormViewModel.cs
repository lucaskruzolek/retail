using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Listas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Listas;

/// <summary>
/// Editor de una lista de artículos (IU-14, L-18): sirve para "Nueva lista" y para "Editar". Pide el nombre y los
/// renglones (buscador de artículos vendibles, como el del POS, y cantidades), muestra el total a los precios de hoy
/// (informativo: el servidor es la autoridad, L-08) y guarda todo junto. Los errores de dominio (nombre repetido, lista
/// modificada en otra terminal) se muestran dentro del diálogo sin cerrarlo.
/// </summary>
public partial class ListaFormViewModel : ObservableObject
{
    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    private const int LongitudMinimaBusqueda = 2;
    private const int CantidadMaxima = 99999;

    private readonly ListaDto? _lista;
    private readonly IListaService _listaService;
    private readonly IVentaService _ventaService;
    private readonly ICurrentUserSession _sesion;
    private readonly CargaSerializada _acceso;
    private readonly ILogger _logger;

    private bool _nombreTocado;

    /// <param name="lista">Lista a editar tal como se leyó al abrir el editor, o <c>null</c> para crear una nueva.</param>
    /// <param name="acceso">Fila de accesos a datos de la pantalla que abre el editor, para no usar su DbContext en paralelo (H-19).</param>
    public ListaFormViewModel(
        ListaDto? lista,
        IListaService listaService,
        IVentaService ventaService,
        ICurrentUserSession sesion,
        CargaSerializada acceso,
        ILogger logger)
    {
        _lista = lista;
        _listaService = listaService ?? throw new ArgumentNullException(nameof(listaService));
        _ventaService = ventaService ?? throw new ArgumentNullException(nameof(ventaService));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (lista is not null)
        {
            _nombre = lista.Nombre;
            foreach (var renglon in lista.Renglones)
            {
                AgregarFila(RenglonListaFila.Desde(renglon));
            }
        }

        Renglones.CollectionChanged += AlCambiarRenglones;
        ActualizarTotales();
    }

    // ------------------------------------------------------------------ //
    // Datos de la lista                                                   //
    // ------------------------------------------------------------------ //

    /// <summary>Límite del dominio, publicado para que la vista lo tome con <c>x:Static</c>.</summary>
    public static int LongitudMaximaNombre => ListaArticulos.LongitudMaximaNombre;

    public bool EsNueva => _lista is null;

    public string TituloVentana => EsNueva ? "Nueva lista" : "Editar lista";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyPropertyChangedFor(nameof(TieneErrorNombre))]
    private string _nombre = string.Empty;

    /// <summary>El nombre es obligatorio: el aviso aparece recién cuando el usuario lo tocó, no al abrir una lista nueva.</summary>
    public bool TieneErrorNombre => _nombreTocado && string.IsNullOrWhiteSpace(Nombre);

    public ObservableCollection<RenglonListaFila> Renglones { get; } = new();

    [ObservableProperty]
    private RenglonListaFila? _renglonSeleccionado;

    public bool SinRenglones => Renglones.Count == 0;

    /// <summary>La última fila no se puede quitar: una lista tiene al menos un artículo.</summary>
    public string TextoQuitarRenglon => PuedeQuitarRenglon()
        ? "Quitar el artículo de la lista"
        : "La lista debe tener al menos un artículo";

    /// <summary>Suma de los subtotales de los renglones disponibles, a los precios de hoy.</summary>
    public decimal TotalDeHoy => Renglones.Sum(r => r.SubtotalQueSuma);

    public int CantidadNoDisponibles => Renglones.Count(r => r.EstaDadoDeBaja);

    public bool HayNoDisponibles => CantidadNoDisponibles > 0;

    public string TextoNoDisponibles => CantidadNoDisponibles == 1
        ? "1 artículo dado de baja no suma al total. Quítelo o déjelo: no se vende ni se imprime."
        : $"{CantidadNoDisponibles} artículos dados de baja no suman al total. Quítelos o déjelos: no se venden ni se imprimen.";

    // ------------------------------------------------------------------ //
    // Estado                                                              //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>La lista se guardó: el diálogo se cierra y la pestaña recarga.</summary>
    public bool Guardada { get; private set; }

    /// <summary>Pide al diálogo que se cierre (lo atiende su code-behind).</summary>
    public event EventHandler? CierreSolicitado;

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
                _logger.LogError(ex, "Error al buscar artículos para la lista");
                MensajeError = "No se pudo buscar en el catálogo de artículos.";
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
                MensajeError = $"No se encontró ningún artículo que coincida con «{entrada}».";
                return;
            }

            AgregarArticulo(articulo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar el artículo ingresado para la lista: {Entrada}", entrada);
            MensajeError = "No se pudo buscar en el catálogo de artículos.";
        }
    }

    /// <summary>Agrega el artículo con cantidad 1; si ya está en la lista, suma 1 a su cantidad en lugar de repetir el renglón.</summary>
    [RelayCommand]
    private void AgregarArticulo(ArticuloVentaDto? articulo)
    {
        if (articulo is null)
        {
            return;
        }

        MensajeError = null;

        var existente = Renglones.FirstOrDefault(r => r.IdArticulo == articulo.IdArticulo);
        if (existente is not null)
        {
            existente.Cantidad = Math.Min(existente.Cantidad + 1, CantidadMaxima);
            RenglonSeleccionado = existente;
        }
        else
        {
            var renglon = RenglonListaFila.Desde(articulo);
            AgregarFila(renglon);
            RenglonSeleccionado = renglon;
        }

        TextoBusqueda = string.Empty;
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

    // ------------------------------------------------------------------ //
    // Renglones                                                           //
    // ------------------------------------------------------------------ //

    [RelayCommand(CanExecute = nameof(PuedeQuitarRenglon))]
    private void QuitarRenglon(RenglonListaFila? renglon)
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
        return Renglones.Count > 1;
    }

    private void AgregarFila(RenglonListaFila fila)
    {
        fila.PropertyChanged += AlCambiarRenglon;
        Renglones.Add(fila);
    }

    private void AlCambiarRenglon(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RenglonListaFila.Cantidad))
        {
            OnPropertyChanged(nameof(TotalDeHoy));
        }
    }

    private void AlCambiarRenglones(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ActualizarTotales();
    }

    private void ActualizarTotales()
    {
        OnPropertyChanged(nameof(TotalDeHoy));
        OnPropertyChanged(nameof(SinRenglones));
        OnPropertyChanged(nameof(CantidadNoDisponibles));
        OnPropertyChanged(nameof(HayNoDisponibles));
        OnPropertyChanged(nameof(TextoNoDisponibles));
        OnPropertyChanged(nameof(TextoQuitarRenglon));
        GuardarCommand.NotifyCanExecuteChanged();
        QuitarRenglonCommand.NotifyCanExecuteChanged();
    }

    partial void OnNombreChanged(string value)
    {
        _nombreTocado = true;
    }

    // ------------------------------------------------------------------ //
    // Guardar                                                             //
    // ------------------------------------------------------------------ //

    private bool PuedeGuardar()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(Nombre) && Renglones.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        var nombre = Nombre.Trim();
        var renglones = Renglones
            .Select(r => new RenglonListaEntradaDto { IdArticulo = r.IdArticulo, Cantidad = r.Cantidad })
            .ToList();

        IsBusy = true;
        MensajeError = null;

        try
        {
            if (_lista is null)
            {
                if (_sesion.IdUsuario is not int idUsuario)
                {
                    MensajeError = "No hay una sesión activa. Inicie sesión nuevamente.";
                    return;
                }

                var dto = new CrearListaDto { IdUsuario = idUsuario, Nombre = nombre, Renglones = renglones };
                var (completado, _) = await _acceso.EjecutarEnFilaAsync(token => _listaService.CrearAsync(dto, token));
                Finalizar(completado);
            }
            else
            {
                var dto = new ActualizarListaDto
                {
                    Nombre = nombre,
                    Renglones = renglones,
                    FechaModificacionLeida = _lista.FechaModificacion
                };
                var (completado, _) = await _acceso.EjecutarEnFilaAsync(token => _listaService.ActualizarAsync(_lista.IdLista, dto, token));
                Finalizar(completado);
            }
        }
        catch (ValidationException ex)
        {
            MensajeError = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct());
        }
        catch (ConflictoDeConcurrenciaException ex)
        {
            MensajeError = ex.Message;
        }
        catch (DomainException ex)
        {
            // Regla de negocio (nombre repetido, lista modificada en otra terminal): información para el usuario, no una falla.
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar la lista de artículos");
            MensajeError = "No se pudo guardar la lista. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Finalizar(bool completado)
    {
        if (!completado)
        {
            return;
        }

        Guardada = true;
        CierreSolicitado?.Invoke(this, EventArgs.Empty);
    }
}
