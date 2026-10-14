using Retail.Application.DTOs.Sistema;
using Retail.Application.DTOs.Usuarios;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Operaciones de administración de la base que se ejecutan fuera de la interfaz gráfica (RF-23).
/// </summary>
/// <remarks>
/// No reciben un actor, así que solo incluyen operaciones que no otorgan acceso a nadie: actualizar el esquema y
/// crear el primer Gerente cuando todavía no existe ninguno. La recuperación de contraseñas NO está acá: un
/// empleado con acceso al equipo podría usarla para tomar la cuenta del Gerente (D-13). Este servicio no se
/// registra en <c>AddApplicationServices</c> sino en <c>AddAdministracionSistema</c>, que solo invoca la
/// herramienta de comandos.
/// </remarks>
public interface IAdministracionSistemaService
{
    /// <summary>
    /// Actualiza el esquema (crea la base si no existe). Si la base ya existe y hay migraciones pendientes, antes
    /// toma una copia <c>PreMigracion</c>: si la copia falla, no se migra.
    /// </summary>
    Task<ResultadoMigracionDto> MigrarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea el primer Gerente de una instalación. Se rechaza si ya existe un Gerente activo: a partir de ahí,
    /// las cuentas se crean desde la aplicación con la autorización por rol.
    /// </summary>
    Task<UsuarioDto> CrearPrimerGerenteAsync(CrearUsuarioDto dto, CancellationToken cancellationToken = default);
}
