using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Regla "término ambiguo + segunda palabra" del motor de catalogación (Track 6.6, Fase 5, M-48): "SET" por sí solo no
/// alcanza para decidir, pero "SET GEOMETRIA" es Geometría. Entidad interna del agregado
/// <see cref="TerminoClasificacion"/>: solo se crea y se modifica con sus métodos <c>AsignarPar</c> y <c>QuitarPar</c>.
/// </summary>
public class ParClasificacion : BaseEntity
{
    // Lo usa EF Core para materializar la entidad desde la base.
    protected ParClasificacion()
    {
    }

    /// <summary>
    /// Recibe la segunda palabra ya normalizada y validada por <see cref="TerminoClasificacion"/>.
    /// </summary>
    internal ParClasificacion(string segundaPalabra)
    {
        SegundaPalabra = segundaPalabra;
    }

    public int IdTermino { get; private set; }

    public TerminoClasificacion? TerminoClasificacion { get; private set; }

    /// <summary>
    /// Forma de comparación (<see cref="ReglasClasificacion.ExigirSegundaPalabra"/>), la misma que devuelve
    /// <see cref="ReglasClasificacion.SegundaPalabra"/> para una descripción.
    /// </summary>
    public string SegundaPalabra { get; private set; } = string.Empty;

    /// <summary>
    /// Categoría que sugiere el par. Se referencia solo por Id, como el término (D-13).
    /// </summary>
    public int? IdCategoria { get; private set; }

    /// <summary>
    /// Sin categoría: el par ya se revisó y no es una regla. No sugiere y no se vuelve a proponer (P-4), con el mismo
    /// patrón que el término ambiguo (M-27) y la equivalencia ignorada (M-32).
    /// </summary>
    public bool EsIgnorado => !IdCategoria.HasValue;

    /// <summary>
    /// El destino ya viene validado por <see cref="TerminoClasificacion"/>.
    /// </summary>
    internal void CambiarDestino(int? idCategoria)
    {
        IdCategoria = idCategoria;
    }
}
