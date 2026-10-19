using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Reportes;

/// <summary>Una fila del reporte de rentabilidad (RF-25), con los importes ya redondeados.</summary>
public record class RentabilidadFilaDto : BaseDto
{
    public int? IdGrupo { get; init; }

    public string Grupo { get; init; } = string.Empty;

    public int Unidades { get; init; }

    public decimal IngresoNeto { get; init; }

    public decimal Costo { get; init; }

    /// <summary>Ingreso neto menos costo, ambos ya redondeados.</summary>
    public decimal MargenBruto { get; init; }

    /// <summary>Margen sobre el ingreso neto, en porcentaje; <c>null</c> si el ingreso fue cero.</summary>
    public decimal? MargenPorcentual { get; init; }
}
