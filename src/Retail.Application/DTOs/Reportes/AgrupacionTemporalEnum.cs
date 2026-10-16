namespace Retail.Application.DTOs.Reportes;

/// <summary>Granularidad con la que se agrupan las ventas de un período (RF-24).</summary>
public enum AgrupacionTemporalEnum
{
    /// <summary>Un punto por día calendario local.</summary>
    Dia,

    /// <summary>Un punto por semana local que empieza el lunes (D-10).</summary>
    Semana,

    /// <summary>Un punto por mes calendario local.</summary>
    Mes
}
