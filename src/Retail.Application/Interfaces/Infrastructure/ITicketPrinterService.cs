using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Listas;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Contrato para el despacho de impresión de tickets térmicos, comprobantes y recibos oficiales.
/// </summary>
public interface ITicketPrinterService
{
    Task ImprimirTicketVentaAsync(VentaResponseDto venta, CancellationToken cancellationToken = default);

    Task ImprimirReciboCobranzaAsync(CobranzaResultadoDto cobranza, CancellationToken cancellationToken = default);

    Task ImprimirActaArqueoAsync(ResultadoArqueoDto arqueo, CancellationToken cancellationToken = default);

    Task ImprimirPresupuestoAsync(PresupuestoDto presupuesto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imprime una lista de artículos con los precios de <paramref name="fechaPrecios"/>. Solo se imprimen los
    /// renglones disponibles (L-13).
    /// </summary>
    Task ImprimirListaAsync(ListaDto lista, DateOnly fechaPrecios, CancellationToken cancellationToken = default);
}
