using Retail.Application.DTOs.Articulos;
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
    /// Crea una categoría y registra en ella el término (o lo reasigna, si ya existe) en una sola transacción
    /// (Track 6.6, P6-4, T-1): si cualquiera de las dos partes falla, no queda ninguna. Si el término era ambiguo, sus
    /// pares se dan de baja (P-3). Devuelve la categoría creada. Lanza <see cref="OperacionNoAutorizadaException"/>
    /// si el actor no tiene autoridad y <see cref="DomainException"/> si el rubro no existe, si ya hay una categoría
    /// con ese nombre (D-8) o si otro usuario registró lo mismo al mismo tiempo.
    /// </summary>
    Task<CategoriaDto> RegistrarConCategoriaNuevaAsync(
        int idActor,
        RegistrarTerminoConCategoriaNuevaDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica: el término pasa a ser desconocido (decisión en bloque, M-26). Lanza
    /// <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y <see cref="DomainException"/> si
    /// el término no existe.
    /// </summary>
    Task BajaAsync(int idActor, int idTermino, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pares activos de los términos activos (Track 6.6, Fase 5), ordenados por término y segunda palabra. No exige
    /// rol (D-21).
    /// </summary>
    Task<IReadOnlyList<ParDto>> ListarParesAsync(
        int? idTermino,
        string? filtro,
        bool incluirIgnorados,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pares que los artículos confirmados respaldan y que todavía no están en el diccionario, ni con categoría ni
    /// ignorados (Fase 5, M-49, P-5): al menos <c>SoporteMinimo</c> artículos y una categoría con al menos
    /// <c>PurezaMinima</c> de ellos. Solo de los términos ambiguos activos, o solo de <paramref name="idTermino"/> si
    /// se indica. No guarda nada y no exige rol (D-21).
    /// </summary>
    Task<IReadOnlyList<ParPropuestoDto>> ListarParesPropuestosAsync(
        int? idTermino,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asigna un par a un término ambiguo: lo crea o reemplaza su destino; sin categoría queda ignorado (P-4). Es
    /// <see cref="AsignarParesAsync"/> con un solo par, y lanza las mismas excepciones.
    /// </summary>
    Task AsignarParAsync(int idActor, AsignarParDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asigna varios pares en una sola operación (aceptar o descartar propuestas, M-49): se guardan todos o ninguno.
    /// Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y
    /// <see cref="DomainException"/> si un término no existe o tiene categoría (P-3), si una categoría no existe, o si
    /// otro usuario registró el mismo par al mismo tiempo.
    /// </summary>
    Task AsignarParesAsync(int idActor, AsignarParesDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica del par: vuelve a poder proponerse. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor
    /// no tiene autoridad y <see cref="DomainException"/> si el término o el par no existen.
    /// </summary>
    Task QuitarParAsync(int idActor, QuitarParDto dto, CancellationToken cancellationToken = default);
}
