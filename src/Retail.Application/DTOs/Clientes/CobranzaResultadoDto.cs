using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clientes;

/// <summary>
/// Comprobante de resultado tras registrar formalmente una cobranza.
/// </summary>
public record class CobranzaResultadoDto
{
    public required int IdCobranza { get; init; }
    public required int IdCliente { get; init; }
    public required string ClienteNombre { get; init; }
    public required DateTime FechaHora { get; init; }
    public required MedioPagoEnum MedioPago { get; init; }
    public required decimal MontoAbonado { get; init; }
    public required decimal SaldoAnterior { get; init; }
    public required decimal NuevoSaldo { get; init; }
    public string? Referencia { get; init; }
}
