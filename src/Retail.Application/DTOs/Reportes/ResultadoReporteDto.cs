using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Reportes;

/// <summary>
/// Base de lo que la pantalla muestra de un reporte. Exportar un reporte (RF-26) toma exactamente una instancia de
/// este tipo, sin volver a consultar la base.
/// </summary>
public abstract record class ResultadoReporteDto : BaseDto
{
    /// <summary>Fecha y hora local en que se generó el reporte.</summary>
    public DateTime GeneradoEl { get; init; }

    /// <summary>Título del reporte, usado en pantalla y en el documento exportado.</summary>
    public abstract string Titulo { get; }
}
