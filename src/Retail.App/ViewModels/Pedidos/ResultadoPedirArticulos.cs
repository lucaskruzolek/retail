namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Cómo terminó un intento de pedir desde una pantalla de origen (A-17). La pantalla muestra el mensaje que
/// corresponda y, si la acción es <see cref="AccionPedirArticulosEnum.IncorporarPrimero"/>, incorpora los ítems de
/// <see cref="IdsSinIncorporar"/> y vuelve a pedir.
/// </summary>
/// <param name="Accion">
/// <see cref="AccionPedirArticulosEnum.Confirmar"/> solo si el lote se guardó; un error se informa como
/// <see cref="AccionPedirArticulosEnum.Cancelar"/> con <see cref="MensajeError"/>.
/// </param>
/// <param name="MensajeEstado">Qué se agregó y a qué pedidos, si se guardó.</param>
/// <param name="MensajeError">Por qué no se pudo pedir, si falló.</param>
/// <param name="IdsSinIncorporar">Los ítems que la vista previa informó sin incorporar, si se eligió incorporarlos.</param>
public sealed record ResultadoPedirArticulos(
    AccionPedirArticulosEnum Accion,
    string? MensajeEstado,
    string? MensajeError,
    IReadOnlyList<int> IdsSinIncorporar)
{
    public static ResultadoPedirArticulos Cancelado { get; } = new(AccionPedirArticulosEnum.Cancelar, null, null, []);

    public static ResultadoPedirArticulos Guardado(string mensajeEstado)
    {
        return new ResultadoPedirArticulos(AccionPedirArticulosEnum.Confirmar, mensajeEstado, null, []);
    }

    public static ResultadoPedirArticulos ConError(string mensajeError)
    {
        return new ResultadoPedirArticulos(AccionPedirArticulosEnum.Cancelar, null, mensajeError, []);
    }

    public static ResultadoPedirArticulos IncorporarPrimero(IReadOnlyList<int> idsSinIncorporar)
    {
        return new ResultadoPedirArticulos(AccionPedirArticulosEnum.IncorporarPrimero, null, null, idsSinIncorporar);
    }
}
