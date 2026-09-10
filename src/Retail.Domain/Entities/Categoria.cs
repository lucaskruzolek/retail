using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Clasificación de rubro o familia de artículos en el catálogo.
/// </summary>
public class Categoria : BaseEntity, IAggregateRoot
{
    public string NombreCategoria { get; set; } = string.Empty;

    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();
}
