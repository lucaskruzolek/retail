using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Entrada de la tabla de términos del motor de catalogación (Track 6.6, M-27): la primera palabra de una descripción
/// ("MOCHILA") asociada a la categoría que sugiere, o marcada como ambigua ("SET", "REPUESTO") cuando por sí sola no
/// alcanza para decidir y el producto se resuelve con sus pares (<see cref="ParClasificacion"/>). Guardar los ambiguos
/// importa: un término ambiguo es conocido, y no se trata como un término nuevo en la decisión en bloque de la
/// incorporación (M-26).
/// </summary>
/// <remarks>
/// Es una raíz de agregado propia, no un hijo de <see cref="Categoria"/>: un término ambiguo no tiene categoría a la
/// que pertenecer. Referencia a la categoría solo por Id, como <see cref="Categoria"/> al rubro (D-13). Que un término
/// figure una sola vez en todo el catálogo lo verifica la capa de Aplicación y lo garantiza un índice único.
/// Un término solo tiene pares mientras es ambiguo (P-3): con categoría, el motor nunca los consultaría.
/// </remarks>
public class TerminoClasificacion : BaseEntity, IAggregateRoot
{
    private readonly List<ParClasificacion> _pares = new();

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

    /// <summary>
    /// Pares "término + segunda palabra" (Track 6.6, Fase 5), vigentes y dados de baja en esta unidad de trabajo; los
    /// dados de baja no cuentan para las reglas. Persistencia los carga siempre con el término (M-57), porque las bajas
    /// en cascada de <see cref="AsignarCategoria"/> y <see cref="MarkAsDeleted"/> dependen de tenerlos en memoria.
    /// </summary>
    public IReadOnlyCollection<ParClasificacion> Pares => _pares.AsReadOnly();

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
    /// Asocia el término a una categoría; si era ambiguo, deja de serlo y sus pares se dan de baja (P-3). Que la
    /// categoría exista y esté activa lo verifica la capa de Aplicación.
    /// </summary>
    public void AsignarCategoria(int idCategoria)
    {
        if (idCategoria <= 0)
        {
            throw new DomainException("La categoría del término no es válida.");
        }

        IdCategoria = idCategoria;
        DarDeBaja(_pares.Where(p => !p.IsDeleted));
    }

    public void MarcarAmbiguo()
    {
        IdCategoria = null;
    }

    /// <summary>
    /// Registra que el término seguido de <paramref name="segundaPalabra"/> sugiere una categoría, o lo ignora si
    /// <paramref name="idCategoria"/> es nulo (P-4). Si el par ya existe, reemplaza su destino (como M-32). Que la
    /// categoría exista y esté activa lo verifica la capa de Aplicación.
    /// </summary>
    public ParClasificacion AsignarPar(string segundaPalabra, int? idCategoria)
    {
        // Se valida todo antes de modificar el agregado.
        if (idCategoria.HasValue && idCategoria.Value <= 0)
        {
            throw new DomainException("La categoría del par no es válida.");
        }

        if (!EsAmbiguo)
        {
            throw new DomainException(
                $"El término '{Termino}' ya sugiere una categoría: solo un término ambiguo puede tener pares.");
        }

        var forma = ReglasClasificacion.ExigirSegundaPalabra(segundaPalabra);

        var par = BuscarParVigente(forma);
        if (par is null)
        {
            par = new ParClasificacion(forma);
            _pares.Add(par);
        }

        par.CambiarDestino(idCategoria);
        return par;
    }

    /// <summary>
    /// Da de baja el par (borrado lógico, Ley 3): el par vuelve a poder proponerse a partir de los artículos.
    /// </summary>
    public void QuitarPar(string segundaPalabra)
    {
        var forma = ReglasClasificacion.ExigirSegundaPalabra(segundaPalabra);
        var existente = BuscarParVigente(forma)
            ?? throw new DomainException($"El término '{Termino}' no tiene un par con '{forma}'.");
        existente.MarkAsDeleted();
    }

    /// <summary>
    /// Da de baja los pares vigentes que sugieren la categoría indicada (M-35), como
    /// <see cref="Proveedor.QuitarEquivalenciasHacia"/>. Los ignorados no apuntan a ninguna categoría y no se tocan.
    /// Devuelve cuántos se dieron de baja.
    /// </summary>
    public int QuitarParesHacia(int idCategoria)
    {
        if (idCategoria <= 0)
        {
            throw new DomainException("La categoría de los pares no es válida.");
        }

        var afectados = _pares.Where(p => !p.IsDeleted && p.IdCategoria == idCategoria).ToList();
        DarDeBaja(afectados);
        return afectados.Count;
    }

    /// <summary>
    /// La baja del término da de baja también sus pares (P-3), como la de una marca sus alias y prefijos.
    /// </summary>
    public override void MarkAsDeleted()
    {
        DarDeBaja(_pares.Where(p => !p.IsDeleted));
        base.MarkAsDeleted();
    }

    private ParClasificacion? BuscarParVigente(string forma)
    {
        return _pares.FirstOrDefault(p => !p.IsDeleted && p.SegundaPalabra == forma);
    }

    private static void DarDeBaja(IEnumerable<ParClasificacion> pares)
    {
        foreach (var par in pares)
        {
            par.MarkAsDeleted();
        }
    }
}
