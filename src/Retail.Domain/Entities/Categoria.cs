using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Segundo nivel de la clasificación de artículos (Rubro → Categoría): el artículo se asigna a una categoría y su
/// rubro se deriva a través de ella. Su nombre solo cambia a través de
/// <see cref="Crear"/> y <see cref="Renombrar"/>, que lo normalizan y validan con <see cref="ReglasTexto"/>.
/// </summary>
public class Categoria : BaseEntity, IAggregateRoot
{
    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    private Categoria()
    {
    }

    public string NombreCategoria { get; private set; } = string.Empty;

    public ICollection<Articulo> Articulos { get; private set; } = new List<Articulo>();

    /// <summary>
    /// Método de creación del agregado: es la única forma de dar de alta una categoría.
    /// </summary>
    public static Categoria Crear(string nombre)
    {
        var categoria = new Categoria();
        categoria.Renombrar(nombre);
        return categoria;
    }

    public void Renombrar(string nombre)
    {
        NombreCategoria = ReglasTexto.ExigirNombreClasificacion(nombre, "nombre de la categoría");
    }
}
