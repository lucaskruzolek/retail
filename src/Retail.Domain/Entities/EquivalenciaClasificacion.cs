using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Regla de traducción de un proveedor (Track 6.6, M-13): un valor de su clasificación ("ESCOLAR / CUADERNOS T/D")
/// corresponde a una categoría o una marca del comercio, o se ignora. Se decide una vez por valor distinto y se
/// reutiliza en cada importación de ese proveedor; no es la clasificación de cada ítem. Entidad interna del agregado
/// <see cref="Proveedor"/>: solo se crea y se modifica con sus métodos <c>AsignarEquivalencia…</c>.
/// </summary>
public class EquivalenciaClasificacion : BaseEntity
{
    // Lo usa EF Core para materializar la entidad desde la base.
    protected EquivalenciaClasificacion()
    {
    }

    /// <summary>
    /// Recibe la clave ya normalizada y validada por <see cref="Proveedor"/>.
    /// </summary>
    internal EquivalenciaClasificacion(TipoEquivalenciaEnum tipo, string? textoRubro, string texto)
    {
        Tipo = tipo;
        TextoRubro = textoRubro;
        Texto = texto;
    }

    public int IdProveedor { get; private set; }

    public Proveedor? Proveedor { get; private set; }

    public TipoEquivalenciaEnum Tipo { get; private set; }

    /// <summary>
    /// Rubro del proveedor como contexto de una equivalencia de categoría (M-31); nulo en las de marca o cuando la
    /// planilla no lo trae. Forma de comparación (<see cref="ReglasClasificacion.ExigirTextoDeProveedor"/>).
    /// </summary>
    public string? TextoRubro { get; private set; }

    /// <summary>
    /// Categoría del proveedor (o su rubro, si es lo único que trae) o marca, en forma de comparación.
    /// </summary>
    public string Texto { get; private set; } = string.Empty;

    public int? IdCategoria { get; private set; }

    public int? IdMarca { get; private set; }

    /// <summary>
    /// Sin destino: el valor ya se revisó y no se usa como pista; la clasificación sigue con los otros niveles (M-32).
    /// </summary>
    public bool EsIgnorada => !IdCategoria.HasValue && !IdMarca.HasValue;

    internal bool TieneClave(TipoEquivalenciaEnum tipo, string? textoRubro, string texto)
    {
        return Tipo == tipo && TextoRubro == textoRubro && Texto == texto;
    }

    /// <summary>
    /// El destino ya viene validado por <see cref="Proveedor"/> según el tipo.
    /// </summary>
    internal void CambiarDestino(int? idCategoria, int? idMarca)
    {
        IdCategoria = idCategoria;
        IdMarca = idMarca;
    }
}
