using FluentValidation;
using Retail.Application.DTOs.Auth;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
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

    public AuthService(
        IRepository<Usuario> usuarioRepository,
        IPasswordHasher passwordHasher,
        IValidator<LoginRequestDto> validator)
    {
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<LoginResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var username = request.NombreUsuario.Trim();
        var usuarios = await _usuarioRepository.FindAsync(
            u => u.NombreUsuario == username,
            includeDeleted: true,
            cancellationToken);

        var usuario = usuarios.Count > 0 ? usuarios[0] : null;
        if (usuario == null)
        {
            throw new CredencialesInvalidasException();
        }

        if (usuario.IsDeleted)
        {
            throw new UsuarioInactivoException(usuario.NombreUsuario);
        }

        var passwordValido = _passwordHasher.VerifyPassword(request.Password, usuario.PasswordHash);
        if (!passwordValido)
        {
            throw new CredencialesInvalidasException();
        }

        return new LoginResultDto
        {
            IdUsuario = usuario.Id,
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            Rol = (RolUsuarioEnum)usuario.IdRol
        };
    }
}
