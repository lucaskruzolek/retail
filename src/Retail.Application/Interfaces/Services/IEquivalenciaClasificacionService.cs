using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Administración de las equivalencias de clasificación de un proveedor (Track 6.6, M-13, M-31, M-32). Las escrituras
/// exigen que el actor <c>PuedeAdministrarClasificacion</c> (D-5) y pueden lanzar
/// <see cref="FluentValidation.ValidationException"/> si el DTO es inválido.
/// </summary>
public interface IEquivalenciaClasificacionService
{
    /// <summary>
    /// Equivalencias activas del proveedor con el nombre de su destino. No exige rol (D-21).
    /// </summary>
    Task<IReadOnlyList<EquivalenciaDto>> ListarAsync(
        int idProveedor,
        TipoEquivalenciaEnum? tipo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea la equivalencia o reemplaza su destino; sin destino, el valor queda ignorado (M-32). Lanza
    /// <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y <see cref="DomainException"/> si
    /// el proveedor o el destino no existen, o si otro usuario la registró al mismo tiempo.
    /// </summary>
    Task AsignarAsync(int idActor, AsignarEquivalenciaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica: la próxima importación vuelve a ofrecer el valor para traducirlo. Lanza
    /// <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y <see cref="DomainException"/> si
    /// el proveedor o la equivalencia no existen.
    /// </summary>
    Task QuitarAsync(int idActor, QuitarEquivalenciaDto dto, CancellationToken cancellationToken = default);
}
