using FluentValidation;
using Retail.Application.DTOs.Backups;
using Retail.Application.DTOs.Sistema;
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
/// Implementación de las operaciones de administración de la base por línea de comandos (RF-23).
/// Reutiliza los mismos validadores y el mismo agregado que el ABM de usuarios: la herramienta no tiene reglas
/// propias. No ofrece recuperación de contraseñas, que es exclusivamente por correo (D-13).
/// </summary>
public class AdministracionSistemaService : IAdministracionSistemaService
{
    private readonly IEsquemaBaseDatos _esquema;
    private readonly IBackupService _backupService;
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<CrearUsuarioDto> _crearUsuarioValidator;

    public AdministracionSistemaService(
        IEsquemaBaseDatos esquema,
        IBackupService backupService,
        IRepository<Usuario> usuarioRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IValidator<CrearUsuarioDto> crearUsuarioValidator)
    {
        _esquema = esquema ?? throw new ArgumentNullException(nameof(esquema));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _crearUsuarioValidator = crearUsuarioValidator ?? throw new ArgumentNullException(nameof(crearUsuarioValidator));
    }

    public async Task<ResultadoMigracionDto> MigrarAsync(CancellationToken cancellationToken = default)
    {
        var estado = await _esquema.ObtenerEstadoAsync(cancellationToken);

        // Si la base es más nueva que esta versión, migrar no lo resuelve y podría empeorarlo.
        if (estado.MigracionesDesconocidas.Count > 0)
        {
            throw new DomainException(
                $"La base es más nueva que esta versión ({estado.MigracionesDesconocidas.Count} migraciones desconocidas). " +
                "Actualice Retail POS antes de migrar.");
        }

        // D-10: nunca se migra una base con datos sin una copia previa. Si la copia falla, la excepción corta acá
        // y no se aplica ninguna migración. Una base que no existe no tiene nada que respaldar.
        ResultadoBackupDto? copiaPrevia = null;
        if (estado.PuedeConectar && estado.MigracionesPendientes.Count > 0)
        {
            copiaPrevia = await _backupService.CrearBackupDelSistemaAsync(TipoBackupEnum.PreMigracion, null, cancellationToken);
        }

        var aplicadas = await _esquema.AplicarMigracionesPendientesAsync(cancellationToken);
        return new ResultadoMigracionDto(aplicadas, copiaPrevia);
    }

    public async Task<UsuarioDto> CrearPrimerGerenteAsync(CrearUsuarioDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Rol != RolUsuarioEnum.Gerente)
        {
            throw new DomainException("La inicialización solo crea la cuenta del primer Gerente.");
        }

        var validationResult = await _crearUsuarioValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var gerentesActivos = await _usuarioRepository.FindAsync(
            u => u.IdRol == (int)RolUsuarioEnum.Gerente,
            includeDeleted: false,
            cancellationToken);

        if (gerentesActivos.Count > 0)
        {
            throw new OperacionNoAutorizadaException(
                "Ya existe un Gerente activo. Las demás cuentas se crean desde la aplicación, con la autorización de un Gerente.");
        }

        var nombreUsuario = ReglasTexto.NormalizarNombreUsuario(dto.NombreUsuario);
        var existentes = await _usuarioRepository.FindAsync(
            u => u.NombreUsuario == nombreUsuario,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"El nombre de usuario '{nombreUsuario}' ya se encuentra registrado en el sistema.");
        }

        var passwordHash = _passwordHasher.HashPassword(dto.Password);
        var gerente = Usuario.Crear(dto.NombreUsuario, dto.Nombre, dto.Apellido, passwordHash, dto.Rol, dto.EmailRecuperacion);

        // A diferencia de un alta desde la aplicación, la contraseña la eligió el propio titular frente al equipo:
        // no hay otra persona que la conozca, así que no se le exige cambiarla al ingresar.
        gerente.CambiarPasswordPropia(passwordHash);

        await _usuarioRepository.AddAsync(gerente, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UsuarioDto
        {
            IdUsuario = gerente.Id,
            NombreUsuario = gerente.NombreUsuario,
            Nombre = gerente.Nombre,
            Apellido = gerente.Apellido,
            Rol = RolUsuarioEnum.Gerente,
            EmailRecuperacion = gerente.EmailRecuperacion,
            DebeCambiarPassword = gerente.DebeCambiarPassword,
            Activo = true,
            CreatedAt = gerente.CreatedAt
        };
    }
}
