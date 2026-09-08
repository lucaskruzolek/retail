using Retail.Application.DTOs.Presupuestos;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para la emisión de presupuestos independientes, control de vigencia (15 días) y conversión en mostrador.
/// </summary>
public interface IPresupuestoService
{
    Task<PresupuestoDto> CrearPresupuestoAsync(CrearPresupuestoDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PresupuestoDto>> ListarPresupuestosAsync(CancellationToken cancellationToken = default);

    Task<PresupuestoDto?> ObtenerPresupuestoPorIdAsync(int idPresupuesto, CancellationToken cancellationToken = default);

    Task<PresupuestoParaVentaDto> RecuperarPresupuestoParaVentaAsync(int idPresupuesto, CancellationToken cancellationToken = default);

    Task MarcarComoConvertidoAsync(int idPresupuesto, CancellationToken cancellationToken = default);
}
