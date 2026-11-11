using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Services;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Ajustes;

/// <summary>
/// Pantalla de Ajustes. Permite elegir el tema claro/oscuro, consultar la documentación y, solo el Gerente, la política
/// global de redondeo del precio de venta (D-38).
/// </summary>
public partial class AjustesViewModel : ObservableObject
{
    // Precio de ejemplo de la vista previa: el mismo que documenta RedondeoPrecioEnum.
    private const decimal PrecioEjemplo = 1663.40m;

    private static readonly CultureInfo CulturaArgentina = new("es-AR");

    private readonly IServicioTema _servicioTema;
    private readonly IPreferenciasUsuario _preferencias;
    private readonly IAyudaDialogService? _ayudaDialogService;
    private readonly IConfiguracionComercialService? _configuracionComercial;
    private readonly ICurrentUserSession? _session;
    private readonly ILogger<AjustesViewModel> _logger;

    public AjustesViewModel(
        IServicioTema servicioTema,
        IPreferenciasUsuario preferencias,
        IAyudaDialogService? ayudaDialogService = null,
        IConfiguracionComercialService? configuracionComercial = null,
        ICurrentUserSession? session = null,
        ILogger<AjustesViewModel>? logger = null)
    {
        _servicioTema = servicioTema ?? throw new ArgumentNullException(nameof(servicioTema));
        _preferencias = preferencias ?? throw new ArgumentNullException(nameof(preferencias));
        _ayudaDialogService = ayudaDialogService;
        _configuracionComercial = configuracionComercial;
        _session = session;
        _logger = logger ?? NullLogger<AjustesViewModel>.Instance;

        // Se asigna el campo (no la propiedad) para no volver a aplicar ni guardar el tema que ya está vigente.
        _temaOscuro = _preferencias.Tema == TemaVisual.Oscuro;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemaClaro))]
    private bool _temaOscuro;

    /// <summary>
    /// Espejo de <see cref="TemaOscuro"/> para la opción "Claro" del selector: las dos opciones son un par excluyente,
    /// cada una enlazada a su propia propiedad.
    /// </summary>
    public bool TemaClaro
    {
        get => !TemaOscuro;
        set => TemaOscuro = !value;
    }

    partial void OnTemaOscuroChanged(bool value)
    {
        var tema = value ? TemaVisual.Oscuro : TemaVisual.Claro;

        _preferencias.Tema = tema;
        _servicioTema.Aplicar(tema);
        _preferencias.Guardar();
    }

    [RelayCommand]
    private void AbrirManualPdf()
    {
        _ayudaDialogService?.AbrirManualPdf();
    }

    [RelayCommand]
    private void AbrirCentroAyuda()
    {
        _ayudaDialogService?.MostrarCentroAyuda();
    }

    // ------------------------------------------------------------------ Precios (D-38)

    /// <summary>La sección de precios se muestra solo al Gerente: el redondeo cambia los precios de todo el comercio.</summary>
    public bool MostrarPrecios => _configuracionComercial is not null && _session?.EsGerente == true;

    public IReadOnlyList<OpcionRedondeoPrecio> OpcionesRedondeo { get; } = OpcionRedondeoPrecio.Todas;

    /// <summary>Política elegida en el selector; se guarda recién con <see cref="AplicarRedondeoCommand"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EjemploRedondeo))]
    [NotifyPropertyChangedFor(nameof(HayCambioDeRedondeo))]
    [NotifyCanExecuteChangedFor(nameof(AplicarRedondeoCommand))]
    private OpcionRedondeoPrecio? _redondeoSeleccionado;

    /// <summary>Política guardada en la base, para saber si el selector tiene un cambio sin aplicar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayCambioDeRedondeo))]
    [NotifyCanExecuteChangedFor(nameof(AplicarRedondeoCommand))]
    private OpcionRedondeoPrecio? _redondeoGuardado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AplicarRedondeoCommand))]
    private bool _estaGuardandoRedondeo;

    /// <summary>Resultado de la última operación sobre el redondeo (confirmación o error), para mostrar bajo el selector.</summary>
    [ObservableProperty]
    private string? _mensajeRedondeo;

    [ObservableProperty]
    private bool _mensajeRedondeoEsError;

    public bool HayCambioDeRedondeo => RedondeoSeleccionado is not null && RedondeoSeleccionado != RedondeoGuardado;

    /// <summary>Vista previa de la política elegida sobre un precio de ejemplo: "$ 1.663,40 → $ 1.670".</summary>
    public string EjemploRedondeo
    {
        get
        {
            if (RedondeoSeleccionado is null)
            {
                return string.Empty;
            }

            var redondeado = ReglasMonetarias.RedondearPrecio(PrecioEjemplo, RedondeoSeleccionado.Valor, minimo: 0m);
            return $"Ejemplo: {PrecioEjemplo.ToString("$ #,##0.00", CulturaArgentina)} → {redondeado.ToString("$ #,##0", CulturaArgentina)}";
        }
    }

    /// <summary>Lee la política vigente. Solo si la sección está visible (Gerente).</summary>
    [RelayCommand]
    public async Task CargarAsync()
    {
        if (!MostrarPrecios || _configuracionComercial is null)
        {
            return;
        }

        try
        {
            var vigente = await _configuracionComercial.ObtenerRedondeoPrecioAsync();
            RedondeoGuardado = OpcionesRedondeo.First(o => o.Valor == vigente);
            RedondeoSeleccionado = RedondeoGuardado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo leer la política de redondeo de precios.");
            MostrarMensajeRedondeo("No se pudo leer la política de redondeo de precios.", esError: true);
        }
    }

    private bool PuedeAplicarRedondeo() => HayCambioDeRedondeo && !EstaGuardandoRedondeo;

    [RelayCommand(CanExecute = nameof(PuedeAplicarRedondeo))]
    private async Task AplicarRedondeoAsync()
    {
        if (_configuracionComercial is null || _session?.IdUsuario is not int idActor || RedondeoSeleccionado is not { } elegido)
        {
            return;
        }

        EstaGuardandoRedondeo = true;
        try
        {
            var anterior = RedondeoGuardado?.Valor;
            var nuevo = elegido.Valor;
            await _configuracionComercial.CambiarRedondeoPrecioAsync(idActor, nuevo);

            // Auditoría: cambia los precios que calculará todo el sistema a partir de ahora.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "El usuario {IdUsuario} cambió el redondeo de precios de {Anterior} a {Nuevo}.",
                    idActor,
                    anterior,
                    nuevo);
            }

            RedondeoGuardado = elegido;
            MostrarMensajeRedondeo(
                "Guardado. Rige para los próximos recálculos de precio (alta o edición de artículos, compras e importaciones); los precios actuales no cambian.",
                esError: false);
        }
        catch (OperacionNoAutorizadaException ex)
        {
            MostrarMensajeRedondeo(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo guardar la política de redondeo de precios.");
            MostrarMensajeRedondeo("No se pudo guardar la política de redondeo. Intente nuevamente.", esError: true);
        }
        finally
        {
            EstaGuardandoRedondeo = false;
        }
    }

    private void MostrarMensajeRedondeo(string mensaje, bool esError)
    {
        MensajeRedondeo = mensaje;
        MensajeRedondeoEsError = esError;
    }
}
