using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Marca o fabricante de los productos comercializados en el comercio. Su nombre solo cambia a través de
/// <see cref="Crear"/> y <see cref="Renombrar"/>, que lo normalizan y validan con <see cref="ReglasTexto"/>
/// respetando la grafía propia de la marca ("3M", "BIC").
/// </summary>
public class Marca : BaseEntity, IAggregateRoot
{
    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    private Marca()
    {
    }

    public string NombreMarca { get; private set; } = string.Empty;

    public ICollection<Articulo> Articulos { get; private set; } = new List<Articulo>();

    /// <summary>
    /// Método de creación del agregado: es la única forma de dar de alta una marca.
    /// </summary>
    public static Marca Crear(string nombre)
    {
        var marca = new Marca();
        marca.Renombrar(nombre);
        return marca;
    }

    public void Renombrar(string nombre)
    {
        NombreMarca = ReglasTexto.ExigirNombreClasificacion(nombre, "nombre de la marca");
    }
}
