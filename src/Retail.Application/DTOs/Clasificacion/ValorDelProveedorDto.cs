using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Valor distinto de rubro, categoría o marca que traen los ítems vigentes de un proveedor, tal como lo escribe, con
/// la cantidad de ítems que lo traen. Lo calcula SQL Server con GROUP BY (Ley 8); el servicio de aplicación decide
/// cuáles ya tienen equivalencia (M-76).
/// </summary>
public record class ValorDelProveedorDto
{
    public required TipoEquivalenciaEnum Tipo { get; init; }

    /// <summary>
    /// Rubro del proveedor, contexto de la categoría (M-31). Nulo para una marca o si la planilla no trae rubro.
    /// </summary>
    public string? Rubro { get; init; }

    /// <summary>
    /// Categoría o marca del proveedor. Nulo solo para una categoría cuando el ítem trae únicamente el rubro.
    /// </summary>
    public string? Texto { get; init; }

    public required int CantidadDeItems { get; init; }
}
