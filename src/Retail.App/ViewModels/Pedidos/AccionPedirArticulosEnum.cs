namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Cómo se cerró el modal de pedido (A-17). Es estado de la pantalla, no un concepto del negocio: por eso vive en la
/// UI y no en <c>Retail.Domain.Enums</c>.
/// </summary>
public enum AccionPedirArticulosEnum
{
    /// <summary>El usuario cerró el modal sin pedir nada.</summary>
    Cancelar = 1,

    /// <summary>El usuario confirmó los renglones: quien abrió el modal los guarda.</summary>
    Confirmar = 2,

    /// <summary>
    /// El usuario quiere incorporar primero los ítems sin incorporar y después volver al modal (recorrido en dos pasos).
    /// </summary>
    IncorporarPrimero = 3
}
