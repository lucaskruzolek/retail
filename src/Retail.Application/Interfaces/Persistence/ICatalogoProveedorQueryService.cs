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

    Task<CatalogoProveedor?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogoProveedor>> ObtenerPorIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, CatalogoProveedor>> ObtenerMapaPorCodigosProveedorAsync(int idProveedor, IEnumerable<string> codigos, CancellationToken cancellationToken = default);

    Task AgregarAsync(CatalogoProveedor catalogo, CancellationToken cancellationToken = default);

    Task ActualizarAsync(CatalogoProveedor catalogo, CancellationToken cancellationToken = default);
}
