namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Comportamiento de un artículo en el historial; cada perfil existe para que un reporte tenga qué mostrar.
/// </summary>
internal enum PerfilDemandaDemo
{
    /// <summary>Se vende y se repone durante todo el período.</summary>
    Regular,

    /// <summary>Se deja de reponer en las últimas semanas: termina en el reporte de stock crítico.</summary>
    SinReposicionFinal,

    /// <summary>Se deja de vender hace más de 90 días, con stock: termina en el reporte sin movimiento.</summary>
    Discontinuado,

    /// <summary>Nunca se vende: encabeza los menos vendidos y aparece como sin movimiento.</summary>
    SinVentas
}
