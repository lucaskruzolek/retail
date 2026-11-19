using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.ViewModels.Ventas;
using Retail.Application.DTOs.Listas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Listas;

/// <summary>
/// Diálogo "Guardar como → Lista" del Punto de venta (L-06, L-16). Sin lista vinculada pide solo el nombre. Si el ticket
/// vino de una lista ofrece dos opciones: "Actualizar «nombre»" (reemplaza los renglones de esa lista, con control de
/// concurrencia sobre la <c>FechaModificacion</c> leída al cargarla) o "Guardar como lista nueva". Los errores de dominio
/// (nombre repetido, lista modificada en otra terminal) se muestran dentro del diálogo, que sigue abierto.
/// </summary>
public partial class GuardarListaViewModel : ObservableObject
{
    private readonly int _idUsuario;
    private readonly ListaCargada? _vinculada;
    private readonly IReadOnlyList<RenglonListaEntradaDto> _renglones;
    private readonly IListaService _listaService;
    private readonly CargaSerializada _acceso;
    private readonly ILogger _logger;

    private bool _nombreTocado;

    /// <param name="idUsuario">Usuario de la sesión, que figura como creador de una lista nueva.</param>
    /// <param name="renglones">Artículos y cantidades del ticket.</param>
    /// <param name="vinculada">Lista de la que viene el ticket, o <c>null</c> si es un ticket armado a mano.</param>
    /// <param name="acceso">Fila de accesos a datos del POS, para no usar su DbContext en paralelo (H-19).</param>
    public GuardarListaViewModel(
        int idUsuario,
        IReadOnlyList<RenglonListaEntradaDto> renglones,
        ListaCargada? vinculada,
        IListaService listaService,
        CargaSerializada acceso,
        ILogger logger)
    {
        _idUsuario = idUsuario;
        _renglones = renglones ?? throw new ArgumentNullException(nameof(renglones));
        _vinculada = vinculada;
        _listaService = listaService ?? throw new ArgumentNullException(nameof(listaService));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Con una lista vinculada, "Actualizar" es la opción por defecto (L-16): es lo que se hace casi siempre.
        _actualizarVinculada = vinculada is not null;
    }

    /// <summary>Límite del dominio, publicado para que la vista lo tome con <c>x:Static</c>.</summary>
    public static int LongitudMaximaNombre => ListaArticulos.LongitudMaximaNombre;

    public bool TieneListaVinculada => _vinculada is not null;

    public string TextoActualizar => $"Actualizar «{_vinculada?.Nombre}»";

    public string TextoCantidadArticulos => _renglones.Count == 1 ? "1 artículo" : $"{_renglones.Count} artículos";

    /// <summary>Se elige "Actualizar «nombre»": reemplaza los renglones de la lista vinculada y no pide nombre.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyPropertyChangedFor(nameof(GuardarComoNueva))]
    [NotifyPropertyChangedFor(nameof(PideNombre))]
    [NotifyPropertyChangedFor(nameof(TieneErrorNombre))]
    private bool _actualizarVinculada;

    /// <summary>Opción complementaria de <see cref="ActualizarVinculada"/>, para el segundo botón de opción.</summary>
    public bool GuardarComoNueva
    {
        get => !ActualizarVinculada;
        set => ActualizarVinculada = !value;
    }

    /// <summary>El nombre solo se pide para una lista nueva.</summary>
    public bool PideNombre => !ActualizarVinculada;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyPropertyChangedFor(nameof(TieneErrorNombre))]
    private string _nombre = string.Empty;

    /// <summary>El nombre es obligatorio: el aviso aparece recién cuando el usuario lo tocó, no al abrir el diálogo.</summary>
    public bool TieneErrorNombre => PideNombre && _nombreTocado && string.IsNullOrWhiteSpace(Nombre);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyPropertyChangedFor(nameof(NoEstaOcupado))]
    private bool _isBusy;

    public bool NoEstaOcupado => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>La lista quedó guardada: el diálogo se cierra y el POS la deja vinculada al ticket.</summary>
    public bool Guardada { get; private set; }

    /// <summary>Lista creada o actualizada; <c>null</c> mientras no se guardó.</summary>
    public ListaDto? ListaGuardada { get; private set; }

    /// <summary>Lo levanta el ViewModel cuando terminó de guardar, para que la vista cierre el diálogo.</summary>
    public event EventHandler? CierreSolicitado;

    partial void OnNombreChanged(string value)
    {
        _nombreTocado = true;
        MensajeError = null;
    }

    partial void OnActualizarVinculadaChanged(bool value)
    {
        MensajeError = null;
    }

    private bool PuedeGuardar()
    {
        if (IsBusy || Guardada || _renglones.Count == 0)
        {
            return false;
        }

        return ActualizarVinculada || !string.IsNullOrWhiteSpace(Nombre);
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        IsBusy = true;
        MensajeError = null;

        try
        {
            ListaDto? guardada;

            if (ActualizarVinculada && _vinculada is { } vinculada)
            {
                var dto = new ActualizarListaDto
                {
                    Nombre = vinculada.Nombre,
                    Renglones = _renglones,
                    FechaModificacionLeida = vinculada.FechaModificacion
                };
                var (completado, resultado) = await _acceso.EjecutarEnFilaAsync(
                    token => _listaService.ActualizarAsync(vinculada.Id, dto, token));
                guardada = completado ? resultado : null;
            }
            else
            {
                var dto = new CrearListaDto
                {
                    IdUsuario = _idUsuario,
                    Nombre = Nombre.Trim(),
                    Renglones = _renglones
                };
                var (completado, resultado) = await _acceso.EjecutarEnFilaAsync(
                    token => _listaService.CrearAsync(dto, token));
                guardada = completado ? resultado : null;
            }

            if (guardada is not null)
            {
                ListaGuardada = guardada;
                Guardada = true;
                CierreSolicitado?.Invoke(this, EventArgs.Empty);
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
            // Regla de negocio (nombre repetido, lista modificada en otra terminal): información para el cajero, no una falla.
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar la lista de artículos desde el Punto de venta");
            MensajeError = "No se pudo guardar la lista. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
