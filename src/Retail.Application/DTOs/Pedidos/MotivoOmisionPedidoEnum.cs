namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Por qué un ítem de catálogo no se puede agregar a un pedido. Es un resultado de la consulta, no un concepto del
/// negocio: por eso vive junto a los DTOs y no en <c>Retail.Domain.Enums</c>.
/// </summary>
public enum MotivoOmisionPedidoEnum
{
    /// <summary>El ítem no existe o fue dado de baja.</summary>
    ItemInexistente = 1,

    /// <summary>El ítem no está vinculado a ningún artículo de la tienda: hay que incorporarlo antes (A-8).</summary>
    SinIncorporar = 2
}
