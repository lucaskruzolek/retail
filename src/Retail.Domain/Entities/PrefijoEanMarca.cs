using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Prefijo GS1 del titular de los códigos de barras de una marca, que sugiere la marca cuando la descripción no la
/// nombra (Track 6.6, M-25). Entidad interna del agregado <see cref="Marca"/>: solo se crea con
/// <see cref="Marca.AgregarPrefijo"/>.
/// </summary>
public class PrefijoEanMarca : BaseEntity
{
    // Lo usa EF Core para materializar la entidad desde la base.
    protected PrefijoEanMarca()
    {
    }

    /// <summary>
    /// Recibe el prefijo ya validado por <see cref="Marca.AgregarPrefijo"/>.
    /// </summary>
    internal PrefijoEanMarca(string prefijoNormalizado)
    {
        Prefijo = prefijoNormalizado;
    }

    public int IdMarca { get; private set; }

    public Marca? Marca { get; private set; }

    /// <summary>
    /// Siempre <see cref="ReglasCodigoBarras.LongitudPrefijoTitular"/> dígitos (M-28).
    /// </summary>
    public string Prefijo { get; private set; } = string.Empty;
}
