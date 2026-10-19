namespace Retail.Application.DTOs.Reportes.Exportacion;

/// <summary>Estilo de un gráfico del documento.</summary>
public enum TipoGraficoEnum
{
    Barras,
    Lineas,

    /// <summary>Barras horizontales: para categorías y artículos de nombres largos, que no entran debajo de una barra vertical (D-26).</summary>
    BarrasHorizontales
}
