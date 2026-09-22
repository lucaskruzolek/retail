using FluentValidation;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de los casos de uso para la administración del padrón de clientes y cuentas corrientes comerciales (RF-20).
/// </summary>
public class ClienteService : IClienteService
{
    private readonly IRepository<Cliente> _clienteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearClienteDto> _crearClienteValidator;
    private readonly IValidator<ActualizarClienteDto> _actualizarClienteValidator;
    private readonly IValidator<RegistrarCobranzaDto> _registrarCobranzaValidator;
    private readonly ICajaService _cajaService;
    private readonly ITicketPrinterService _ticketPrinterService;
    private readonly IClienteQueryService? _clienteQueryService;

    public ClienteService(
        IRepository<Cliente> clienteRepository,
        IUnitOfWork unitOfWork,
        IValidator<CrearClienteDto> crearClienteValidator,
        IValidator<ActualizarClienteDto> actualizarClienteValidator,
        IValidator<RegistrarCobranzaDto> registrarCobranzaValidator,
        ICajaService cajaService,
        ITicketPrinterService ticketPrinterService,
        IClienteQueryService? clienteQueryService = null)
    {
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _crearClienteValidator = crearClienteValidator ?? throw new ArgumentNullException(nameof(crearClienteValidator));
        _actualizarClienteValidator = actualizarClienteValidator ?? throw new ArgumentNullException(nameof(actualizarClienteValidator));
        _registrarCobranzaValidator = registrarCobranzaValidator ?? throw new ArgumentNullException(nameof(registrarCobranzaValidator));
        _cajaService = cajaService ?? throw new ArgumentNullException(nameof(cajaService));
        _ticketPrinterService = ticketPrinterService ?? throw new ArgumentNullException(nameof(ticketPrinterService));
        _clienteQueryService = clienteQueryService;
    }

