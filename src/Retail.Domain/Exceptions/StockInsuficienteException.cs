namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta vender o reservar una cantidad superior al stock físico disponible.
/// </summary>
public class StockInsuficienteException : DomainException
{
    public int ArticuloId { get; }
    public int StockActual { get; }
    public int CantidadSolicitada { get; }

    public StockInsuficienteException(int articuloId, int stockActual, int cantidadSolicitada)
        : base($"Stock insuficiente para el artículo ID {articuloId}. Stock actual: {stockActual}, solicitado: {cantidadSolicitada}.")
    {
        ArticuloId = articuloId;
        StockActual = stockActual;
        CantidadSolicitada = cantidadSolicitada;
    }

    public StockInsuficienteException(string message)
        : base(message)
    {
    }
}
