using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Otra forma en que las descripciones de los proveedores escriben una marca ("HUSAR" por "HUSARES"). Entidad interna
/// del agregado <see cref="Marca"/>: solo se crea con <see cref="Marca.AgregarAlias"/> (Track 6.6).
/// </summary>
public class AliasMarca : BaseEntity
{
    // Lo usa EF Core para materializar la entidad desde la base.
    protected AliasMarca()
    {
    }

    /// <summary>
    /// Recibe el alias ya normalizado y validado por <see cref="Marca.AgregarAlias"/>.
    /// </summary>
    internal AliasMarca(string aliasNormalizado)
    {
        Alias = aliasNormalizado;
    }

    public int IdMarca { get; private set; }

    public Marca? Marca { get; private set; }

    /// <summary>
    /// Forma de comparación del alias (<see cref="ReglasClasificacion.ExigirAlias"/>).
    /// </summary>
    public string Alias { get; private set; } = string.Empty;
}
