using FluentValidation;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de los casos de uso del Punto de Venta (POS) y orquestación de ventas en mostrador (RF-09, RF-10, RNF-01).
/// Provee consultas directas al motor relacional sin tracking y orquesta la interacción con comprobantes térmicos.
/// </summary>
public class VentaService : IVentaService
{
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IRepository<Venta> _ventaRepository;
    private readonly IRepository<TurnoCaja> _turnoRepository;
    private readonly IRepository<Cliente> _clienteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearVentaDto> _crearVentaValidator;
    private readonly ITicketPrinterService _ticketPrinterService;

    public VentaService(
        IRepository<Articulo> articuloRepository,
        IRepository<Venta> ventaRepository,
        IRepository<TurnoCaja> turnoRepository,
        IRepository<Cliente> clienteRepository,
        IUnitOfWork unitOfWork,
        IValidator<CrearVentaDto> crearVentaValidator,
        ITicketPrinterService ticketPrinterService)
    {
        _articuloRepository = articuloRepository ?? throw new ArgumentNullException(nameof(articuloRepository));
        _ventaRepository = ventaRepository ?? throw new ArgumentNullException(nameof(ventaRepository));
        _turnoRepository = turnoRepository ?? throw new ArgumentNullException(nameof(turnoRepository));
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _crearVentaValidator = crearVentaValidator ?? throw new ArgumentNullException(nameof(crearVentaValidator));
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
        await _crearVentaValidator.ValidateAndThrowAsync(dto, cancellationToken);

        // 1. Cargar los agregados involucrados. Ninguno se modifica hasta que pasen todas las validaciones:
        //    el DbContext vive mientras dura la pantalla del POS (H-19), y un agregado modificado por una
        //    venta rechazada se guardaría junto con la siguiente venta exitosa.
        var turno = await _turnoRepository.GetByIdAsync(dto.IdTurno, cancellationToken);
        if (turno == null || turno.Estado != EstadoTurnoEnum.Abierto)
        {
            throw new CajaCerradaException();
        }

        var idsArticulos = dto.Items.Select(i => i.IdArticulo).Distinct().ToList();
        var articulos = await _articuloRepository.FindAsync(
            a => idsArticulos.Contains(a.Id),
            includeDeleted: false,
            cancellationToken);
        var articulosPorId = articulos.ToDictionary(a => a.Id);

        Cliente? cliente = null;
        if (dto.IdCliente is int idCliente)
        {
            cliente = await _clienteRepository.GetByIdAsync(idCliente, includeDeleted: false, cancellationToken)
                ?? throw new DomainException($"No se encontró ningún cliente activo con el identificador {idCliente}.");
        }

        // 2. Armar la venta con los precios vigentes del catálogo (D-11), no con los que envía la UI.
        var venta = Venta.Registrar(dto.IdTurno, dto.IdUsuario, dto.IdCliente);

        foreach (var item in dto.Items)
        {
            if (!articulosPorId.TryGetValue(item.IdArticulo, out var articulo))
            {
                throw new DomainException($"El artículo '{item.Descripcion}' ya no está disponible: fue dado de baja.");
            }

            if (articulo.PrecioVenta != item.PrecioUnitario)
            {
                throw new DomainException(
                    $"El precio de '{articulo.Descripcion}' cambió de {item.PrecioUnitario:C} a {articulo.PrecioVenta:C}. Vuelva a cargar el artículo en el ticket.");
            }

            venta.AgregarItem(articulo.Id, item.Cantidad, articulo.PrecioVenta);
        }

        venta.AplicarDescuento(dto.Descuento);

        foreach (var pago in dto.Pagos)
        {
            venta.ImputarPago(pago.MedioPago, pago.Monto, pago.ReferenciaPago);
        }

        venta.ValidarCierre();

        // 3. Verificar el stock de TODOS los artículos antes de descontar cualquiera (RF-10).
        //    venta.Detalles ya agrupa en una sola línea los artículos repetidos del ticket.
        var descuentosDeStock = venta.Detalles
            .Select(d => (Articulo: articulosPorId[d.IdArticulo], d.Cantidad))
            .Where(x => !x.Articulo.EsServicio)
            .ToList();

        foreach (var (articulo, cantidad) in descuentosDeStock)
        {
            articulo.VerificarStockDisponible(cantidad);
        }

        // 4. Último paso que puede fallar (límite de crédito): DebitarCuentaCorriente valida antes de modificar
        //    el saldo, así que si lanza todavía no se modificó ningún agregado. El cliente existe siempre que haya
        //    pagos en cuenta corriente, porque Venta.ImputarPago se los rechaza al Consumidor Final.
        if (cliente != null && venta.TotalCuentaCorriente > 0m)
        {
            cliente.DebitarCuentaCorriente(venta.TotalCuentaCorriente);
        }

        // 5. Con todo validado, se modifican los demás agregados. No hace falta UpdateAsync: este mismo contexto
        //    los cargó y su ChangeTracker detecta qué columnas cambiaron.
        foreach (var (articulo, cantidad) in descuentosDeStock)
        {
            articulo.DescontarStock(cantidad);
        }

        // La cuenta corriente es deuda del cliente: no ingresa dinero a la gaveta (RF-15).
        turno.ImputarVenta(venta.TotalEfectivo, venta.TotalElectronico);

        await _ventaRepository.AddAsync(venta, cancellationToken);

        // 6. Un único SaveChanges: EF Core guarda la venta con sus ítems y pagos, el stock, el turno y la cuenta
        //    corriente en una sola transacción de SQL Server. O se guarda todo, o nada (RF-10).
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // La base revirtió la transacción, pero el contexto conserva en memoria la venta agregada y los
            // agregados modificados. Se descartan para que no se guarden con la próxima venta; el reintento
            // vuelve a leer stock y rowversion actualizados.
            _unitOfWork.DescartarCambios();
            throw;
        }

        var response = new VentaResponseDto
        {
            IdVenta = venta.Id,
            IdTurno = venta.IdTurno,
            IdUsuario = venta.IdUsuario,
            IdCliente = venta.IdCliente,
            ClienteNombre = cliente?.RazonSocialONombre ?? "Consumidor Final",
            IdPresupuestoOrigen = venta.IdPresupuestoOrigen,
            FechaHora = venta.FechaHora,
            Subtotal = venta.Subtotal,
            Descuento = venta.Descuento,
            Total = venta.Total,
            EstadoFiscal = venta.EstadoFiscal,
            Items = venta.Detalles
                .Select(d => new DetalleVentaDto
                {
                    IdArticulo = d.IdArticulo,
                    CodigoBarras = articulosPorId[d.IdArticulo].CodigoBarras,
                    Descripcion = articulosPorId[d.IdArticulo].Descripcion,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario
                })
                .ToList(),
            Pagos = dto.Pagos
        };

        try
        {
            await _ticketPrinterService.ImprimirTicketVentaAsync(response, cancellationToken);
        }
        catch
        {
            // La venta ya está confirmada: una falla de la impresora no debe hacer creer al cajero que el cobro
            // falló (y llevarlo a cobrar de nuevo). Mismo criterio que el recibo de cobranza.
        }

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
