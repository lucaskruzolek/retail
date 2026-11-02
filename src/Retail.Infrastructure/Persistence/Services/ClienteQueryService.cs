using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación de consultas optimizadas, búsqueda rápida y paginación para clientes con push-down a SQL Server (Ley 8).
/// </summary>
public class ClienteQueryService : IClienteQueryService
{
    private readonly RetailDbContext _context;

    public ClienteQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ClientesPaginadosDto> ObtenerClientesPaginadosAsync(
        ConsultaClientesDto consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        // 1. Agregaciones nativas globales de cartera en servidor (Ley 8)
        int totalClientes = await _context.Clientes.CountAsync(cancellationToken);
        int totalConDeuda = await _context.Clientes.CountAsync(c => c.SaldoCuentaCorriente > 0m, cancellationToken);
        decimal totalDeudaCartera = await _context.Clientes.SumAsync(c => (decimal?)c.SaldoCuentaCorriente, cancellationToken) ?? 0m;

        // 2. Consulta filtrada sin tracking (RNF-03)
        IQueryable<Cliente> query = _context.Clientes
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(consulta.TerminoBusqueda))
        {
            var term = consulta.TerminoBusqueda.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.RazonSocialONombre, $"%{term}%") ||
                EF.Functions.Like(c.NumeroDocumento, $"%{term}%") ||
                (c.Email != null && EF.Functions.Like(c.Email, $"%{term}%")));
        }

        if (consulta.CondicionIva.HasValue)
        {
            query = query.Where(c => c.CondicionIva == consulta.CondicionIva.Value);
        }

        // Los dos estados de cada filtro son disjuntos: elegir uno filtra y elegir ambos (o ninguno) no filtra.
        // La comparación llega a SQL como un parámetro booleano, así que la consulta sigue siendo componible (Ley 8).
        bool conDeuda = consulta.EstadosDeuda.Contains(FiltroDeudaClienteEnum.ConDeuda);
        bool sinDeuda = consulta.EstadosDeuda.Contains(FiltroDeudaClienteEnum.SinDeuda);
        bool filtraDeuda = conDeuda != sinDeuda;
        if (filtraDeuda)
        {
            query = query.Where(c => (conDeuda && c.SaldoCuentaCorriente > 0m) || (sinDeuda && c.SaldoCuentaCorriente <= 0m));
        }

        bool conCuentaCorriente = consulta.EstadosCuentaCorriente.Contains(FiltroCuentaCorrienteClienteEnum.ConCuentaCorriente);
        bool sinCuentaCorriente = consulta.EstadosCuentaCorriente.Contains(FiltroCuentaCorrienteClienteEnum.SinCuentaCorriente);
        bool filtraCuentaCorriente = conCuentaCorriente != sinCuentaCorriente;
        if (filtraCuentaCorriente)
        {
            query = query.Where(c => (conCuentaCorriente && c.TieneCuentaCorriente) || (sinCuentaCorriente && !c.TieneCuentaCorriente));
        }

        bool esConsultaFiltrada = !string.IsNullOrWhiteSpace(consulta.TerminoBusqueda) ||
            consulta.CondicionIva.HasValue ||
            filtraDeuda ||
            filtraCuentaCorriente;

        // Conteo de registros coincidentes (evita consulta COUNT duplicada si no hay filtros)
        int totalRegistros = esConsultaFiltrada
            ? await query.CountAsync(cancellationToken)
            : totalClientes;

        int tamanoPagina = Math.Max(1, consulta.TamanoPagina);
        int pagina = Math.Max(1, consulta.Pagina);

        var entidades = await query
            .OrderBy(c => c.RazonSocialONombre)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);

        var items = entidades.Select(c => new ClienteDto
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
        }).ToList();

        return new ClientesPaginadosDto
        {
            Items = items,
            TotalRegistros = totalRegistros,
            TotalClientes = totalClientes,
            TotalClientesConDeuda = totalConDeuda,
            TotalDeudaCartera = totalDeudaCartera,
            PaginaActual = pagina,
            TamanoPagina = tamanoPagina
        };
    }

    public async Task<IReadOnlyList<ClienteDto>> BuscarClientesRapidoAsync(
        string terminoBusqueda,
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Cliente> query = _context.Clientes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(terminoBusqueda))
        {
            var term = terminoBusqueda.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.RazonSocialONombre, $"%{term}%") ||
                EF.Functions.Like(c.NumeroDocumento, $"%{term}%") ||
                (c.Email != null && EF.Functions.Like(c.Email, $"%{term}%")));
        }

        var entidades = await query
            .OrderBy(c => c.RazonSocialONombre)
            .Take(Math.Max(1, limite))
            .ToListAsync(cancellationToken);

        return entidades.Select(c => new ClienteDto
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
        }).ToList();
    }
}
