using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Agrupación de primer nivel del catálogo (por ejemplo "Librería" o "Papelería"). Su nombre solo cambia a través
/// de <see cref="Crear"/> y <see cref="Renombrar"/>, que lo normalizan y validan con <see cref="ReglasTexto"/>.
/// </summary>
public class Rubro : BaseEntity, IAggregateRoot
{
    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    private Rubro()
    {
    }

    public string NombreRubro { get; private set; } = string.Empty;

    /// <summary>
    /// Método de creación del agregado: es la única forma de dar de alta un rubro.
    /// </summary>
    public static Rubro Crear(string nombre)
    {
        var rubro = new Rubro();
        rubro.Renombrar(nombre);
        return rubro;
    }

    public void Renombrar(string nombre)
    {
        NombreRubro = ReglasTexto.ExigirNombreClasificacion(nombre, "nombre del rubro");
    }
}
