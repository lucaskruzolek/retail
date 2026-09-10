using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Marca o fabricante de los productos comercializados en el comercio.
/// </summary>
public class Marca : BaseEntity, IAggregateRoot
{
    public string NombreMarca { get; set; } = string.Empty;

    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();
}
