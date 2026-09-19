using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de los casos de uso del Punto de Venta (POS) y orquestación de ventas en mostrador (RF-09, RF-10, RNF-01).
/// Provee consultas directas al motor relacional sin tracking y orquesta la interacción con comprobantes térmicos.
/// </summary>
public class VentaService : IVentaService
{
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly ITicketPrinterService _ticketPrinterService;

    public VentaService(
        IRepository<Articulo> articuloRepository,
        ITicketPrinterService ticketPrinterService)
    {
        _articuloRepository = articuloRepository ?? throw new ArgumentNullException(nameof(articuloRepository));
        _ticketPrinterService = ticketPrinterService ?? throw new ArgumentNullException(nameof(ticketPrinterService));
    }

    public async Task<ArticuloVentaDto?> BuscarPorCodigoBarrasAsync(string codigoBarras, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoBarras))
        {
            return null;
        }

        string codigoLimpio = codigoBarras.Trim();
        var articulos = await _articuloRepository.FindAsync(
            a => a.CodigoBarras == codigoLimpio,
            includeDeleted: false,
            cancellationToken);

        return articulos.Count > 0 ? MapToVentaDto(articulos[0]) : null;
    }

    public async Task<IReadOnlyList<ArticuloVentaDto>> BuscarPorTextoAsync(string termino, int limite = 15, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(termino))
        {
            return Array.Empty<ArticuloVentaDto>();
        }

        string terminoLimpio = termino.Trim();
        var articulos = await _articuloRepository.FindAsync(
            a => a.Descripcion.Contains(terminoLimpio) || (a.CodigoBarras != null && a.CodigoBarras.Contains(terminoLimpio)),
            includeDeleted: false,
            cancellationToken);

        return articulos
            .OrderBy(a => a.Descripcion)
            .Take(limite)
            .Select(MapToVentaDto)
            .ToList();
    }

    public async Task<ArticuloVentaDto?> BuscarArticuloParaVentaAsync(string codigoOBusqueda, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoOBusqueda))
        {
            return null;
        }

        // 1. Intentar coincidencia exacta de código de barras
        var porCodigo = await BuscarPorCodigoBarrasAsync(codigoOBusqueda, cancellationToken);
        if (porCodigo != null)
        {
            return porCodigo;
        }

        // 2. Intentar por texto si no hubo coincidencia por código
        var porTexto = await BuscarPorTextoAsync(codigoOBusqueda, 1, cancellationToken);
        return porTexto.Count > 0 ? porTexto[0] : null;
    }

    public async Task<VentaResponseDto> RegistrarVentaAsync(CrearVentaDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Items.Count == 0)
        {
            throw new InvalidOperationException("No se puede registrar una venta sin artículos.");
        }

        decimal subtotal = dto.Items.Sum(i => i.SubtotalItem);
        decimal total = Math.Max(0m, subtotal - dto.Descuento);

        var response = new VentaResponseDto
        {
            IdVenta = Random.Shared.Next(1000, 99999),
            IdTurno = dto.IdTurno,
            IdUsuario = dto.IdUsuario,
            IdCliente = dto.IdCliente,
            ClienteNombre = dto.IdCliente.HasValue ? null : "Consumidor Final",
            IdPresupuestoOrigen = dto.IdPresupuestoOrigen,
            FechaHora = DateTime.UtcNow,
            Subtotal = subtotal,
            Descuento = dto.Descuento,
            Total = total,
            EstadoFiscal = EstadoFiscalEnum.NoAplica,
            Items = dto.Items,
            Pagos = dto.Pagos
        };

        // Despacho de impresión de ticket térmico al hardware simulado
        await _ticketPrinterService.ImprimirTicketVentaAsync(response, cancellationToken);

        return response;
    }

    public Task<VentaResponseDto?> ObtenerVentaPorIdAsync(int idVenta, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<VentaResponseDto?>(null);
    }

    public Task<IReadOnlyList<VentaResponseDto>> ListarVentasTurnoAsync(int idTurno, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<VentaResponseDto>>(Array.Empty<VentaResponseDto>());
    }

    private static ArticuloVentaDto MapToVentaDto(Articulo a)
    {
        return new ArticuloVentaDto
        {
            IdArticulo = a.Id,
            CodigoBarras = a.CodigoBarras,
            Descripcion = a.Descripcion,
            PrecioVenta = a.PrecioVenta,
            StockActual = a.StockActual,
            EsServicio = a.EsServicio
        };
    }
}
