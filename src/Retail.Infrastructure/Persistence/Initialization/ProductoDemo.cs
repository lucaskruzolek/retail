namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Un artículo de compra o un servicio del catálogo de demostración, con su costo inicial y su margen.
/// </summary>
internal sealed record ProductoDemo(
    string Descripcion,
    string Categoria,
    string? Marca,
    decimal CostoInicial,
    decimal PorcentajeGanancia,
    bool EsServicio = false);
