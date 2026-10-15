using FluentValidation;
using FluentValidation.Results;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación del caso de uso de administración de usuarios, roles RBAC y credenciales (RF-03).
/// Aplica la matriz de autoridad del actor (<see cref="Usuario.PuedeGestionarCuentasDeRol"/>) en cada operación
/// y custodia la invariante de negocio que impide eliminar o revocar el rol al último Gerente activo.
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

    public async Task<IReadOnlyList<UsuarioDto>> ListarUsuariosAsync(int idActor, CancellationToken cancellationToken = default)
    {
        var actor = await ObtenerGestorDeCuentasAsync(idActor, cancellationToken);

        var usuarios = await _usuarioRepository.ListAllAsync(includeDeleted: false, cancellationToken);

        return usuarios
            .OrderBy(u => u.Apellido)
            .ThenBy(u => u.Nombre)
            .Select(u => MapToDto(u, actor))
            .ToList();
    }

    public async Task<IReadOnlyList<RolUsuarioEnum>> ObtenerRolesAsignablesAsync(int idActor, CancellationToken cancellationToken = default)
    {
        var actor = await ObtenerActorAsync(idActor, cancellationToken);

        return Enum.GetValues<RolUsuarioEnum>()
            .Where(actor.PuedeGestionarCuentasDeRol)
            .ToList();
    }

    public async Task<UsuarioDto> ObtenerPorIdAsync(int idActor, int idUsuario, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuario), "El identificador del usuario debe ser mayor a cero.");
        }

        var actor = await ObtenerGestorDeCuentasAsync(idActor, cancellationToken);

        var usuario = await _usuarioRepository.GetByIdAsync(idUsuario, includeDeleted: true, cancellationToken);
        if (usuario == null)
        {
            throw new DomainException($"No se encontró ningún usuario con el ID {idUsuario}.");
        }

        return MapToDto(usuario, actor);
    }

    public async Task<UsuarioDto> RegistrarUsuarioAsync(int idActor, CrearUsuarioDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var actor = await ObtenerGestorDeCuentasAsync(idActor, cancellationToken);

        var validationResult = await _crearUsuarioValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        ExigirAutoridadSobreRol(actor, dto.Rol);

        // Se compara la forma canónica (minúsculas): "JPerez" y "jperez" son la misma cuenta por regla de
        // dominio, sin depender de que la collation de la base ignore mayúsculas.
        var usernameNormalizado = ReglasTexto.NormalizarNombreUsuario(dto.NombreUsuario);
        var existentes = await _usuarioRepository.FindAsync(
            u => u.NombreUsuario == usernameNormalizado,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"El nombre de usuario '{usernameNormalizado}' ya se encuentra registrado en el sistema.");
        }

        var passwordHash = _passwordHasher.HashPassword(dto.Password);

        var nuevoUsuario = Usuario.Crear(
            dto.NombreUsuario,
            dto.Nombre,
            dto.Apellido,
            passwordHash,
            dto.Rol,
            dto.EmailRecuperacion);

        await _usuarioRepository.AddAsync(nuevoUsuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(nuevoUsuario, actor);
    }

    public async Task<UsuarioDto> ModificarUsuarioAsync(int idActor, ModificarUsuarioDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var actor = await ObtenerGestorDeCuentasAsync(idActor, cancellationToken);

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

        // Se exige autoridad sobre el rol actual y sobre el nuevo: un Administrador no puede editar a un Gerente
        // ni convertir a un Cajero en Gerente.
        ExigirAutoridadSobreRol(actor, (RolUsuarioEnum)usuario.IdRol);
        ExigirAutoridadSobreRol(actor, dto.Rol);

        if (usuario.EsGerente() && dto.Rol != RolUsuarioEnum.Gerente)
        {
            await ExigirQueNoSeaElUltimoGerenteAsync(usuario.Id, cancellationToken);
        }

        usuario.ActualizarDatos(dto.Nombre, dto.Apellido, dto.Rol, dto.EmailRecuperacion);

        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(usuario, actor);
    }

    public async Task BajaUsuarioAsync(int idActor, int idUsuario, CancellationToken cancellationToken = default)
    {
        if (idUsuario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuario), "El identificador del usuario debe ser mayor a cero.");
        }

        var actor = await ObtenerGestorDeCuentasAsync(idActor, cancellationToken);

        var usuario = await _usuarioRepository.GetByIdAsync(idUsuario, includeDeleted: false, cancellationToken);
        if (usuario == null)
        {
            throw new DomainException($"No se encontró ningún usuario activo con el ID {idUsuario}.");
        }

        ExigirAutoridadSobreRol(actor, (RolUsuarioEnum)usuario.IdRol);

        if (usuario.EsGerente())
        {
            await ExigirQueNoSeaElUltimoGerenteAsync(usuario.Id, cancellationToken);
        }

        await _usuarioRepository.DeleteAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CambiarPasswordAsync(int idActor, CambiarPasswordDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validationResult = await _cambiarPasswordValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var actor = await ObtenerActorAsync(idActor, cancellationToken);
        var esCambioPropio = actor.Id == dto.IdUsuario;
        Usuario usuario;

        if (esCambioPropio)
        {
            // Cualquier usuario puede cambiar su propia contraseña, pero demostrando que conoce la actual: una
            // sesión abierta no alcanza para tomar la cuenta.
            usuario = actor;
            ExigirPasswordActualValidaYDistinta(usuario, dto);
        }
        else
        {
            // Blanquear la de otro requiere ser gestor de cuentas. Se verifica antes de buscar al objetivo para
            // que un rol sin autoridad no pueda averiguar qué cuentas existen.
            ExigirGestorDeCuentas(actor);
            usuario = await _usuarioRepository.GetByIdAsync(dto.IdUsuario, includeDeleted: false, cancellationToken)
                ?? throw new DomainException($"No se encontró ningún usuario activo con el ID {dto.IdUsuario}.");
            ExigirAutoridadSobreRol(actor, (RolUsuarioEnum)usuario.IdRol);
        }

        // Esta regla no está en CambiarPasswordValidator porque necesita el nombre de usuario, que se lee de la
        // base. Se informa como error de validación para que la UI la muestre igual que las demás.
        if (string.Equals(dto.NuevaPassword.Trim(), usuario.NombreUsuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(dto.NuevaPassword), "La contraseña no puede ser igual al nombre de usuario.")
            ]);
        }

        var nuevoHash = _passwordHasher.HashPassword(dto.NuevaPassword);
        if (esCambioPropio)
        {
            usuario.CambiarPasswordPropia(nuevoHash);
        }
        else
        {
            usuario.BlanquearPassword(nuevoHash);
        }

        await _usuarioRepository.UpdateAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// El actor se lee de la base en cada operación: si fue dado de baja o cambió de rol mientras tenía la sesión
    /// abierta, la autorización usa su estado actual y no el que tenía al iniciar sesión.
    /// </summary>
    private async Task<Usuario> ObtenerActorAsync(int idActor, CancellationToken cancellationToken)
    {
        var actor = idActor > 0
            ? await _usuarioRepository.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        return actor ?? throw new OperacionNoAutorizadaException(
            "La operación fue solicitada por un usuario inexistente o dado de baja.");
    }

    private async Task<Usuario> ObtenerGestorDeCuentasAsync(int idActor, CancellationToken cancellationToken)
    {
        var actor = await ObtenerActorAsync(idActor, cancellationToken);
        ExigirGestorDeCuentas(actor);
        return actor;
    }

    private static void ExigirGestorDeCuentas(Usuario actor)
    {
        if (!actor.EsGerente() && !actor.EsAdministrador())
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente o un Administrador puede gestionar usuarios.");
        }
    }

    /// <summary>
    /// Cambio propio: la contraseña actual debe ser correcta y la nueva distinta. Lo segundo evita que, en el
    /// cambio obligatorio, el titular conserve la contraseña temporal que eligió otra persona (D-6).
    /// </summary>
    private void ExigirPasswordActualValidaYDistinta(Usuario usuario, CambiarPasswordDto dto)
    {
        if (string.IsNullOrEmpty(dto.PasswordActual)
            || !_passwordHasher.VerifyPassword(dto.PasswordActual, usuario.PasswordHash))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(dto.PasswordActual), "La contraseña actual no es correcta.")
            ]);
        }

        if (dto.NuevaPassword == dto.PasswordActual)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(dto.NuevaPassword), "La nueva contraseña debe ser distinta de la actual.")
            ]);
        }
    }

    private static void ExigirAutoridadSobreRol(Usuario actor, RolUsuarioEnum rolObjetivo)
    {
        if (!actor.PuedeGestionarCuentasDeRol(rolObjetivo))
        {
            throw new OperacionNoAutorizadaException(
                $"Su rol no tiene autoridad para gestionar cuentas con rol {rolObjetivo}.");
        }
    }

    private async Task ExigirQueNoSeaElUltimoGerenteAsync(int idUsuario, CancellationToken cancellationToken)
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

    /// <summary>
    /// El email de recuperación es la llave de acceso de la cuenta: solo lo ve quien tiene autoridad sobre ese rol.
    /// Así un Administrador no conoce el canal de recuperación de un Gerente.
    /// </summary>
    private static UsuarioDto MapToDto(Usuario usuario, Usuario actor)
    {
        return new UsuarioDto
        {
            IdUsuario = usuario.Id,
            NombreUsuario = usuario.NombreUsuario,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Rol = (RolUsuarioEnum)usuario.IdRol,
            EmailRecuperacion = actor.PuedeGestionarCuentasDeRol((RolUsuarioEnum)usuario.IdRol)
                ? usuario.EmailRecuperacion
                : null,
            DebeCambiarPassword = usuario.DebeCambiarPassword,
            Activo = !usuario.IsDeleted,
            CreatedAt = usuario.CreatedAt
        };
    }
}
