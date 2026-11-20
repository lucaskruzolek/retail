using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;

namespace Retail.App.Services;

/// <summary>
/// Registro de la sesión única del usuario al arrancar (MT-3), después de verificar la terminal y antes de mostrar la
/// ventana principal. Un usuario no puede tener dos sesiones vigentes a la vez: si ya está abierta en otra terminal, la
/// aplicación no abre. Vive aparte de <c>App.xaml.cs</c> para poder probarse sin WPF y no ensanchar ese archivo (Ley 10).
/// </summary>
/// <remarks>
/// Usa su propio scope de DI (y su propio DbContext), que se descarta al terminar: el Shell es Singleton (H-19).
/// </remarks>
public sealed class InicioSesionUnica
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserSession _sesion;
    private readonly ITerminalLocal _terminalLocal;
    private readonly IAvisosSesion _avisos;
    private readonly ILogger<InicioSesionUnica> _logger;

    public InicioSesionUnica(
        IServiceScopeFactory scopeFactory,
        ICurrentUserSession sesion,
        ITerminalLocal terminalLocal,
        IAvisosSesion avisos,
        ILogger<InicioSesionUnica> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _terminalLocal = terminalLocal ?? throw new ArgumentNullException(nameof(terminalLocal));
        _avisos = avisos ?? throw new ArgumentNullException(nameof(avisos));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registra la sesión de quien ingresó en esta terminal. Devuelve <c>true</c> si la aplicación puede seguir abriéndose
    /// y <c>false</c> si debe cerrarse (el usuario ya tiene una sesión vigente en otra terminal).
    /// </summary>
    public async Task<bool> EjecutarAsync()
    {
        if (_sesion.IdUsuario is not int idUsuario || _terminalLocal.IdTerminal is not int idTerminal)
        {
            _logger.LogWarning("No se pudo registrar la sesión: falta el usuario o la terminal de esta PC");
            _avisos.RechazarInicio("No se pudo identificar esta terminal. Vuelva a abrir la aplicación; si persiste, consulte a un Gerente.");
            return false;
        }

        Retail.Application.DTOs.Sesiones.ResultadoInicioSesionDto resultado;
        using (var scope = _scopeFactory.CreateScope())
        {
            resultado = await scope.ServiceProvider
                .GetRequiredService<ISesionService>()
                .IniciarAsync(idUsuario, idTerminal);
        }

        if (!resultado.Iniciada)
        {
            _logger.LogWarning(
                "Inicio rechazado: el usuario {IdUsuario} ya tiene una sesión vigente en la terminal {Terminal}",
                idUsuario,
                resultado.TerminalOcupada ?? "(desconocida)");

            _avisos.RechazarInicio(ArmarMensajeDeRechazo(resultado.TerminalOcupada, resultado.InicioOtraSesionUtc));
            return false;
        }

        if (resultado.TomoSesionVencida)
        {
            _logger.LogWarning("El usuario {IdUsuario} retomó una sesión vencida de otra terminal", idUsuario);
            _avisos.Informar("Se retomó la sesión que había quedado abierta en otra terminal.");
        }

        return true;
    }

    private string ArmarMensajeDeRechazo(string? terminalOcupada, DateTime? inicioUtc)
    {
        var donde = string.IsNullOrWhiteSpace(terminalOcupada) ? "en otra terminal" : $"en la terminal «{terminalOcupada}»";
        var desde = inicioUtc is { } inicio ? $" desde las {ConversionHora.AHoraLocal(inicio):HH:mm}" : string.Empty;

        return $"{_sesion.NombreCompleto} ya tiene una sesión abierta {donde}{desde}. " +
               "Ciérrela allí, o espere 3 minutos si esa PC se apagó.";
    }
}
