using Retail.Application.DTOs.Fiscal;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Contrato de comunicación HTTP hacia el microservicio fiscal local arcasdk.
/// </summary>
public interface IArcaClient
{
    Task<RespuestaCaeDto> SolicitarCaeAsync(SolicitudCaeDto solicitud, CancellationToken cancellationToken = default);

    Task<bool> VerificarSaludServicioAsync(CancellationToken cancellationToken = default);
}
