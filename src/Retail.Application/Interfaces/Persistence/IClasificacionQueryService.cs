using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Enums;

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
    /// Categorías activas, opcionalmente solo las del rubro <c>idRubro</c> (D-26; el filtro se resuelve en SQL).
    /// <see cref="CategoriaDto.CantidadEnUso"/> cuenta los artículos asignados, incluidos
    /// los dados de baja (R-1).
    /// </summary>
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(string? filtro, int? idRubro, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Términos activos ordenados alfabéticamente (Track 6.6, pestaña de términos). El filtro se compara con el término
    /// en forma de comparación; <paramref name="idCategoria"/> deja solo los de esa categoría y
    /// <paramref name="soloAmbiguos"/>, solo los que no tienen categoría (M-27).
    /// </summary>
    Task<IReadOnlyList<TerminoDto>> ListarTerminosAsync(
        string? filtro,
        int? idCategoria,
        bool soloAmbiguos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pares activos de los términos activos (Track 6.6, Fase 5, pestaña de pares), ordenados por término y segunda
    /// palabra. <paramref name="idTermino"/> deja solo los de ese término; el filtro se compara, en forma de comparación,
    /// con el término y con la segunda palabra; sin <paramref name="incluirIgnorados"/> quedan solo los que tienen
    /// categoría (P-4).
    /// </summary>
    Task<IReadOnlyList<ParDto>> ListarParesAsync(
        int? idTermino,
        string? filtro,
        bool incluirIgnorados,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Artículos confirmados (activos, con categoría activa) cuya descripción contiene alguno de los
    /// <paramref name="terminos"/> en forma de comparación, ordenados por Id (Track 6.6, Fase 5, pares propuestos). Es un
    /// pre-filtro amplio: trae también descripciones donde el término no es la primera palabra ("CASETE" para "SET"),
    /// que el llamador descarta con <c>ReglasClasificacion.TerminoDeDescripcion</c>; nunca deja afuera una que sí lo es.
    /// </summary>
    Task<IReadOnlyList<DescripcionClasificadaDto>> ListarArticulosConTerminosAsync(
        IReadOnlyCollection<string> terminos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alias activos de las marcas activas, ordenados por marca y alias. El filtro busca en el alias y en el nombre
    /// de la marca.
    /// </summary>
    Task<IReadOnlyList<AliasListadoDto>> ListarAliasAsync(string? filtro, int? idMarca, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prefijos EAN activos de las marcas activas, ordenados por marca y prefijo. El filtro busca en el prefijo (que
    /// empiece con) y en el nombre de la marca (que contenga).
    /// </summary>
    Task<IReadOnlyList<PrefijoListadoDto>> ListarPrefijosAsync(string? filtro, int? idMarca, CancellationToken cancellationToken = default);

    /// <summary>
    /// Equivalencias activas del proveedor con el nombre de su destino, opcionalmente solo las de un tipo.
    /// </summary>
    Task<IReadOnlyList<EquivalenciaDto>> ListarEquivalenciasAsync(
        int idProveedor,
        TipoEquivalenciaEnum? tipo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nombre de la marca activa, distinta de <paramref name="idMarcaExcluida"/>, que ya tiene el alias indicado
    /// (en forma de comparación), o <c>null</c> si ninguna lo tiene. Base de la unicidad entre agregados (sección 8.1).
    /// </summary>
    Task<string?> BuscarMarcaPorAliasAsync(string aliasNormalizado, int? idMarcaExcluida, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nombre de la marca activa, distinta de <paramref name="idMarcaExcluida"/>, cuyo nombre en forma de comparación
    /// (mayúsculas y sin diacríticos, incluida la ñ) es <paramref name="formaComparacion"/>, o <c>null</c>.
    /// </summary>
    Task<string?> BuscarMarcaPorNombreAsync(string formaComparacion, int? idMarcaExcluida, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nombre de la marca activa, distinta de <paramref name="idMarcaExcluida"/>, que ya tiene el prefijo indicado,
    /// o <c>null</c> si ninguna lo tiene.
    /// </summary>
    Task<string?> BuscarMarcaPorPrefijoAsync(string prefijo, int? idMarcaExcluida, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proveedores activos con equivalencias activas hacia la categoría o la marca indicada: los que la baja de ese
    /// destino debe cargar para quitarlas a través de la raíz (M-35, Ley 2).
    /// </summary>
    Task<IReadOnlyList<int>> ListarProveedoresConEquivalenciasHaciaAsync(
        TipoEquivalenciaEnum tipo,
        int idDestino,
        CancellationToken cancellationToken = default);
}
