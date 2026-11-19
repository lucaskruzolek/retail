using Retail.Application.DTOs.Listas;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Consultas de solo lectura sobre listas de artículos: proyecciones <c>AsNoTracking</c> a DTOs, evaluadas en SQL
/// Server (Ley 8). Los totales usan el precio de catálogo de hoy y se calculan en SQL; la cantidad de ventas de cada
/// lista también se cuenta en SQL y no se almacena (L-09). Las modificaciones pasan siempre por la raíz
/// <c>ListaArticulos</c> (Ley 2).
/// </summary>
public interface IListaQueryService
{
    /// <summary>Página de listas activas, filtradas por nombre y ordenadas en el servidor.</summary>
    Task<ListasPaginadosDto> ListarAsync(ConsultaListasDto consulta, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>null</c> si la lista no existe o fue dada de baja. Incluye los renglones cuyo artículo fue dado de baja,
    /// marcados con <c>EstaDisponible = false</c>.
    /// </summary>
    Task<ListaDto?> ObtenerAsync(int idLista, CancellationToken cancellationToken = default);
}
