using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Converters;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Comun;
using Retail.Application.DTOs.Backups;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Backups;

/// <summary>
/// ViewModel de la pantalla de respaldos (RF-22, IU-10): historial de copias, copia manual y restauración. El
/// Gerente opera con su contraseña; el Administrador también, pero restaura solo con la autorización de un Gerente
/// (D-30, D-32). La autoridad real es de los servicios de Aplicación.
/// <para>
/// Los filtros de columna de las dos grillas se aplican acá, sobre las listas ya cargadas: ambos historiales llegan completos y
/// sin paginar por diseño del servicio (D-34; las copias se purgan y las restauraciones son excepcionales), así que no hay
/// una tabla creciente que filtrar en SQL como en el listado de pedidos.
/// </para>
/// </summary>
public partial class BackupViewModel : ObservableObject, IDisposable
{
    // Serializa las cargas del historial sobre el DbContext de esta pantalla y las cancela al salir de ella (H-19).
    private readonly CargaSerializada _cargaHistorial = new();

    private readonly IBackupService _backupService;
    private readonly IRestauracionService _restauracionService;
    private readonly IBackupDialogService _dialogService;
    private readonly ICurrentUserSession _session;
    private readonly IReinicioAplicacion _reinicio;
    private readonly ILogger<BackupViewModel> _logger;

    // Los historiales completos tal como llegaron; las colecciones de las grillas son lo que pasa los filtros
    private List<RegistroBackupFila> _todasLasCopias = [];
    private List<RegistroRestauracionFila> _todasLasRestauraciones = [];

