using Retail.Application.DTOs.Articulos;
using Retail.Domain.Exceptions;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Administración de categorías (Track 6.5, D-10: un servicio por tipo, sin genérico). Las operaciones de
/// escritura exigen que el actor <c>PuedeAdministrarClasificacion</c> (D-5). Todas pueden lanzar
/// <see cref="FluentValidation.ValidationException"/> si el DTO es inválido.
/// </summary>
public interface ICategoriaService
{
    /// <summary>
    /// Lista los elementos activos ordenados por nombre, con filtro de texto opcional (contiene). No exige
    /// rol: lo consulta cualquier usuario autenticado (D-21). Si <c>idRubro</c> tiene valor, solo devuelve las
    /// categorías de ese rubro (D-26).
    /// </summary>
    Task<IReadOnlyList<CategoriaDto>> ListarAsync(string? filtro, int? idRubro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de alta un elemento. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene
    /// autoridad y <see cref="DomainException"/> si el nombre ya existe o el rubro no existe (D-23).
    /// </summary>
    Task<CategoriaDto> CrearAsync(int idActor, CrearCategoriaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el nombre y el rubro (D-23). Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene
    /// autoridad y <see cref="DomainException"/> si el elemento o el rubro no existen, o el nombre está duplicado.
    /// </summary>
    Task ActualizarAsync(int idActor, ActualizarCategoriaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad,
    /// <see cref="DomainException"/> si el elemento no existe y <see cref="ClasificacionEnUsoException"/>
    /// si está en uso (D-1).
    /// </summary>
    Task BajaAsync(int idActor, int idCategoria, CancellationToken cancellationToken = default);
}
