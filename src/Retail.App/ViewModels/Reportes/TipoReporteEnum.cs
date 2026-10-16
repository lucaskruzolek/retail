namespace Retail.App.ViewModels.Reportes;

/// <summary>
/// Reportes que ofrece la pantalla del tablero gerencial (IU-11). Es un enum de presentación: elige qué filtros,
/// gráficos y grillas se muestran, y no pertenece al Dominio ni a Aplicación.
/// </summary>
public enum TipoReporteEnum
{
    /// <summary>Indicadores del período comparados con el anterior (RF-24, RF-25).</summary>
    Tablero,

    /// <summary>Ventas agrupadas por día, semana o mes (RF-24).</summary>
    VentasPorPeriodo,

    /// <summary>Artículos más y menos vendidos (RF-24).</summary>
    Ranking,

    /// <summary>Margen bruto por categoría, marca o artículo (RF-25).</summary>
    Rentabilidad,

    /// <summary>Artículos bajo el stock mínimo y valorización (RF-25).</summary>
    StockCritico,

    /// <summary>Artículos con stock que no se venden hace tiempo (RF-25).</summary>
    SinMovimiento
}
