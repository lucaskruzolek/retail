using Retail.Application.DTOs.Fiscal;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para la orquestación tributaria ante ARCA (AFIP), contingencia y reintento en lote.
/// </summary>
public interface IFiscalService
{
    Task<ComprobanteFiscalDto> EmitirComprobanteVentaAsync(int idVenta, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComprobanteFiscalDto>> ObtenerComprobantesPendientesReintentoAsync(CancellationToken cancellationToken = default);

    Task<ReintentoLoteResultadoDto> ReintentarLoteFiscalAsync(IReadOnlyList<int> idsComprobantes, CancellationToken cancellationToken = default);
}
