using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clientes;

/// <summary>
/// Información completa de un cliente del comercio y su estado de cuenta corriente.
/// </summary>
public record class ClienteDto
{
    public required int IdCliente { get; init; }
    public required string RazonSocialONombre { get; init; }
    public required TipoDocumentoEnum TipoDocumento { get; init; }
    public required string NumeroDocumento { get; init; }
    public required CondicionIvaEnum CondicionIva { get; init; }
    public string? DomicilioFiscal { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
    public required bool TieneCuentaCorriente { get; init; }
    public required decimal LimiteCredito { get; init; }
    public required decimal SaldoCuentaCorriente { get; init; }
    public decimal CreditoDisponible => TieneCuentaCorriente ? Math.Max(0, LimiteCredito - SaldoCuentaCorriente) : 0;
}
