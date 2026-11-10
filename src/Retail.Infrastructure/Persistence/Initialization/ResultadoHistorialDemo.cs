namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Lo que generó la simulación del historial de demostración.
/// </summary>
public sealed record ResultadoHistorialDemo(
    DateOnly Desde,
    DateOnly Hasta,
    int Articulos,
    int Clientes,
    int Turnos,
    int Ventas,
    int Compras,
    int Cobranzas);
