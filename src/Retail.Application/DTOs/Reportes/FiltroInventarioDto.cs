namespace Retail.Application.DTOs.Reportes;

/// <summary>Filtros de los reportes de inventario (stock crítico y valorización). No tienen período (RF-25).</summary>
public record class FiltroInventarioDto
{
    public int? IdCategoria { get; init; }

    public int? IdMarca { get; init; }
}
