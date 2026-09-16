using Retail.Application.DTOs.Clientes;

namespace Retail.App.Services;

/// <summary>
/// Contrato para la interacción con diálogos y ventanas modales del padrón de clientes.
/// Desacopla la lógica de presentación (ViewModels) de la instanciación de ventanas WPF para facilitar pruebas unitarias.
/// </summary>
public interface IClienteDialogService
{
    bool Confirmar(string titulo, string mensaje);

    void MostrarError(string titulo, string mensaje);

    void MostrarInformacion(string titulo, string mensaje);

    CrearClienteDto? MostrarDialogoCrear(Func<CrearClienteDto, Task>? onGuardarAsync = null);

    ActualizarClienteDto? MostrarDialogoModificar(ClienteDto cliente, Func<ActualizarClienteDto, Task>? onGuardarAsync = null);

    Task<CobranzaResultadoDto?> MostrarCobranzaModalAsync(ClienteDto cliente);
}