    public async Task<ClientesPaginadosDto> ListarClientesPaginadosAsync(ConsultaClientesDto consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (_clienteQueryService != null)
        {
            return await _clienteQueryService.ObtenerClientesPaginadosAsync(consulta, cancellationToken);
        }

        var todos = await BuscarClientesAsync(string.Empty, cancellationToken);
        var query = todos.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(consulta.TerminoBusqueda))
        {
            var term = consulta.TerminoBusqueda.Trim();
            query = query.Where(c =>
                c.RazonSocialONombre.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.NumeroDocumento.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (c.Email != null && c.Email.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (consulta.CondicionIva.HasValue)
        {
            query = query.Where(c => c.CondicionIva == consulta.CondicionIva.Value);
        }

        if (consulta.SoloConDeuda)
        {
            query = query.Where(c => c.SaldoCuentaCorriente > 0m);
        }

        if (consulta.SoloConCuentaCorriente)
        {
            query = query.Where(c => c.TieneCuentaCorriente);
        }

        var filtrados = query.OrderBy(c => c.RazonSocialONombre).ToList();
        int tamano = Math.Max(1, consulta.TamanoPagina);
        int pagina = Math.Max(1, consulta.Pagina);
        var items = filtrados.Skip((pagina - 1) * tamano).Take(tamano).ToList();

        return new ClientesPaginadosDto
        {
            Items = items,
            TotalRegistros = filtrados.Count,
            TotalClientes = todos.Count,
            TotalClientesConDeuda = todos.Count(c => c.SaldoCuentaCorriente > 0m),
            TotalDeudaCartera = todos.Sum(c => c.SaldoCuentaCorriente),
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    public async Task<IReadOnlyList<ClienteDto>> BuscarClientesAsync(string terminoBusqueda, CancellationToken cancellationToken = default)
    {
        if (_clienteQueryService != null)
        {
            return await _clienteQueryService.BuscarClientesRapidoAsync(terminoBusqueda, 50, cancellationToken);
        }

        var todos = await _clienteRepository.ListAllAsync(includeDeleted: false, cancellationToken);

        if (string.IsNullOrWhiteSpace(terminoBusqueda))
        {
            return todos
                .OrderBy(c => c.RazonSocialONombre)
                .Select(MapToDto)
                .ToList();
        }

        string termino = terminoBusqueda.Trim();

        return todos
            .Where(c => c.RazonSocialONombre.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                        c.NumeroDocumento.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                        (c.Email != null && c.Email.Contains(termino, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.RazonSocialONombre)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ClienteDto?> ObtenerClientePorIdAsync(int idCliente, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(idCliente, cancellationToken);
        return cliente == null || cliente.IsDeleted ? null : MapToDto(cliente);
    }

    public async Task<ClienteDto> CrearClienteAsync(CrearClienteDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _crearClienteValidator.ValidateAndThrowAsync(dto, cancellationToken);

        string documentoSanitizado = dto.NumeroDocumento.Trim();

        var existente = await _clienteRepository.FindAsync(c => c.NumeroDocumento == documentoSanitizado, cancellationToken);
        if (existente.Count > 0)
        {
            throw new InvalidOperationException($"Ya existe un cliente activo registrado con el número de documento '{documentoSanitizado}'.");
        }

        var cliente = new Cliente
        {
            RazonSocialONombre = dto.RazonSocialONombre.Trim(),
            TipoDocumento = dto.TipoDocumento,
            NumeroDocumento = documentoSanitizado,
            CondicionIva = dto.CondicionIva,
            DomicilioFiscal = string.IsNullOrWhiteSpace(dto.DomicilioFiscal) ? null : dto.DomicilioFiscal.Trim(),
            Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            TieneCuentaCorriente = dto.TieneCuentaCorriente,
            LimiteCredito = dto.TieneCuentaCorriente ? dto.LimiteCredito : 0m,
            SaldoCuentaCorriente = 0m
        };

        await _clienteRepository.AddAsync(cliente, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(cliente);
    }

    public async Task ActualizarClienteAsync(ActualizarClienteDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _actualizarClienteValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var cliente = await _clienteRepository.GetByIdAsync(dto.IdCliente, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el cliente activo con ID {dto.IdCliente}.");

        string documentoSanitizado = dto.NumeroDocumento.Trim();

        var duplicado = await _clienteRepository.FindAsync(
            c => c.NumeroDocumento == documentoSanitizado && c.Id != dto.IdCliente,
            cancellationToken);

        if (duplicado.Count > 0)
        {
            throw new InvalidOperationException($"Ya existe otro cliente activo registrado con el número de documento '{documentoSanitizado}'.");
        }

        cliente.ActualizarDatos(
            dto.RazonSocialONombre,
            dto.TipoDocumento,
            documentoSanitizado,
            dto.CondicionIva,
            dto.DomicilioFiscal,
            dto.Telefono,
            dto.Email);

        if (dto.TieneCuentaCorriente)
        {
            if (!cliente.TieneCuentaCorriente)
            {
                cliente.HabilitarCuentaCorriente(dto.LimiteCredito);
            }
            else
            {
                cliente.ModificarLimiteCredito(dto.LimiteCredito);
            }
        }
        else if (cliente.TieneCuentaCorriente)
        {
            cliente.DeshabilitarCuentaCorriente();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task BajaClienteAsync(int idCliente, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(idCliente, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el cliente activo con ID {idCliente}.");

        cliente.MarkAsDeleted();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DebitarCuentaCorrienteAsync(int idCliente, decimal monto, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdAsync(idCliente, cancellationToken)
            ?? throw new KeyNotFoundException($"No se encontró el cliente activo con ID {idCliente}.");

        cliente.DebitarCuentaCorriente(monto);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CobranzaHistorialDto>> ListarHistorialCobranzasAsync(int idCliente, CancellationToken cancellationToken = default)
    {
        var cliente = await _clienteRepository.GetByIdWithIncludesAsync(idCliente, cancellationToken, c => c.Cobranzas);
        if (cliente == null)
        {
            return Array.Empty<CobranzaHistorialDto>();
        }

        return cliente.Cobranzas
            .OrderByDescending(c => c.FechaHora)
            .Select(c => new CobranzaHistorialDto
            {
                IdCobranza = c.Id,
                IdCliente = c.IdCliente,
                IdTurno = c.IdTurno,
                IdUsuario = c.IdUsuario,
                UsuarioNombre = null,
                FechaHora = c.FechaHora,
                MedioPago = c.MedioPago,
                Monto = c.Monto,
                Referencia = c.Referencia
            })
            .ToList();
    }

    public async Task<CobranzaResultadoDto> RegistrarCobranzaAsync(RegistrarCobranzaDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _registrarCobranzaValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var turnoActivo = await _cajaService.ObtenerTurnoActivoAsync(cancellationToken);
        if (turnoActivo == null || turnoActivo.Estado != Retail.Domain.Enums.EstadoTurnoEnum.Abierto)
        {
            throw new InvalidOperationException("No se puede registrar una cobranza sin un turno de caja activo y abierto.");
        }

        if (turnoActivo.IdTurno != dto.IdTurno)
        {
            throw new InvalidOperationException($"El turno especificado (#{dto.IdTurno}) no coincide con el turno activo actual (#{turnoActivo.IdTurno}).");
        }

        var cliente = await _clienteRepository.GetByIdWithIncludesAsync(dto.IdCliente, cancellationToken, c => c.Cobranzas)
            ?? throw new KeyNotFoundException($"No se encontró el cliente activo con ID {dto.IdCliente}.");

        decimal saldoAnterior = cliente.SaldoCuentaCorriente;

        var cobranza = cliente.RegistrarCobranza(
            dto.IdTurno,
            dto.IdUsuario,
            dto.Monto,
            dto.MedioPago,
            dto.Referencia);

        await _cajaService.RegistrarIngresoCobranzaAsync(dto.IdTurno, dto.Monto, dto.MedioPago, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var resultado = new CobranzaResultadoDto
        {
            IdCobranza = cobranza.Id,
            IdCliente = cliente.Id,
            ClienteNombre = cliente.RazonSocialONombre,
            FechaHora = cobranza.FechaHora,
            MedioPago = cobranza.MedioPago,
            MontoAbonado = cobranza.Monto,
            SaldoAnterior = saldoAnterior,
            NuevoSaldo = cliente.SaldoCuentaCorriente,
            Referencia = cobranza.Referencia
        };

        try
        {
            await _ticketPrinterService.ImprimirReciboCobranzaAsync(resultado, cancellationToken);
        }
        catch
        {
            // La eventual falla de hardware de impresión no cancela la transacción financiera ya confirmada.
        }

        return resultado;
    }

    private static ClienteDto MapToDto(Cliente c) => new()
    {
        IdCliente = c.Id,
        RazonSocialONombre = c.RazonSocialONombre,
        TipoDocumento = c.TipoDocumento,
        NumeroDocumento = c.NumeroDocumento,
        CondicionIva = c.CondicionIva,
        DomicilioFiscal = c.DomicilioFiscal,
        Telefono = c.Telefono,
        Email = c.Email,
        TieneCuentaCorriente = c.TieneCuentaCorriente,
        LimiteCredito = c.LimiteCredito,
        SaldoCuentaCorriente = c.SaldoCuentaCorriente
    };
}
