namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Fila de la grilla de diferencias de precio de <see cref="AlertaPreciosPresupuestoViewModel"/>: lo pactado, lo actual de
/// catálogo y la diferencia por unidad. Los aumentos se resaltan con el token de advertencia; las bajas, con uno neutro.
/// </summary>
public sealed record FilaDiscrepanciaPrecio(
    string Descripcion,
    string Pactado,
    string Actual,
    string Diferencia,
    bool HuboAumento);

/// <summary>
/// Fila de la lista de faltantes de stock: lo que falta y, si el artículo es una presentación que se puede fraccionar
/// (RF-21), la sugerencia. <see cref="Sugerencia"/> es <c>null</c> si no hay forma de cubrirlo.
/// </summary>
public sealed record FilaFaltanteStock(string Texto, string? Sugerencia)
{
    public bool TieneSugerencia => Sugerencia is not null;
}
