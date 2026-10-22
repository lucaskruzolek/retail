using FluentValidation;
using Retail.Application.DTOs.Auth;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;

using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación del caso de uso de autenticación de operadores (RF-01).
/// Valida credenciales contra el hash protegido con salt (RNF-04) y emite el contexto de usuario.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<LoginRequestDto> _validator;
    private readonly IRegistroAccesosService _registroAccesosService;

    public AuthService(
        IRepository<Usuario> usuarioRepository,
        IPasswordHasher passwordHasher,
        IValidator<LoginRequestDto> validator,
        IRegistroAccesosService registroAccesosService)
    {
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _registroAccesosService = registroAccesosService ?? throw new ArgumentNullException(nameof(registroAccesosService));
    }

    public async Task<LoginResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Misma forma canónica con la que UsuarioService guarda el login: "Admin" entra como "admin".
        var username = ReglasTexto.NormalizarNombreUsuario(request.NombreUsuario);
        var usuarios = await _usuarioRepository.FindAsync(
            u => u.NombreUsuario == username,
            includeDeleted: true,
            cancellationToken);

        var usuarioActivo = usuarios.FirstOrDefault(u => !u.IsDeleted);
        if (usuarioActivo != null)
        {
            var passwordValido = _passwordHasher.VerifyPassword(request.Password, usuarioActivo.PasswordHash);

            // Bloqueo por autorizaciones fallidas (D-31), distinto del bloqueo temporal de inicio de sesión. Se informa
            // solo con la contraseña correcta, para no revelar qué cuentas están bloqueadas a quien no la conoce, y
            // antes de llamar al SP: informarle un login válido registraría "Aceptado" y reiniciaría su contador.
            if (passwordValido && usuarioActivo.EstaBloqueada)
            {
                throw CuentaBloqueadaException.PorAutorizacionesFallidas();
            }

            var intento = await _registroAccesosService.RegistrarIntentoAsync(
                usuarioActivo.NombreUsuario,
                usuarioActivo.Id,
                passwordValido,
                cancellationToken);

            if (intento.EstaBloqueado)
            {
                throw new CuentaBloqueadaException(intento.BloqueadoHasta);
            }

            if (!passwordValido)
            {
                var msg = intento.IntentosRestantes > 0
                    ? $"Contraseña incorrecta. Le quedan {intento.IntentosRestantes} intentos antes del bloqueo."
                    : "Contraseña incorrecta.";
                throw new CredencialesInvalidasException(msg);
            }

            return new LoginResultDto
            {
                IdUsuario = usuarioActivo.Id,
                NombreUsuario = usuarioActivo.NombreUsuario,
                NombreCompleto = usuarioActivo.NombreCompleto,
                Rol = (RolUsuarioEnum)usuarioActivo.IdRol,
                DebeCambiarPassword = usuarioActivo.DebeCambiarPassword
            };
        }

        if (usuarios.Any(u => u.IsDeleted))
        {
            throw new UsuarioInactivoException(username);
        }

        await _registroAccesosService.RegistrarIntentoAsync(username, null, false, cancellationToken);
        throw new CredencialesInvalidasException();
    }

    public async Task VerificarCredencialAsync(int idUsuario, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);

        var usuario = idUsuario > 0
            ? await _usuarioRepository.GetByIdAsync(idUsuario, includeDeleted: false, cancellationToken)
            : null;

        // Un usuario inexistente o dado de baja recibe la misma respuesta que una contraseña incorrecta: la
        // reautenticación no debe revelar el estado de otras cuentas.
        if (usuario == null || string.IsNullOrEmpty(password)
            || !_passwordHasher.VerifyPassword(password, usuario.PasswordHash))
        {
            throw new CredencialesInvalidasException();
        }

        // Mismo orden que en el login: el bloqueo solo se revela a quien demostró conocer la contraseña (D-31).
        if (usuario.EstaBloqueada)
        {
            throw CuentaBloqueadaException.PorAutorizacionesFallidas();
        }
    }
}
