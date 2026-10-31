using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Fila de la pestaña de equivalencias de un proveedor (Track 6.6, Fase 6): la clave en forma de comparación y el
/// destino con su nombre.
/// </summary>
public record class EquivalenciaDto
{
    public required TipoEquivalenciaEnum Tipo { get; init; }

    public string? TextoRubro { get; init; }

    public required string Texto { get; init; }

    public int? IdCategoria { get; init; }

    public string? NombreCategoria { get; init; }

    public int? IdMarca { get; init; }

    public string? NombreMarca { get; init; }

    /// <summary>
    /// Se deriva igual que en la entidad (M-32): sin destino, el valor está ignorado.
    /// </summary>
    public bool EsIgnorada => IdCategoria is null && IdMarca is null;
}
