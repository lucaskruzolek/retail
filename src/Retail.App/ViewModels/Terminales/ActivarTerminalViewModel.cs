using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Services;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Terminales;

/// <summary>
/// Diálogo "Activar esta PC" (MT-1): un Gerente, recién ingresado, convierte esta PC en una terminal de la librería, ya
/// sea dando de alta una terminal nueva o reemplazando el equipo de una existente (cambio de hardware, que conserva el
/// historial de turnos). Los errores de las reglas de negocio se muestran dentro del diálogo, que sigue abierto.
/// </summary>
public partial class ActivarTerminalViewModel : ObservableObject
{
    private readonly ITerminalService _terminalService;
    private readonly ITerminalLocal _terminalLocal;
    private readonly ICurrentUserSession _sesion;
    private readonly ILogger<ActivarTerminalViewModel> _logger;

    public ActivarTerminalViewModel(
        ITerminalService terminalService,
        ITerminalLocal terminalLocal,
        ICurrentUserSession sesion,
        ILogger<ActivarTerminalViewModel> logger)
    {
        _terminalService = terminalService ?? throw new ArgumentNullException(nameof(terminalService));
        _terminalLocal = terminalLocal ?? throw new ArgumentNullException(nameof(terminalLocal));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Terminales activas entre las que se puede elegir cuál reemplazar.</summary>
    public ObservableCollection<OpcionTerminal> Terminales { get; } = [];

    /// <summary>Nombre de Windows de esta PC, tal como lo ve el sistema.</summary>
    public string NombreEquipo => _terminalLocal.NombreEquipo;

    /// <summary>
    /// La terminal guardada en esta PC ya no existe en la base (por ejemplo, tras restaurar una copia más vieja): el
    /// diálogo lo explica en lugar de decir que la PC nunca fue activada.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoIntroduccion))]
    private bool _terminalInexistente;

    public string TextoIntroduccion => TerminalInexistente
        ? $"La terminal guardada en esta PC ({NombreEquipo}) ya no existe en la base, por ejemplo porque se restauró una copia más antigua. Actívela de nuevo."
        : $"Esta PC ({NombreEquipo}) todavía no es una terminal de la librería.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivarCommand))]
    [NotifyPropertyChangedFor(nameof(EsNueva))]
    [NotifyPropertyChangedFor(nameof(EsReemplazo))]
    [NotifyPropertyChangedFor(nameof(NoHayTerminalesParaReemplazar))]
    private ModoActivacionTerminal _modo = ModoActivacionTerminal.Nueva;

    public bool EsNueva => Modo == ModoActivacionTerminal.Nueva;

    public bool EsReemplazo => Modo == ModoActivacionTerminal.Reemplazo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivarCommand))]
    private string _nombre = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivarCommand))]
    private OpcionTerminal? _terminalElegida;

    /// <summary>Ya se leyó la lista de terminales: hasta entonces no se puede decir que no hay ninguna para reemplazar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoHayTerminalesParaReemplazar))]
    private bool _terminalesCargadas;

    public bool NoHayTerminalesParaReemplazar => EsReemplazo && TerminalesCargadas && Terminales.Count == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivarCommand))]
    [NotifyPropertyChangedFor(nameof(NoEstaOcupado))]
    private bool _isBusy;

    public bool NoEstaOcupado => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>La PC quedó activada: el diálogo se cierra con resultado afirmativo.</summary>
    public bool Activada { get; private set; }

    /// <summary>Lo levanta el ViewModel al terminar de activar (<c>true</c>) para que la vista cierre el diálogo.</summary>
    public event EventHandler<bool>? RequestClose;

    /// <summary>Indica si la terminal guardada en esta PC dejó de existir, para ajustar el texto del diálogo.</summary>
    public void Inicializar(bool terminalInexistente)
    {
        TerminalInexistente = terminalInexistente;
    }

    partial void OnNombreChanged(string value)
    {
        MensajeError = null;
    }

    partial void OnTerminalElegidaChanged(OpcionTerminal? value)
    {
        MensajeError = null;
    }

    partial void OnModoChanged(ModoActivacionTerminal value)
    {
        MensajeError = null;
    }

    /// <summary>Lee las terminales activas para ofrecerlas como opción de reemplazo.</summary>
    [RelayCommand]
    public async Task CargarAsync()
    {
        IsBusy = true;

        try
        {
            var terminales = await Task.Run(() => _terminalService.ListarAsync());

            Terminales.Clear();
            foreach (var terminal in terminales)
            {
                Terminales.Add(OpcionTerminal.Desde(terminal));
            }

            TerminalesCargadas = true;
            OnPropertyChanged(nameof(NoHayTerminalesParaReemplazar));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo leer la lista de terminales para activar esta PC");
            MensajeError = "No se pudo leer la lista de terminales. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool PuedeActivar()
    {
        if (IsBusy || Activada)
        {
            return false;
        }

        return EsNueva
            ? !string.IsNullOrWhiteSpace(Nombre)
            : TerminalElegida is not null;
    }

    [RelayCommand(CanExecute = nameof(PuedeActivar))]
    private async Task ActivarAsync()
    {
        if (_sesion.IdUsuario is not int idActor)
        {
            MensajeError = "No se pudo identificar el usuario de la sesión. Vuelva a iniciar sesión.";
            return;
        }

        IsBusy = true;
        MensajeError = null;

        try
        {
            if (EsNueva)
            {
                var nombre = Nombre.Trim();
                await Task.Run(() => _terminalService.ActivarNuevaAsync(idActor, nombre));
            }
            else if (TerminalElegida is { } elegida)
            {
                await Task.Run(() => _terminalService.ActivarReemplazoAsync(idActor, elegida.Id));
            }

            Activada = true;
            RequestClose?.Invoke(this, true);
        }
        catch (ValidationException ex)
        {
            MensajeError = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct());
        }
        catch (DomainException ex)
        {
            // Regla de negocio (nombre repetido, la terminal ya tiene otra PC, falta de permisos): información, no una falla.
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al activar esta PC como terminal");
            MensajeError = "No se pudo activar esta PC. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
