namespace Retail.Application.DTOs.Reportes;

/// <summary>Dimensión por la que se agrupa el reporte de rentabilidad (RF-25).</summary>
public enum AgrupacionRentabilidadEnum
{
    /// <summary>Por categoría del artículo.</summary>
    Categoria,

    /// <summary>Por marca del artículo.</summary>
    Marca,

    /// <summary>Por artículo individual.</summary>
    Articulo
}
