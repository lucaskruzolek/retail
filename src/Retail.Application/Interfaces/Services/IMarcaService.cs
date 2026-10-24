using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Exceptions;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Administración de marcas (Track 6.5, D-10: un servicio por tipo, sin genérico). Las operaciones de
/// escritura exigen que el actor <c>PuedeAdministrarClasificacion</c> (D-5). Todas pueden lanzar
/// <see cref="FluentValidation.ValidationException"/> si el DTO es inválido.
/// </summary>
public interface IMarcaService
{
    /// <summary>
    /// Lista los elementos activos ordenados por nombre, con filtro de texto opcional (contiene). No exige
    /// rol: lo consulta cualquier usuario autenticado (D-21).
    /// </summary>
    Task<IReadOnlyList<MarcaDto>> ListarAsync(string? filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de alta un elemento. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene
    /// autoridad y <see cref="DomainException"/> si el nombre ya existe, el motor no lo distingue de otra marca
    /// (M-39) o es alias de otra marca (M-38).
    /// </summary>
    Task<MarcaDto> CrearAsync(int idActor, CrearMarcaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el nombre. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y
    /// <see cref="DomainException"/> si el elemento no existe, el nombre está duplicado, el motor no lo distingue de
    /// otra marca (M-39) o es alias de otra marca (M-38).
    /// </summary>
    Task RenombrarAsync(int idActor, RenombrarMarcaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica, con sus alias, prefijos y las equivalencias de proveedores que la tienen como destino (M-35).
    /// Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad,
    /// <see cref="DomainException"/> si el elemento no existe y <see cref="ClasificacionEnUsoException"/>
    /// si está en uso (D-1).
    /// </summary>
    Task BajaAsync(int idActor, int idMarca, CancellationToken cancellationToken = default);

    /// <summary>
    /// Alias activos de las marcas activas (Track 6.6). No exige rol (D-21).
    /// </summary>
    Task<IReadOnlyList<AliasListadoDto>> ListarAliasAsync(string? filtro, int? idMarca, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prefijos EAN activos de las marcas activas (Track 6.6). No exige rol (D-21).
    /// </summary>
    Task<IReadOnlyList<PrefijoListadoDto>> ListarPrefijosAsync(string? filtro, int? idMarca, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un alias a la marca. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad
    /// y <see cref="DomainException"/> si la marca no existe, si el alias ya es de esta u otra marca, si es el nombre
    /// de otra marca o si otro usuario lo registró al mismo tiempo.
    /// </summary>
    Task AgregarAliasAsync(int idActor, AliasMarcaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de baja el alias. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y
    /// <see cref="DomainException"/> si la marca o el alias no existen.
    /// </summary>
    Task QuitarAliasAsync(int idActor, AliasMarcaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un prefijo EAN a la marca. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene
    /// autoridad y <see cref="DomainException"/> si la marca no existe, si el prefijo ya es de esta u otra marca o si
    /// otro usuario lo registró al mismo tiempo.
    /// </summary>
    Task AgregarPrefijoAsync(int idActor, PrefijoMarcaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de baja el prefijo. Lanza <see cref="OperacionNoAutorizadaException"/> si el actor no tiene autoridad y
    /// <see cref="DomainException"/> si la marca o el prefijo no existen.
    /// </summary>
    Task QuitarPrefijoAsync(int idActor, PrefijoMarcaDto dto, CancellationToken cancellationToken = default);
}
