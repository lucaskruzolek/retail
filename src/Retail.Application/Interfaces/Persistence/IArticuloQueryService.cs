using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Ventas;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Contrato especializado para consultas y paginación del catálogo de artículos con push-down a SQL Server (Ley 8).
/// </summary>
public interface IArticuloQueryService
{
    Task<ArticulosPaginadosDto> ObtenerArticulosPaginadosAsync(ConsultaArticulosDto consulta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Artículo activo con ese código de barras exacto, o <c>null</c> (lector de códigos del POS).
    /// </summary>
    Task<ArticuloVentaDto?> ObtenerParaVentaPorCodigoBarrasAsync(string codigoBarras, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hasta <paramref name="limite"/> artículos activos cuya descripción o código de barras contienen el término,
    /// ordenados por descripción. El filtro, el orden y el límite se resuelven en SQL Server.
    /// </summary>
    Task<IReadOnlyList<ArticuloVentaDto>> BuscarParaVentaAsync(string termino, int limite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Precio, stock y datos del origen de los artículos activos indicados. Los dados de baja no se devuelven.
    /// </summary>
    Task<IReadOnlyList<DisponibilidadArticuloDto>> ObtenerDisponibilidadAsync(IReadOnlyCollection<int> idsArticulos, CancellationToken cancellationToken = default);
}
