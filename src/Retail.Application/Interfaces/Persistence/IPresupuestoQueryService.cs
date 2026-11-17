using Retail.Application.DTOs.Presupuestos;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Consultas de solo lectura sobre presupuestos: proyecciones <c>AsNoTracking</c> a DTOs, evaluadas en SQL Server
/// (Ley 8). Las modificaciones pasan siempre por la raíz <c>Presupuesto</c> (Ley 2).
/// </summary>
public interface IPresupuestoQueryService
{
    /// <summary>
    /// Página de presupuestos. <paramref name="hoy"/> es la fecha calendario local que calcula el servicio: la consulta
    /// la usa para <c>EstaVigente</c> y para los filtros Vigentes y Vencidos, que se resuelven en SQL.
    /// </summary>
    Task<PresupuestosPaginadosDto> ListarAsync(ConsultaPresupuestosDto consulta, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary><c>null</c> si el presupuesto no existe. <paramref name="hoy"/> alimenta <c>EstaVigente</c>.</summary>
    Task<PresupuestoDto?> ObtenerAsync(int idPresupuesto, DateOnly hoy, CancellationToken cancellationToken = default);
}
