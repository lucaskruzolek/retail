using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clientes;

/// <summary>
/// Parámetros para registrar el cobro de deuda de cuenta corriente de un cliente.
/// </summary>
public record class RegistrarCobranzaDto
{
    public required int IdCliente { get; init; }
    public required int IdTurno { get; init; }
    public required int IdUsuario { get; init; }
    public required MedioPagoEnum MedioPago { get; init; }
    public required decimal Monto { get; init; }
    public string? Referencia { get; init; }
}
