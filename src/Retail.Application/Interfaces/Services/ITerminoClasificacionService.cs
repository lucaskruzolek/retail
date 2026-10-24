using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Exceptions;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Administración de la tabla de términos del motor de catalogación (Track 6.6, M-16, M-27). Las escrituras exigen
/// que el actor <c>PuedeAdministrarClasificacion</c> (D-5) y pueden lanzar
/// <see cref="FluentValidation.ValidationException"/> si el DTO es inválido.
/// </summary>
public interface ITerminoClasificacionService
{
    /// <summary>
    /// Términos activos ordenados alfabéticamente. No exige rol (D-21).
    /// </summary>
    Task<IReadOnlyList<TerminoDto>> ListarAsync(
        string? filtro,
        int? idCategoria,
        bool soloAmbiguos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra el término o, si ya existe, lo reasigna: un término figura una sola vez en todo el catálogo. Sin
    /// categoría queda ambiguo. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y
    /// <see cref="DomainException"/> si la categoría no existe o si otro usuario registró el término al mismo tiempo.
    /// </summary>
    Task RegistrarAsync(int idActor, RegistrarTerminoDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica: el término pasa a ser desconocido (decisión en bloque, M-26). Lanza
    /// <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y <see cref="DomainException"/> si
    /// el término no existe.
    /// </summary>
    Task BajaAsync(int idActor, int idTermino, CancellationToken cancellationToken = default);
}
