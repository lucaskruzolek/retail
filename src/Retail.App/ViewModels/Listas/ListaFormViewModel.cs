using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Comun;
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
    private readonly ListaDto? _lista;
    private readonly IListaService _listaService;
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
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Editor = new EditorRenglonesViewModel(
            ventaService ?? throw new ArgumentNullException(nameof(ventaService)),
            acceso,
            logger,
            exigirAlMenosUnRenglon: true,
            textoQuitar: "Quitar el artículo de la lista",
            textoUltimoRenglon: "La lista debe tener al menos un artículo");
        Editor.ContenidoCambiado += (_, _) => ActualizarTotales();
        Editor.MensajeEmitido += (_, mensaje) => MensajeError = mensaje;

        if (lista is not null)
        {
            _nombre = lista.Nombre;
            Editor.CargarRenglones(lista.Renglones.Select(r => new RenglonEditableFila(
                r.IdArticulo,
                r.CodigoBarras,
                r.Descripcion,
                r.PrecioActual,
                r.Cantidad,
                r.EstaDisponible)));
        }

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

    /// <summary>Buscador, renglones y aviso de artículos dados de baja (componente compartido con el editor de presupuestos).</summary>
    public EditorRenglonesViewModel Editor { get; }

    /// <summary>Suma de los subtotales de los renglones disponibles, a los precios de hoy.</summary>
    public decimal TotalDeHoy => Editor.Subtotal;

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

    private void ActualizarTotales()
    {
        OnPropertyChanged(nameof(TotalDeHoy));
        GuardarCommand.NotifyCanExecuteChanged();
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
        return !IsBusy && !string.IsNullOrWhiteSpace(Nombre) && Editor.Renglones.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        var nombre = Nombre.Trim();
        var renglones = Editor.Renglones
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
