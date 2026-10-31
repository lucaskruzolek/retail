using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Base de los ViewModels hijos de <see cref="AdministrarClasificacionViewModel"/> para las secciones de los
/// diccionarios del motor de catalogación (Track 6.6, P6-7): términos, detalle de marca (alias y prefijos, M-68)
/// y reglas propuestas.
/// Concentra lo que tienen en común: el permiso de escritura (D-5), el error visible, la baja con confirmación
/// y la traducción de excepciones a mensajes.
/// </summary>
/// <remarks>
/// Todas las secciones comparten la <see cref="CargaSerializada"/> del diálogo: el diálogo usa un único DbContext
/// y EF Core no admite dos operaciones simultáneas sobre él (H-19).
/// </remarks>
public abstract partial class SeccionClasificacionViewModelBase : ObservableObject
{
    protected static readonly TimeSpan EsperaFiltro = TimeSpan.FromMilliseconds(250);

    private readonly ICurrentUserSession _session;

    protected SeccionClasificacionViewModelBase(
        IClasificacionDialogService dialogService,
        ICurrentUserSession session,
        CargaSerializada carga,
        ILogger logger)
    {
        DialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Carga = carga ?? throw new ArgumentNullException(nameof(carga));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    [ObservableProperty]
    private bool _isBusy;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>
    /// Solo el Gerente y el Encargado modifican los diccionarios (D-5, RF-04). El servidor vuelve a autorizar cada
    /// escritura: la UI solo evita ofrecer lo que va a fallar (P6-6).
    /// </summary>
    public bool PuedeEscribir => _session.EsGerente || _session.EsEncargado;

    protected IClasificacionDialogService DialogService { get; }

    protected CargaSerializada Carga { get; }

    protected ILogger Logger { get; }

    protected int IdActor => _session.IdUsuario ?? 0;

    /// <summary>Pide confirmación y, si el usuario acepta, da de baja y recarga la grilla.</summary>
    protected async Task DarDeBajaAsync(
        string descripcionElemento,
        Func<int, CancellationToken, Task> baja,
        Func<Task> recargar)
    {
        MensajeError = null;

        bool confirmado = DialogService.Confirmar(
            "Confirmar baja",
            $"¿Está seguro de dar de baja {descripcionElemento}?\nEl registro será archivado lógicamente.");
        if (!confirmado)
        {
            return;
        }

        await EscribirAsync("dar de baja el elemento", baja, recargar);
    }

    /// <summary>
    /// Ejecuta una escritura contra el servicio, en segundo plano (Ley 4) y detrás de las demás lecturas. Si sale
    /// bien recarga la grilla; si falla muestra el error sin cerrar nada.
    /// </summary>
    protected async Task EscribirAsync(
        string accion,
        Func<int, CancellationToken, Task> escritura,
        Func<Task> recargar)
    {
        int idActor = IdActor;
        bool exito = false;

        IsBusy = true;
        MensajeError = null;
        try
        {
            await Carga.EjecutarOperacionAsync(
                async token =>
                {
                    await Task.Run(() => escritura(idActor, token), token);
                    return true;
                },
                _ => exito = true,
                ex => InformarError(accion, ex));

            if (exito)
            {
                await recargar();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Lectura puntual (por ejemplo, los pares de un término antes de avisar de su baja).</summary>
    protected async Task<T?> LeerAsync<T>(string accion, Func<CancellationToken, Task<T>> consulta)
    {
        T? resultado = default;

        await Carga.EjecutarOperacionAsync(
            token => Task.Run(() => consulta(token), token),
            r => resultado = r,
            ex => InformarError(accion, ex));

        return resultado;
    }

    protected void InformarError(string accion, Exception ex)
    {
        if (ex is ValidationException validacion)
        {
            Logger.LogWarning(ex, "Validación al {Accion}: {Mensaje}", accion, ex.Message);
            MensajeError = string.Join("\n", validacion.Errors.Select(e => e.ErrorMessage));
            return;
        }

        if (ex is DomainException)
        {
            // Incluye OperacionNoAutorizadaException (D-5) y los duplicados que detecta el servicio.
            Logger.LogWarning(ex, "Regla de negocio al {Accion}: {Mensaje}", accion, ex.Message);
            MensajeError = ex.Message;
            return;
        }

        Logger.LogError(ex, "Error inesperado al {Accion}: {Mensaje}", accion, ex.Message);
        MensajeError = $"Error inesperado al {accion}: {ex.Message}";
    }

    protected static string? NormalizarFiltro(string filtro)
    {
        return string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim();
    }

    /// <summary>Reemplaza el contenido solo si cambió, para no perder selección ni posición de scroll de la grilla.</summary>
    protected static void Reemplazar<T>(ObservableCollection<T> destino, IReadOnlyList<T> nuevos)
    {
        if (destino.SequenceEqual(nuevos))
        {
            return;
        }

        destino.Clear();
        foreach (var item in nuevos)
        {
            destino.Add(item);
        }
    }

    protected static void EjecutarEnDispatcher(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }
}
