namespace Retail.Application.DTOs.Reportes;

/// <summary>Magnitud por la que se ordena el ranking de artículos (RF-24).</summary>
public enum CriterioRankingEnum
{
    /// <summary>Cantidad de unidades vendidas.</summary>
    Unidades,

    /// <summary>Ingreso neto generado.</summary>
    Importe
}
