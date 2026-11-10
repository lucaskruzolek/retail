namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Una presentación derivada del catálogo de demostración (RF-21): la unidad suelta de un pack.
/// </summary>
internal sealed record PresentacionDemo(
    string DescripcionOrigen,
    string Descripcion,
    int UnidadesPorOrigen,
    decimal PorcentajeGanancia);
