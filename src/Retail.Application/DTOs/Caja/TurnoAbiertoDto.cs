namespace Retail.Application.DTOs.Caja;

/// <summary>
/// Un turno abierto en alguna terminal, tal como lo ve un Gerente que puede cerrarlo. Los tres importes
/// "leídos" (saldo teórico, total electrónico y cantidad de ventas) viajan de vuelta en el cierre: son la versión del
/// turno que vio quien cierra, y el dominio rechaza el cierre si cambiaron (control optimista fuera de línea).
/// </summary>
public record class TurnoAbiertoDto
{
    public required int IdTurno { get; init; }
    public required int IdTerminal { get; init; }
    public required string NombreTerminal { get; init; }
    public required int IdUsuario { get; init; }
    public required string NombreCajero { get; init; }
    public required DateTime FechaApertura { get; init; }
    public required decimal SaldoTeoricoEfectivo { get; init; }
    public required decimal TotalVentasElectronicas { get; init; }
    public required int CantidadVentas { get; init; }
}
