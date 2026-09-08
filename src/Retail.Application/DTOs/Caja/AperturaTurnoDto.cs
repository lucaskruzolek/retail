namespace Retail.Application.DTOs.Caja;

/// <summary>
/// Parámetros para la apertura de un nuevo turno de caja en mostrador.
/// </summary>
public record class AperturaTurnoDto
{
    public required int IdUsuario { get; init; }
    public required decimal SaldoInicial { get; init; }
}
