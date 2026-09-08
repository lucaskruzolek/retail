using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clientes;

/// <summary>
/// Parámetros de entrada para el registro de un nuevo cliente.
/// </summary>
public record class CrearClienteDto
{
    public required string RazonSocialONombre { get; init; }
    public required TipoDocumentoEnum TipoDocumento { get; init; }
    public required string NumeroDocumento { get; init; }
    public required CondicionIvaEnum CondicionIva { get; init; }
    public string? DomicilioFiscal { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
    public bool TieneCuentaCorriente { get; init; }
    public decimal LimiteCredito { get; init; }
}
