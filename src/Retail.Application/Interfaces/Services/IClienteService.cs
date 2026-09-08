using Retail.Application.DTOs.Clientes;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para la administración de clientes, límites de crédito comercial y cobranza multimedio de cuenta corriente.
/// </summary>
public interface IClienteService
{
    Task<IReadOnlyList<ClienteDto>> BuscarClientesAsync(string terminoBusqueda, CancellationToken cancellationToken = default);

    Task<ClienteDto?> ObtenerClientePorIdAsync(int idCliente, CancellationToken cancellationToken = default);

    Task<ClienteDto> CrearClienteAsync(CrearClienteDto dto, CancellationToken cancellationToken = default);

    Task ActualizarClienteAsync(ActualizarClienteDto dto, CancellationToken cancellationToken = default);

    Task BajaClienteAsync(int idCliente, CancellationToken cancellationToken = default);

    Task<CobranzaResultadoDto> RegistrarCobranzaAsync(RegistrarCobranzaDto dto, CancellationToken cancellationToken = default);

    Task DebitarCuentaCorrienteAsync(int idCliente, decimal monto, CancellationToken cancellationToken = default);
}
