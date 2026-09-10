using FluentValidation;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación del caso de uso de administración de usuarios, roles RBAC y credenciales (RF-03).
/// Custodia la invariante de negocio que impide eliminar o revocar el rol al último Gerente activo.
/// </summary>
public class UsuarioService : IUsuarioService
{
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<CrearUsuarioDto> _crearUsuarioValidator;
    private readonly IValidator<ModificarUsuarioDto> _modificarUsuarioValidator;
    private readonly IValidator<CambiarPasswordDto> _cambiarPasswordValidator;

    public UsuarioService(
        IRepository<Usuario> usuarioRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IValidator<CrearUsuarioDto> crearUsuarioValidator,
        IValidator<ModificarUsuarioDto> modificarUsuarioValidator,
        IValidator<CambiarPasswordDto> cambiarPasswordValidator)
    {
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _crearUsuarioValidator = crearUsuarioValidator ?? throw new ArgumentNullException(nameof(crearUsuarioValidator));
        _modificarUsuarioValidator = modificarUsuarioValidator ?? throw new ArgumentNullException(nameof(modificarUsuarioValidator));
        _cambiarPasswordValidator = cambiarPasswordValidator ?? throw new ArgumentNullException(nameof(cambiarPasswordValidator));
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarUsuariosAsync(CancellationToken cancellationToken = default)
    {
        var usuarios = await _usuarioRepository.ListAllAsync(includeDeleted: false, cancellationToken);

        return usuarios
            .OrderBy(u => u.NombreCompleto)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<UsuarioDto> ObtenerPorIdAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuario), "El identificador del usuario debe ser mayor a cero.");
        }

        var usuario = await _usuarioRepository.GetByIdAsync(idUsuario, includeDeleted: true, cancellationToken);
        if (usuario == null)
        {
            throw new DomainException($"No se encontró ningún usuario con el ID {idUsuario}.");
        }

        return MapToDto(usuario);
    }

    public async Task<UsuarioDto> RegistrarUsuarioAsync(CrearUsuarioDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validationResult = await _crearUsuarioValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var usernameNormalizado = dto.NombreUsuario.Trim();
        var existentes = await _usuarioRepository.FindAsync(
            u => u.NombreUsuario == usernameNormalizado,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"El nombre de usuario '{dto.NombreUsuario}' ya se encuentra registrado en el sistema.");
        }

        var passwordHash = _passwordHasher.HashPassword(dto.Password);

        var nuevoUsuario = new Usuario
        {
            NombreUsuario = dto.NombreUsuario.Trim(),
            NombreCompleto = dto.NombreCompleto.Trim(),
            PasswordHash = passwordHash,
            IdRol = (int)dto.Rol
        };

        await _usuarioRepository.AddAsync(nuevoUsuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(nuevoUsuario);
    }

    public async Task<UsuarioDto> ModificarUsuarioAsync(ModificarUsuarioDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validationResult = await _modificarUsuarioValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var usuario = await _usuarioRepository.GetByIdAsync(dto.IdUsuario, includeDeleted: false, cancellationToken);
        if (usuario == null)
        {
            throw new DomainException($"No se encontró ningún usuario activo con el ID {dto.IdUsuario}.");
        }

        // Si se intenta degradar a un Gerente a otro rol, custodiar que no sea el único Gerente activo
        if (usuario.EsGerente() && dto.Rol != RolUsuarioEnum.Gerente)
        {
            var gerentesActivos = await _usuarioRepository.FindAsync(
                u => u.IdRol == (int)RolUsuarioEnum.Gerente,
                includeDeleted: false,
                cancellationToken);

            if (gerentesActivos.Count <= 1 || gerentesActivos.All(g => g.Id == dto.IdUsuario))
            {
                throw new UltimoGerenteException(dto.IdUsuario);
            }
        }

        usuario.ActualizarDatos(dto.NombreCompleto, (int)dto.Rol);

        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(usuario);
    }

    public async Task BajaUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuario), "El identificador del usuario debe ser mayor a cero.");
        }

        var usuario = await _usuarioRepository.GetByIdAsync(idUsuario, includeDeleted: false, cancellationToken);
        if (usuario == null)
        {
            throw new DomainException($"No se encontró ningún usuario activo con el ID {idUsuario}.");
        }

        if (usuario.EsGerente())
        {
            var gerentesActivos = await _usuarioRepository.FindAsync(
                u => u.IdRol == (int)RolUsuarioEnum.Gerente,
                includeDeleted: false,
                cancellationToken);

            if (gerentesActivos.Count <= 1 || gerentesActivos.All(g => g.Id == idUsuario))
            {
                throw new UltimoGerenteException(idUsuario);
            }
        }

        await _usuarioRepository.DeleteAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CambiarPasswordAsync(CambiarPasswordDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validationResult = await _cambiarPasswordValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var usuario = await _usuarioRepository.GetByIdAsync(dto.IdUsuario, includeDeleted: false, cancellationToken);
        if (usuario == null)
        {
            throw new DomainException($"No se encontró ningún usuario activo con el ID {dto.IdUsuario}.");
        }

        var nuevoHash = _passwordHasher.HashPassword(dto.NuevaPassword);
        usuario.ActualizarPassword(nuevoHash);

        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static UsuarioDto MapToDto(Usuario usuario)
    {
        return new UsuarioDto
        {
            IdUsuario = usuario.Id,
            NombreUsuario = usuario.NombreUsuario,
            NombreCompleto = usuario.NombreCompleto,
            Rol = (RolUsuarioEnum)usuario.IdRol,
            Activo = !usuario.IsDeleted,
            CreatedAt = usuario.CreatedAt
        };
    }
}
