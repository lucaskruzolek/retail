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
using Retail.Application.DTOs.Backups;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Backups;

/// <summary>
/// ViewModel de la pantalla de respaldos (RF-22, IU-10): historial de copias, copia manual y restauración. El
/// Administrador ve el historial pero no puede operar (D-3); la autoridad real es de los servicios de Aplicación.
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

    public BackupViewModel(
        IBackupService backupService,
        IRestauracionService restauracionService,
        IBackupDialogService dialogService,
        ICurrentUserSession session,
        IReinicioAplicacion reinicio,
        ILogger<BackupViewModel>? logger = null)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _restauracionService = restauracionService ?? throw new ArgumentNullException(nameof(restauracionService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _reinicio = reinicio ?? throw new ArgumentNullException(nameof(reinicio));
        _logger = logger ?? NullLogger<BackupViewModel>.Instance;
    }

    public ObservableCollection<RegistroBackupFila> Copias { get; } = new();

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

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public bool TieneUltimaRestauracion => !string.IsNullOrWhiteSpace(UltimaRestauracionTexto);

    /// <summary>
    /// Solo el Gerente ve los botones de operar. Al Administrador se le oculta lo que el servicio le rechazaría (D-3).
    /// </summary>
    public bool PuedeOperar => _session.EsGerente;

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
                    // Secuencial a propósito: ambas consultas comparten el DbContext de la pantalla.
                    var copias = await _backupService.ListarAsync(IdActor, token);
                    var ultima = await _restauracionService.ObtenerUltimaRestauracionAsync(IdActor, token);
                    return (Copias: copias, Ultima: ultima);
                },
                token),
            resultado =>
            {
                Copias.Clear();
                foreach (var dto in resultado.Copias)
                {
                    Copias.Add(new RegistroBackupFila(dto));
                }

                UltimaRestauracionTexto = resultado.Ultima is null ? null : DescribirRestauracion(resultado.Ultima);
                MensajeEstado = $"Se cargaron {Copias.Count} copias.";
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
            _dialogService.MostrarError("Operación no permitida", "Solo un Gerente puede crear copias de seguridad.");
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
                "Seleccione una copia verificada, cuyo archivo se conserve, y use una cuenta de Gerente.");
            return;
        }

        var idActor = IdActor;
        ResultadoRestauracionDto? restaurada = null;
        var recargar = false;

        IsBusy = true;
        MensajeError = null;

        try
        {
            var vm = ConfirmarCredencialViewModel.ParaRestauracion(
                async (password, progreso) =>
                {
                    restaurada = await Task.Run(() =>
                        _restauracionService.RestaurarDesdeHistorialAsync(idActor, password, copia.Referencia, progreso));
                },
                copia.FechaLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
                ResultadoRestauracionDto.AvisoConciliacionArca);

            var resultado = _dialogService.SolicitarCredencial(vm);

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

        return $"Última restauración: {fecha:dd/MM/yyyy HH:mm} por {restauracion.NombreUsuario} ({origen}), copia {archivo}.";
    }
}
