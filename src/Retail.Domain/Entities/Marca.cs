using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Marca o fabricante de los productos comercializados en el comercio. Su nombre solo cambia a través de
/// <see cref="Crear"/> y <see cref="Renombrar"/>, que lo normalizan y validan con <see cref="ReglasTexto"/>
/// respetando la grafía propia de la marca ("3M", "BIC").
/// </summary>
/// <remarks>
/// Custodia además sus diccionarios del motor de catalogación (Track 6.6): los alias con que la escriben los
/// proveedores y los prefijos EAN de sus titulares. Que un alias o un prefijo pertenezca a una sola marca cruza
/// agregados: lo verifica la capa de Aplicación y lo garantiza un índice único.
/// </remarks>
public class Marca : BaseEntity, IAggregateRoot
{
    private readonly List<AliasMarca> _alias = new();
    private readonly List<PrefijoEanMarca> _prefijos = new();

    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    private Marca()
    {
    }

    public string NombreMarca { get; private set; } = string.Empty;

    public ICollection<Articulo> Articulos { get; private set; } = new List<Articulo>();

    /// <summary>
    /// Alias vigentes y dados de baja en esta unidad de trabajo; los dados de baja no cuentan para las reglas.
    /// </summary>
    public IReadOnlyCollection<AliasMarca> Alias => _alias.AsReadOnly();

    public IReadOnlyCollection<PrefijoEanMarca> Prefijos => _prefijos.AsReadOnly();

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
        var normalizado = ReglasTexto.ExigirNombreClasificacion(nombre, "nombre de la marca");
        var formaDeComparacion = ReglasClasificacion.NormalizarTexto(normalizado);
        if (BuscarAliasVigente(formaDeComparacion) is not null)
        {
            throw new DomainException(
                $"La marca ya tiene el alias '{formaDeComparacion}': quítelo antes de usarlo como nombre.");
        }

        NombreMarca = normalizado;
    }

    /// <summary>
    /// Registra otra forma de escribir la marca. Un alias igual al nombre no aporta nada: el motor ya busca el nombre.
    /// </summary>
    public AliasMarca AgregarAlias(string alias)
    {
        var normalizado = ReglasClasificacion.ExigirAlias(alias);
        if (normalizado == ReglasClasificacion.NormalizarTexto(NombreMarca))
        {
            throw new DomainException($"El alias '{normalizado}' es el nombre de la marca.");
        }

        if (BuscarAliasVigente(normalizado) is not null)
        {
            throw new DomainException($"La marca ya tiene el alias '{normalizado}'.");
        }

        var nuevo = new AliasMarca(normalizado);
        _alias.Add(nuevo);
        return nuevo;
    }

    /// <summary>
    /// Da de baja el alias (borrado lógico, Ley 3). El texto se compara en su forma normalizada.
    /// </summary>
    public void QuitarAlias(string alias)
    {
        var normalizado = ReglasClasificacion.ExigirAlias(alias);
        var existente = BuscarAliasVigente(normalizado)
            ?? throw new DomainException($"La marca no tiene el alias '{normalizado}'.");
        existente.MarkAsDeleted();
    }

    public PrefijoEanMarca AgregarPrefijo(string prefijo)
    {
        var normalizado = ReglasCodigoBarras.ExigirPrefijoTitular(prefijo);
        if (BuscarPrefijoVigente(normalizado) is not null)
        {
            throw new DomainException($"La marca ya tiene el prefijo '{normalizado}'.");
        }

        var nuevo = new PrefijoEanMarca(normalizado);
        _prefijos.Add(nuevo);
        return nuevo;
    }

    public void QuitarPrefijo(string prefijo)
    {
        var normalizado = ReglasCodigoBarras.ExigirPrefijoTitular(prefijo);
        var existente = BuscarPrefijoVigente(normalizado)
            ?? throw new DomainException($"La marca no tiene el prefijo '{normalizado}'.");
        existente.MarkAsDeleted();
    }

    /// <summary>
    /// La baja de la marca da de baja también sus alias y prefijos, para que queden libres para otra marca. Requiere
    /// que el agregado se haya cargado con ellos.
    /// </summary>
    public override void MarkAsDeleted()
    {
        foreach (var alias in _alias.Where(a => !a.IsDeleted))
        {
            alias.MarkAsDeleted();
        }

        foreach (var prefijo in _prefijos.Where(p => !p.IsDeleted))
        {
            prefijo.MarkAsDeleted();
        }

        base.MarkAsDeleted();
    }

    private AliasMarca? BuscarAliasVigente(string aliasNormalizado)
    {
        return _alias.FirstOrDefault(a => !a.IsDeleted && a.Alias == aliasNormalizado);
    }

    private PrefijoEanMarca? BuscarPrefijoVigente(string prefijoNormalizado)
    {
        return _prefijos.FirstOrDefault(p => !p.IsDeleted && p.Prefijo == prefijoNormalizado);
    }
}
