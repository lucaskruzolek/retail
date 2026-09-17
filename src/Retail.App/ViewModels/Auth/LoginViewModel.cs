using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Services;
using Retail.Application.DTOs.Auth;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Auth;

/// <summary>
/// ViewModel responsable de orquestar la autenticación de operadores en el mostrador (RF-01, RF-02).
/// Consume CommunityToolkit.Mvvm con Source Generators para binding reactivo y feedback de errores.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    private string _nombreUsuario = string.Empty;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    private bool _tieneError;

    [ObservableProperty]
    private bool _estaCargando;

    /// <summary>
    /// Evento disparado tras un inicio de sesión exitoso para permitir el cierre coordinado de la vista.
    /// </summary>
    public event Action? LoginExitoso;

    public LoginViewModel(
        IAuthService authService,
        ICurrentUserSession session,
        ILogger<LoginViewModel> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    public async Task IniciarSesionAsync(object? parameter)
    {
        if (EstaCargando)
        {
            return;
        }

        var password = string.Empty;

        if (parameter is PasswordBox passwordBox)
        {
            password = passwordBox.Password;
        }
        else if (parameter is string passwordStr)
        {
            password = passwordStr;
        }

        EstaCargando = true;
        TieneError = false;
        MensajeError = string.Empty;

        try
        {
            var request = new LoginRequestDto
            {
                NombreUsuario = NombreUsuario,
                Password = password
            };

            var resultado = await _authService.LoginAsync(request);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Inicio de sesión exitoso para el operador '{Usuario}' (Rol: {Rol})", resultado.NombreUsuario, resultado.Rol);
            }

            _session.EstablecerSesion(resultado);
            LoginExitoso?.Invoke();
        }
        catch (CredencialesInvalidasException ex)
        {
            TieneError = true;
            MensajeError = ex.Message;
        }
        catch (UsuarioInactivoException ex)
        {
            TieneError = true;
            MensajeError = ex.Message;
        }
        catch (ValidationException ex)
        {
            TieneError = true;
            MensajeError = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo inesperado al autenticar al operador '{Usuario}'", NombreUsuario);
            TieneError = true;
            MensajeError = "Ocurrió un error inesperado al intentar iniciar sesión.";
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
