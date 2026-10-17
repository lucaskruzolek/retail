using Retail.Application.DTOs.Articulos;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Puerto de lectura de rubros, categorías y marcas (Track 6.5, D-20). Lo implementa Infrastructure con
/// proyecciones <c>AsNoTracking</c> y filtros, ordenamiento y conteos ejecutados en SQL Server (Ley 8,
/// push-down); los conteos de uso son la base del bloqueo de bajas (D-9). Los listados devuelven solo
/// elementos activos, ordenados por nombre, con filtro de texto opcional (contiene).
/// </summary>
public interface IClasificacionQueryService
{
    /// <summary>
    /// Rubros activos. <see cref="RubroDto.CantidadEnUso"/> cuenta sus categorías activas.
    /// </summary>
    Task<IReadOnlyList<RubroDto>> ListarRubrosAsync(string? filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Categorías activas. <see cref="CategoriaDto.CantidadEnUso"/> cuenta los artículos asignados, incluidos
    /// los dados de baja (R-1).
    /// </summary>
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(string? filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marcas activas. <see cref="MarcaDto.CantidadEnUso"/> cuenta los artículos asignados, incluidos los
    /// dados de baja (R-1).
    /// </summary>
    Task<IReadOnlyList<MarcaDto>> ListarMarcasAsync(string? filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta las categorías activas del rubro; las dadas de baja lógica no cuentan.
    /// </summary>
    Task<int> ContarCategoriasActivasPorRubroAsync(int idRubro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta los artículos de la categoría, incluidos los dados de baja lógica (R-1).
    /// </summary>
    Task<int> ContarArticulosPorCategoriaAsync(int idCategoria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta los artículos de la marca, incluidos los dados de baja lógica (R-1).
    /// </summary>
    Task<int> ContarArticulosPorMarcaAsync(int idMarca, CancellationToken cancellationToken = default);
}
