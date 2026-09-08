using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clientes;

/// <summary>
/// Parámetros para modificar datos personales y comerciales de un cliente existente.
/// </summary>
public record class ActualizarClienteDto
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
}
