using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Entrada de la tabla de términos del motor de catalogación (Track 6.6, M-27): la primera palabra de una descripción
/// ("MOCHILA") asociada a la categoría que sugiere, o marcada como ambigua ("SET", "REPUESTO") cuando por sí sola no
/// alcanza para decidir y el producto pasa al modelo. Guardar los ambiguos importa: un término ambiguo es conocido,
/// y no se trata como un término nuevo en la decisión en bloque de la incorporación (M-26).
/// </summary>
/// <remarks>
/// Es una raíz de agregado propia, no un hijo de <see cref="Categoria"/>: un término ambiguo no tiene categoría a la
/// que pertenecer. Referencia a la categoría solo por Id, como <see cref="Categoria"/> al rubro (D-13). Que un término
/// figure una sola vez en todo el catálogo lo verifica la capa de Aplicación y lo garantiza un índice único.
/// </remarks>
public class TerminoClasificacion : BaseEntity, IAggregateRoot
{
    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa los métodos de creación.
    private TerminoClasificacion()
    {
    }

    /// <summary>
    /// Forma de comparación del término (<see cref="ReglasClasificacion.ExigirTermino"/>). No cambia: corregir un
    /// término es dar de baja el anterior y crear otro.
    /// </summary>
    public string Termino { get; private set; } = string.Empty;

    /// <summary>
    /// Categoría que sugiere el término. <c>null</c> significa que el término es ambiguo.
    /// </summary>
    public int? IdCategoria { get; private set; }

    /// <summary>
    /// Se deriva de la categoría para que un término no pueda tener categoría y ser ambiguo a la vez, ni ninguna de
    /// las dos cosas.
    /// </summary>
    public bool EsAmbiguo => !IdCategoria.HasValue;

    public static TerminoClasificacion CrearConCategoria(string termino, int idCategoria)
    {
        var entrada = new TerminoClasificacion { Termino = ReglasClasificacion.ExigirTermino(termino) };
        entrada.AsignarCategoria(idCategoria);
        return entrada;
    }

    public static TerminoClasificacion CrearAmbiguo(string termino)
    {
        return new TerminoClasificacion { Termino = ReglasClasificacion.ExigirTermino(termino) };
    }

    /// <summary>
    /// Asocia el término a una categoría; si era ambiguo, deja de serlo. Que la categoría exista y esté activa lo
    /// verifica la capa de Aplicación.
    /// </summary>
    public void AsignarCategoria(int idCategoria)
    {
        if (idCategoria <= 0)
        {
            throw new DomainException("La categoría del término no es válida.");
        }

        IdCategoria = idCategoria;
    }

    public void MarcarAmbiguo()
    {
        IdCategoria = null;
    }
}
