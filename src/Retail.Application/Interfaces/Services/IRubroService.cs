using Retail.Application.DTOs.Articulos;
using Retail.Domain.Exceptions;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Administración de rubros (Track 6.5, D-10: un servicio por tipo, sin genérico). Las operaciones de
/// escritura exigen que el actor <c>PuedeAdministrarClasificacion</c> (D-5). Todas pueden lanzar
/// <see cref="FluentValidation.ValidationException"/> si el DTO es inválido.
/// </summary>
public interface IRubroService
{
    /// <summary>
    /// Lista los elementos activos ordenados por nombre, con filtro de texto opcional (contiene). No exige
    /// rol: lo consulta cualquier usuario autenticado (D-21).
    /// </summary>
    Task<IReadOnlyList<RubroDto>> ListarAsync(string? filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de alta un elemento. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene
    /// autoridad y <see cref="DomainException"/> si el nombre ya existe.
    /// </summary>
    Task<RubroDto> CrearAsync(int idActor, CrearRubroDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el nombre. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y
    /// <see cref="DomainException"/> si el elemento no existe o el nombre está duplicado.
    /// </summary>
    Task RenombrarAsync(int idActor, RenombrarRubroDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad,
    /// <see cref="DomainException"/> si el elemento no existe y <see cref="ClasificacionEnUsoException"/>
    /// si está en uso (D-1).
    /// </summary>
    Task BajaAsync(int idActor, int idRubro, CancellationToken cancellationToken = default);
}
