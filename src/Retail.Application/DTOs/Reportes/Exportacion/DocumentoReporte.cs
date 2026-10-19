namespace Retail.Application.DTOs.Reportes.Exportacion;

/// <summary>
/// Modelo neutro de exportación (D-12): lo único que conoce un <c>IExportadorReportes</c>. Application no depende de
/// MiniExcel ni de QuestPDF; cada formato tiene un exportador y no uno por reporte.
/// </summary>
/// <param name="GeneradoEl">Fecha y hora local de generación.</param>
/// <param name="GeneradoPor">Nombre del Gerente que lo generó.</param>
public record DocumentoReporte(
    string Titulo,
    IReadOnlyList<FiltroAplicado> Filtros,
    DateTime GeneradoEl,
    string GeneradoPor,
    IReadOnlyList<TablaReporte> Tablas,
    IReadOnlyList<GraficoReporte> Graficos);
