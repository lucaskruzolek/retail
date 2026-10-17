using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Segundo nivel de la clasificación de artículos (Rubro → Categoría): el artículo se asigna a una categoría y su
/// rubro se deriva a través de ella. Su nombre solo cambia a través de <see cref="Crear"/> y
/// <see cref="Renombrar"/>, que lo normalizan y validan con <see cref="ReglasTexto"/>, y su rubro a través de
/// <see cref="Crear"/> y <see cref="MoverARubro"/>. Referencia al rubro solo por Id: son dos raíces de agregado
/// distintas (D-13), por eso no hay navegación.
/// </summary>
public class Categoria : BaseEntity, IAggregateRoot
{
    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    private Categoria()
    {
    }

    public string NombreCategoria { get; private set; } = string.Empty;

    public int IdRubro { get; private set; }

    public ICollection<Articulo> Articulos { get; private set; } = new List<Articulo>();

    /// <summary>
    /// Método de creación del agregado: es la única forma de dar de alta una categoría.
    /// </summary>
    public static Categoria Crear(string nombre, int idRubro)
    {
        var categoria = new Categoria();
        categoria.Renombrar(nombre);
        categoria.MoverARubro(idRubro);
        return categoria;
    }

    public void Renombrar(string nombre)
    {
        NombreCategoria = ReglasTexto.ExigirNombreClasificacion(nombre, "nombre de la categoría");
    }

    /// <summary>
    /// Cambia el rubro al que pertenece la categoría. Que el rubro exista y esté activo lo verifica la capa de
    /// Aplicación; acá solo se exige un identificador válido.
    /// </summary>
    public void MoverARubro(int idRubro)
    {
        if (idRubro <= 0)
        {
            throw new DomainException("El rubro de la categoría no es válido.");
        }

        IdRubro = idRubro;
    }
}
