using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Traduce un valor de la clasificación de un proveedor a una categoría o una marca del comercio (Track 6.6, M-13).
/// Si la equivalencia ya existe, reemplaza su destino; sin destino, el valor queda ignorado (M-32).
/// </summary>
public record class AsignarEquivalenciaDto
{
    public required int IdProveedor { get; init; }

    public required TipoEquivalenciaEnum Tipo { get; init; }

    /// <summary>
    /// Rubro del proveedor como contexto (M-31). Solo en las equivalencias de categoría.
    /// </summary>
    public string? TextoRubro { get; init; }

    public required string Texto { get; init; }

    /// <summary>
    /// Categoría o marca de destino, según <see cref="Tipo"/>. <c>null</c> ignora el valor.
    /// </summary>
    public int? IdDestino { get; init; }
}
