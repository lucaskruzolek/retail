using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Proveedores;
using Retail.Domain.Entities;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Contrato especializado para consultas y persistencia del catálogo de distribuidores (Ley 2 y Ley 8).
/// Permite push-down a SQL Server y streaming sin violar los límites de agregados.
/// </summary>
public interface ICatalogoProveedorQueryService
{
    Task<CatalogoPaginadoDto> ObtenerCatalogoPaginadoAsync(ConsultaCatalogoProveedorDto consulta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ítems vigentes del proveedor para clasificar (Track 6.6, M-75): todos o solo los pendientes (sin fecha de
    /// clasificación). Vuelven con tracking porque se cargan para modificarlos.
    /// </summary>
    Task<IReadOnlyList<CatalogoProveedor>> ObtenerParaClasificarAsync(int idProveedor, bool soloPendientes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pares distintos de rubro y categoría, y marcas distintas, de los ítems vigentes del proveedor, con la cantidad
    /// de ítems de cada uno (M-76). Se agrupa en SQL Server.
    /// </summary>
    Task<IReadOnlyList<ValorDelProveedorDto>> ObtenerValoresDeClasificacionAsync(int idProveedor, CancellationToken cancellationToken = default);

    Task<CatalogoProveedor?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogoProveedor>> ObtenerPorIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ítems vigentes que son fuente del artículo, principal o adicional (Track 4.3, A-16). Vuelven con tracking
    /// porque se cargan para modificarlos (J3).
    /// </summary>
    Task<IReadOnlyList<CatalogoProveedor>> ObtenerFuentesDeArticuloAsync(int idArticulo, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, CatalogoProveedor>> ObtenerMapaPorCodigosProveedorAsync(int idProveedor, IEnumerable<string> codigos, CancellationToken cancellationToken = default);

    Task AgregarAsync(CatalogoProveedor catalogo, CancellationToken cancellationToken = default);

    Task ActualizarAsync(CatalogoProveedor catalogo, CancellationToken cancellationToken = default);
}
