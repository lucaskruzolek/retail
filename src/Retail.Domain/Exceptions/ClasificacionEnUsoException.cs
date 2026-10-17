namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta dar de baja un rubro con categorías activas, o una categoría o marca con
/// artículos asignados: la baja dejaría artículos o categorías huérfanos.
/// </summary>
public class ClasificacionEnUsoException : DomainException
{
    public string Tipo { get; }
    public string Nombre { get; }
    public int CantidadEnUso { get; }
    public string Dependientes { get; }

    /// <param name="tipo">Elemento con su artículo: "el rubro", "la categoría" o "la marca".</param>
    /// <param name="nombre">Nombre del elemento que se intentó dar de baja.</param>
    /// <param name="cantidadEnUso">Cantidad de elementos que lo referencian.</param>
    /// <param name="dependientes">Qué lo referencia, en plural: "artículos" o "categorías activas".</param>
    public ClasificacionEnUsoException(string tipo, string nombre, int cantidadEnUso, string dependientes)
        : base($"No se puede dar de baja {tipo} '{nombre}': está en uso por {cantidadEnUso} {dependientes}.")
    {
        Tipo = tipo;
        Nombre = nombre;
        CantidadEnUso = cantidadEnUso;
        Dependientes = dependientes;
    }
}
