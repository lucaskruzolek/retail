using Retail.Application.DTOs.Pedidos;

namespace Retail.App.Services;

/// <summary>
/// Contrato para la interacción con diálogos y ventanas modales del módulo de Compras (Track 4.3).
/// Desacopla la lógica de presentación (ComprasViewModel) de la instanciación de ventanas WPF.
/// </summary>
public interface ICompraDialogService
{
    /// <summary>
    /// Muestra un cuadro de confirmación con opciones Sí/No.
    /// </summary>
    bool Confirmar(string titulo, string mensaje);

    /// <summary>
    /// Muestra un cuadro de alerta informativa/advertencia al usuario.
    /// </summary>
    void MostrarAlerta(string titulo, string mensaje);

    /// <summary>
    /// Abre el diálogo modal de selección de pedidos emitidos para el proveedor especificado
    /// (<paramref name="razonSocialProveedor"/> se muestra en el encabezado de la sección).
    /// Devuelve el pedido seleccionado o null si el usuario canceló la selección.
    /// </summary>
    Task<PedidoEmitidoDto?> MostrarSeleccionarPedidoModalAsync(int idProveedor, string razonSocialProveedor);
}