    public BackupViewModel(
        IBackupService backupService,
        IRestauracionService restauracionService,
        IBackupDialogService dialogService,
        ICurrentUserSession session,
        IReinicioAplicacion reinicio,
        ILogger<BackupViewModel>? logger = null,
        TimeProvider? reloj = null)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _restauracionService = restauracionService ?? throw new ArgumentNullException(nameof(restauracionService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _reinicio = reinicio ?? throw new ArgumentNullException(nameof(reinicio));
        _logger = logger ?? NullLogger<BackupViewModel>.Instance;

        FiltroCopiaFecha = new FiltroFechaColumnaViewModel("Fecha", reloj);
        FiltroRestauracionFecha = new FiltroFechaColumnaViewModel("Fecha", reloj);
        FiltroCopiaTipo.CargarOpciones(OpcionesDeEnum<TipoBackupEnum>());
        FiltroCopiaEstado.CargarOpciones(OpcionesDeEnum<EstadoBackupEnum>());
        FiltroRestauracionOrigen.CargarOpciones(OpcionesDeEnum<OrigenRestauracionEnum>());

        foreach (var filtro in new[] { FiltroCopiaTipo, FiltroCopiaEstado, FiltroRestauracionOrigen, FiltroCopiaOperador.Filtro, FiltroRestauracionEjecutadaPor.Filtro, FiltroRestauracionAutorizadaPor.Filtro })
        {
            filtro.Aplicado += AlAplicarFiltro;
        }

        FiltroCopiaFecha.Aplicado += AlAplicarFiltro;
        FiltroRestauracionFecha.Aplicado += AlAplicarFiltro;
    }

    public ObservableCollection<RegistroBackupFila> Copias { get; } = new();

    public ObservableCollection<RegistroRestauracionFila> Restauraciones { get; } = new();

    // ------------------------------------------------------------------ //
    // Filtros de columna                                                  //
    // ------------------------------------------------------------------ //

    public FiltroFechaColumnaViewModel FiltroCopiaFecha { get; }

    public FiltroColumnaViewModel FiltroCopiaTipo { get; } = new("Tipo");

    public FiltroPorValoresViewModel FiltroCopiaOperador { get; } = new("Operador");

    public FiltroColumnaViewModel FiltroCopiaEstado { get; } = new("Estado");

    public FiltroFechaColumnaViewModel FiltroRestauracionFecha { get; }

    public FiltroColumnaViewModel FiltroRestauracionOrigen { get; } = new("Origen");

    public FiltroPorValoresViewModel FiltroRestauracionEjecutadaPor { get; } = new("Ejecutada por");

    public FiltroPorValoresViewModel FiltroRestauracionAutorizadaPor { get; } = new("Autorizada por");

    /// <summary>Hay algún filtro aplicado en la grilla que se está mostrando.</summary>
    public bool HayFiltrosActivos => MostrandoCopias
        ? FiltroCopiaFecha.HayFiltroActivo || FiltroCopiaTipo.HayFiltroActivo || FiltroCopiaOperador.Filtro.HayFiltroActivo || FiltroCopiaEstado.HayFiltroActivo
        : FiltroRestauracionFecha.HayFiltroActivo || FiltroRestauracionOrigen.HayFiltroActivo || FiltroRestauracionEjecutadaPor.Filtro.HayFiltroActivo || FiltroRestauracionAutorizadaPor.Filtro.HayFiltroActivo;

    [RelayCommand(CanExecute = nameof(HayFiltrosActivos))]
    public void LimpiarFiltros()
    {
        if (MostrandoCopias)
        {
            FiltroCopiaFecha.QuitarFiltro();
            FiltroCopiaTipo.QuitarFiltro();
            FiltroCopiaOperador.Filtro.QuitarFiltro();
            FiltroCopiaEstado.QuitarFiltro();
        }
        else
        {
            FiltroRestauracionFecha.QuitarFiltro();
            FiltroRestauracionOrigen.QuitarFiltro();
            FiltroRestauracionEjecutadaPor.Filtro.QuitarFiltro();
            FiltroRestauracionAutorizadaPor.Filtro.QuitarFiltro();
        }

        AplicarFiltros();
    }

    private void AlAplicarFiltro(object? sender, EventArgs e)
    {
        AplicarFiltros();
    }

    /// <summary>Vuelve a armar las dos grillas con lo que pasa los filtros aplicados, sin tocar la base.</summary>
    private void AplicarFiltros()
    {
        var copias = _todasLasCopias.Where(c =>
            FiltroCopiaFecha.Contiene(c.FechaLocal) &&
            Pasa(FiltroCopiaTipo, (int)c.Tipo) &&
            FiltroCopiaOperador.Coincide(c.Operador) &&
            Pasa(FiltroCopiaEstado, (int)c.Estado)).ToList();

        Copias.Clear();
        foreach (var copia in copias)
        {
            Copias.Add(copia);
        }

        if (CopiaSeleccionada is not null && !copias.Contains(CopiaSeleccionada))
        {
            CopiaSeleccionada = null;
        }

        var restauraciones = _todasLasRestauraciones.Where(r =>
            FiltroRestauracionFecha.Contiene(r.FechaLocal) &&
            Pasa(FiltroRestauracionOrigen, (int)r.Dto.Origen) &&
            FiltroRestauracionEjecutadaPor.Coincide(r.EjecutadaPor) &&
            FiltroRestauracionAutorizadaPor.Coincide(r.AutorizadaPor)).ToList();

        Restauraciones.Clear();
        foreach (var restauracion in restauraciones)
        {
            Restauraciones.Add(restauracion);
        }

        OnPropertyChanged(nameof(NoHayRestauraciones));
        OnPropertyChanged(nameof(NoHayCopias));
        OnPropertyChanged(nameof(MensajeSinRestauraciones));
        OnPropertyChanged(nameof(MensajeSinCopias));
        OnPropertyChanged(nameof(HayFiltrosActivos));
        LimpiarFiltrosCommand.NotifyCanExecuteChanged();
    }

    // Sin casillas aplicadas no se filtra
    private static bool Pasa(FiltroColumnaViewModel filtro, int id)
    {
        return !filtro.HayFiltroActivo || filtro.IdsAplicados.Contains(id);
    }

    private static IEnumerable<OpcionFiltroColumna> OpcionesDeEnum<T>() where T : struct, Enum
    {
        return Enum.GetValues<T>().Select(valor => new OpcionFiltroColumna(Convert.ToInt32(valor, CultureInfo.InvariantCulture), EnumATextoConverter.ATexto(valor)));
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestaurarCopiaCommand))]
    private RegistroBackupFila? _copiaSeleccionada;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearBackupManualCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestaurarCopiaCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneUltimaRestauracion))]
    private string? _ultimaRestauracionTexto;

    /// <summary>
    /// Pestaña visible de la grilla (D-34): las copias o el historial de restauraciones. <see cref="MostrandoRestauraciones"/>
    /// es su complemento, para que los dos selectores se enlacen sin convertidores.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrandoRestauraciones))]
    [NotifyPropertyChangedFor(nameof(HayFiltrosActivos))]
    [NotifyCanExecuteChangedFor(nameof(LimpiarFiltrosCommand))]
    private bool _mostrandoCopias = true;

    public bool MostrandoRestauraciones
    {
        get => !MostrandoCopias;
        set => MostrandoCopias = !value;
    }

    public bool NoHayRestauraciones => Restauraciones.Count == 0;

    public bool NoHayCopias => Copias.Count == 0;

    /// <summary>Distingue "nunca se restauró" de "los filtros no dejan ninguna".</summary>
    public string MensajeSinRestauraciones => _todasLasRestauraciones.Count == 0
        ? "No se registraron restauraciones"
        : "Ninguna restauración coincide con los filtros";

    public string MensajeSinCopias => _todasLasCopias.Count == 0
        ? "No hay copias registradas"
        : "Ninguna copia coincide con los filtros";

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public bool TieneUltimaRestauracion => !string.IsNullOrWhiteSpace(UltimaRestauracionTexto);

    /// <summary>
    /// Gerente y Administrador ven los botones de operar (D-30, D-32); a cualquier otro rol se le oculta lo que el
    /// servicio le rechazaría.
    /// </summary>
    public bool PuedeOperar => _session.EsGerente || _session.EsAdministrador;

    public bool EsSoloLectura => !PuedeOperar;

    private int IdActor => _session.IdUsuario ?? 0;

    [RelayCommand]
    public async Task CargarAsync()
    {
        IsBusy = true;
        MensajeError = null;
        MensajeEstado = "Cargando historial de copias...";

        await _cargaHistorial.EjecutarAsync(
            token => Task.Run(
                async () =>
                {
                    // Secuencial a propósito: las consultas comparten el DbContext de la pantalla.
                    var copias = await _backupService.ListarAsync(IdActor, token);
                    var ultima = await _restauracionService.ObtenerUltimaRestauracionAsync(IdActor, token);
                    var restauraciones = await _restauracionService.ListarRestauracionesAsync(IdActor, token);
                    return (Copias: copias, Ultima: ultima, Restauraciones: restauraciones);
                },
                token),
            resultado =>
            {
                _todasLasCopias = resultado.Copias.Select(dto => new RegistroBackupFila(dto)).ToList();
                _todasLasRestauraciones = resultado.Restauraciones.Select(dto => new RegistroRestauracionFila(dto)).ToList();

                FiltroCopiaOperador.Actualizar(_todasLasCopias.Select(c => c.Operador));
                FiltroRestauracionEjecutadaPor.Actualizar(_todasLasRestauraciones.Select(r => r.EjecutadaPor));
                FiltroRestauracionAutorizadaPor.Actualizar(_todasLasRestauraciones.Select(r => r.AutorizadaPor));
                AplicarFiltros();

                UltimaRestauracionTexto = resultado.Ultima is null ? null : DescribirRestauracion(resultado.Ultima);
                MensajeEstado = string.Empty;
            },
            ex =>
            {
                _logger.LogError(ex, "Error al obtener el historial de copias: {Mensaje}", ex.Message);
                MensajeError = $"Error al obtener el historial: {ex.Message}";
                _dialogService.MostrarError("Error de Carga", ex.Message);
            });

        IsBusy = _cargaHistorial.EnCurso;
    }

    [RelayCommand(CanExecute = nameof(PuedeCrearBackup))]
    public async Task CrearBackupManualAsync()
    {
        if (!PuedeOperar)
        {
            _dialogService.MostrarError("Operación no permitida", "Solo un Gerente o un Administrador pueden crear copias de seguridad.");
            return;
        }

        var idActor = IdActor;
        ResultadoBackupDto? creada = null;
        var recargar = false;

        IsBusy = true;
        MensajeError = null;

        try
        {
            var vm = ConfirmarCredencialViewModel.ParaBackupManual(async (password, progreso) =>
            {
                // Ley 4: la copia (E/S de disco y de SQL Server) nunca corre en el hilo de la UI.
                creada = await Task.Run(() => _backupService.CrearBackupManualAsync(idActor, password, progreso));
            });

            var resultado = _dialogService.SolicitarCredencial(vm);

            // Una copia fallida también deja su registro en el historial: se recarga salvo que el Gerente haya cancelado.
            recargar = resultado.Completada || resultado.Error is not null;

            if (resultado.Error is not null)
            {
                InformarFallo("No se pudo crear la copia", resultado.Error);
            }
            else if (resultado.Completada && creada is not null)
            {
                _dialogService.MostrarInformacion(
                    "Copia creada",
                    $"La copia se creó y se verificó correctamente.\n\nArchivo: {creada.RutaArchivo}\nTamaño: {RegistroBackupFila.FormatearTamano(creada.TamanoBytes)}");
            }
        }
        finally
        {
            IsBusy = false;
        }

        if (recargar)
        {
            // La recarga limpia el mensaje de error: el de esta operación se vuelve a mostrar.
            var errorDeLaOperacion = MensajeError;
            await CargarAsync();
            MensajeError = errorDeLaOperacion;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeRestaurar))]
    public async Task RestaurarCopiaAsync()
    {
        var copia = CopiaSeleccionada;
        if (!PuedeOperar || copia is null || !copia.EsRestaurable)
        {
            _dialogService.MostrarError(
                "Restauración no disponible",
                "Seleccione una copia verificada, cuyo archivo se conserve, y use una cuenta de Gerente o de Administrador.");
            return;
        }

        var idActor = IdActor;
        ResultadoRestauracionDto? restaurada = null;
        var recargar = false;

        IsBusy = true;
        MensajeError = null;

        try
        {
            var fechaCopia = copia.FechaLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

            // El Gerente se reautentica con su propia contraseña; el Administrador necesita la autorización de un Gerente.
            var vm = _session.EsGerente
                ? ConfirmarCredencialViewModel.ParaRestauracion(
                    async (password, progreso) =>
                    {
                        restaurada = await Task.Run(() =>
                            _restauracionService.RestaurarDesdeHistorialAsync(idActor, password, copia.Referencia, progreso));
                    },
                    fechaCopia,
                    ResultadoRestauracionDto.AvisoConciliacionArca)
                : ConfirmarCredencialViewModel.ParaRestauracionAutorizada(
                    async (passwordActor, credencial, progreso) =>
                    {
                        // Los rechazos quedan auditados en la base (AUDITORIA_ACCESOS) por el servicio de aplicación.
                        restaurada = await Task.Run(() =>
                            _restauracionService.RestaurarConAutorizacionDeGerenteAsync(
                                idActor, passwordActor, credencial, copia.Referencia, progreso));
                    },
                    fechaCopia,
                    ResultadoRestauracionDto.AvisoConciliacionArca);

            var resultado = _dialogService.SolicitarCredencial(vm);

            if (resultado.Error is CuentaBloqueadaException bloqueo)
            {
                // Opción C (D-31): la cuenta quedó bloqueada; reiniciar cierra de verdad la sesión abierta.
                ReiniciarPorCuentaBloqueada(idActor, bloqueo);
                return;
            }

            if (resultado.Error is not null)
            {
                // La base temporal se descartó: la base vigente no se tocó.
                InformarFallo("No se pudo restaurar la copia. La base de datos actual no fue modificada", resultado.Error);
                recargar = true;
            }
            else if (resultado.Completada && restaurada is not null)
            {
                // Con la base ya reemplazada no se recarga nada: la pantalla queda ocupada hasta el reinicio.
                ReiniciarTrasRestaurar(restaurada);
                return;
            }
        }
        catch
        {
            IsBusy = false;
            throw;
        }

        IsBusy = false;

        if (recargar)
        {
            // La recarga limpia el mensaje de error: el de esta operación se vuelve a mostrar.
            var errorDeLaOperacion = MensajeError;
            await CargarAsync();
            MensajeError = errorDeLaOperacion;
        }
    }

    public void Dispose()
    {
        _cargaHistorial.Dispose();
        GC.SuppressFinalize(this);
    }

    private bool PuedeCrearBackup() => PuedeOperar && !IsBusy;

    private bool PuedeRestaurar() => PuedeOperar && !IsBusy && CopiaSeleccionada?.EsRestaurable == true;

    /// <summary>
    /// Después de restaurar, las conexiones y las pantallas abiertas apuntan a la base reemplazada: se muestra el
    /// resultado completo (incluida la conciliación con ARCA) y se reinicia la aplicación.
    /// </summary>
    private void ReiniciarTrasRestaurar(ResultadoRestauracionDto resultado)
    {
        var mensaje = string.Join("\n\n", resultado.Advertencias) +
            "\n\nLa aplicación se reiniciará ahora para trabajar con la base restaurada.";
        _dialogService.MostrarInformacion("Restauración completada", mensaje);

        try
        {
            _reinicio.Reiniciar();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo reiniciar la aplicación después de restaurar: {Mensaje}", ex.Message);
            _dialogService.MostrarError(
                "Reinicio manual necesario",
                "La restauración se completó, pero no se pudo reiniciar la aplicación. Ciérrela y vuelva a abrirla antes de seguir trabajando.");
        }
    }

    /// <summary>
    /// La cuenta del Administrador se bloqueó por autorizaciones fallidas: se informa y se reinicia la aplicación, el único
    /// modo de cerrar la sesión abierta sin tocar el Shell. No se recarga nada: la pantalla queda ocupada hasta el reinicio.
    /// </summary>
    private void ReiniciarPorCuentaBloqueada(int idActor, CuentaBloqueadaException bloqueo)
    {
        _logger.LogWarning("La cuenta del actor {IdActor} quedó bloqueada por autorizaciones de restauración fallidas", idActor);
        _dialogService.MostrarError("Cuenta bloqueada", bloqueo.Message + " La aplicación se reiniciará.");

        try
        {
            _reinicio.Reiniciar();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo reiniciar la aplicación tras el bloqueo de la cuenta: {Mensaje}", ex.Message);
            _dialogService.MostrarError(
                "Reinicio manual necesario",
                "No se pudo reiniciar la aplicación. Ciérrela y vuelva a abrirla antes de seguir trabajando.");
        }
    }

    private void InformarFallo(string titulo, Exception ex)
    {
        _logger.LogError(ex, "{Titulo}: {Mensaje}", titulo, ex.Message);
        MensajeError = ex.Message;
        _dialogService.MostrarError(titulo, ex.Message);
    }

    private static string DescribirRestauracion(RegistroRestauracionDto restauracion)
    {
        var fecha = DateTime.SpecifyKind(restauracion.Fecha, DateTimeKind.Utc).ToLocalTime();
        var origen = EnumATextoConverter.ATexto(restauracion.Origen);
        var archivo = Path.GetFileName(restauracion.RutaArchivo);

        // Con operador, la ejecutó un Administrador y NombreUsuario es el Gerente que la autorizó (D-30).
        var responsables = restauracion.NombreOperador is null
            ? $"por {restauracion.NombreUsuario}"
            : $"ejecutada por {restauracion.NombreOperador}, autorizada por {restauracion.NombreUsuario}";

        return $"Última restauración: {fecha:dd/MM/yyyy HH:mm} {responsables} ({origen}), copia {archivo}.";
    }
}
