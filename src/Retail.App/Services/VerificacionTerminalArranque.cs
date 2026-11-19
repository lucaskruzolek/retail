using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Retail.App.ViewModels.Terminales;
using Retail.Application.DTOs.Terminales;
using Retail.Application.Interfaces.Services;

namespace Retail.App.Services;

/// <summary>
/// Verificación de la identidad de la terminal al arrancar (MT-1), después del ingreso normal y antes de mostrar la
/// ventana principal. Decide qué hacer según el estado de esta PC: seguir, avisar y seguir, activarla (si quien ingresó
/// es Gerente) o bloquear el arranque (si no lo es). Vive aparte de <c>App.xaml.cs</c> para poder probarse sin WPF
/// y para no ensanchar ese archivo compartido (Ley 10).
/// </summary>
/// <remarks>
/// Usa su propio scope de DI (y su propio DbContext), que se descarta al terminar la verificación o al cerrarse el
/// diálogo de activación (H-19): el Shell es Singleton y no puede retener un contexto Scoped.
/// </remarks>
public sealed class VerificacionTerminalArranque
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserSession _sesion;
    private readonly IDialogosArranqueTerminal _dialogos;
    private readonly ILogger<VerificacionTerminalArranque> _logger;

    public VerificacionTerminalArranque(
        IServiceScopeFactory scopeFactory,
        ICurrentUserSession sesion,
        IDialogosArranqueTerminal dialogos,
        ILogger<VerificacionTerminalArranque> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Verifica esta PC y, si hace falta, la activa. Devuelve <c>true</c> si la aplicación puede seguir abriéndose y
    /// <c>false</c> si debe cerrarse (la PC no está activada y nadie la activó).
    /// </summary>
    public async Task<bool> EjecutarAsync()
    {
        EstadoTerminalDto estado;
        using (var scope = _scopeFactory.CreateScope())
        {
            estado = await scope.ServiceProvider.GetRequiredService<ITerminalService>().VerificarAsync();
        }

        switch (estado.Estado)
        {
            case EstadoTerminalEnum.Activa:
                return true;

            case EstadoTerminalEnum.EquipoDistinto:
                var terminal = estado.Terminal;
                _logger.LogWarning(
                    "La terminal {Terminal} está registrada con el equipo {EquipoRegistrado}, distinto de esta PC",
                    terminal?.Nombre,
                    terminal?.NombreEquipo);

                _dialogos.Informar(
                    $"Esta PC figura como terminal «{terminal?.Nombre}», registrada en el equipo «{terminal?.NombreEquipo}». " +
                    "Si la configuración se copió de otra PC, un Gerente debe activarla de nuevo.");
                return true;

            default:
                return ActivarSiEsGerente(estado.Estado);
        }
    }

    private bool ActivarSiEsGerente(EstadoTerminalEnum estado)
    {
        if (!_sesion.EsGerente)
        {
            _logger.LogWarning("Arranque bloqueado: esta PC no está activada como terminal ({Estado}) y quien ingresó no es Gerente", estado);
            _dialogos.Bloquear(
                "Esta PC no está activada como terminal de la librería. Un Gerente debe iniciar sesión en ella para activarla.");
            return false;
        }

        // El scope vive mientras el diálogo está abierto: su ViewModel usa el servicio (y el DbContext) de ese scope.
        using var scope = _scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<ActivarTerminalViewModel>();
        viewModel.Inicializar(terminalInexistente: estado == EstadoTerminalEnum.Inexistente);

        var activada = _dialogos.ActivarTerminal(viewModel);
        if (!activada)
        {
            _logger.LogWarning("Arranque cancelado: el Gerente no activó esta PC como terminal");
        }

        return activada;
    }
}
