namespace Retail.Domain.Entities;

/// <summary>
/// Dato de entrada de <see cref="Presupuesto.Emitir"/>: un renglón a presupuestar, con la descripción y el precio
/// que el llamador ya resolvió. El agregado no conoce a <see cref="Articulo"/> (I8): solo recibe su identificador.
/// </summary>
/// <param name="IdArticulo">Identificador del artículo presupuestado.</param>
/// <param name="Descripcion">Descripción del artículo al momento de emitir (se guarda como foto, PS-11).</param>
/// <param name="Cantidad">Cantidad presupuestada; al menos 1.</param>
/// <param name="PrecioUnitario">Precio unitario pactado; no negativo.</param>
public readonly record struct RenglonAPresupuestar(int IdArticulo, string Descripcion, int Cantidad, decimal PrecioUnitario);
