using Retail.Application.DTOs.Caja;
using Retail.Domain.Enums;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para el control del turno de caja, movimientos de tesorería y arqueo ciego.
/// </summary>
public interface ICajaService
{
    Task<TurnoCajaDto?> ObtenerTurnoActivoAsync(CancellationToken cancellationToken = default);

    Task<TurnoCajaDto> AbrirTurnoAsync(AperturaTurnoDto dto, CancellationToken cancellationToken = default);

    Task<TurnoCajaDto> RegistrarMovimientoAsync(MovimientoCajaDto dto, CancellationToken cancellationToken = default);

    Task<ResultadoArqueoDto> CerrarTurnoConArqueoCiegoAsync(ArqueoCiegoDto dto, CancellationToken cancellationToken = default);

    Task RegistrarIngresoCobranzaAsync(int idTurno, decimal monto, MedioPagoEnum medioPago, CancellationToken cancellationToken = default);

    Task RegistrarVentaEnCajaAsync(int idTurno, decimal totalEfectivo, decimal totalElectronico, CancellationToken cancellationToken = default);
}
