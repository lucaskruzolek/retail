using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Equivalencia del proveedor con la clave en forma de comparación (M-31). Sin destino, está ignorada (M-32).
/// </summary>
public record class EquivalenciaDiccionarioDto
{
    public required TipoEquivalenciaEnum Tipo { get; init; }

    public string? TextoRubro { get; init; }

    public required string Texto { get; init; }

    public int? IdCategoria { get; init; }

    public int? IdMarca { get; init; }
}
