using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Ventas;

namespace Retail.App.Services;

/// <summary>
/// Contrato para el despacho de diálogos y ventanas modales de la terminal de mostrador (POS).
/// Desacopla la lógica de presentación (ViewModels) de la instanciación de ventanas WPF para facilitar pruebas unitarias.
/// </summary>
public interface IVentaDialogService
{
    Task<IReadOnlyList<PagoVentaDto>?> MostrarCobroModalAsync(decimal totalVenta, ClienteDto? cliente);

    Task<ClienteDto?> MostrarSeleccionarClienteModalAsync(ClienteDto? clienteActual);

    bool Confirmar(string titulo, string mensaje);

    void MostrarAlerta(string titulo, string mensaje);

    void MostrarError(string titulo, string mensaje);
}
