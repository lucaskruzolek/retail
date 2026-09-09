using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clientes;

/// <summary>
/// Registro histórico de pago o cobranza efectuada por un cliente sobre su cuenta corriente comercial.
/// </summary>
public record class CobranzaHistorialDto
{
    public required int IdCobranza { get; init; }
    public required int IdCliente { get; init; }
    public required int IdTurno { get; init; }
    public required int IdUsuario { get; init; }
    public string? UsuarioNombre { get; init; }
    public required DateTime FechaHora { get; init; }
    public required MedioPagoEnum MedioPago { get; init; }
    public required decimal Monto { get; init; }
    public string? Referencia { get; init; }
}
