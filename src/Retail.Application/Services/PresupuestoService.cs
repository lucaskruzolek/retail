using FluentValidation;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de los casos de uso del presupuesto comercial (RF-11, RF-12). Toda modificación pasa por la raíz
/// <see cref="Presupuesto"/> (Ley 2); las lecturas se delegan en <see cref="IPresupuestoQueryService"/> (Ley 8).
/// La conversión en venta no está acá: la hace <see cref="VentaService"/> dentro del cobro (PS-12).
/// </summary>
public class PresupuestoService : IPresupuestoService
{
    private readonly IRepository<Presupuesto> _presupuestoRepository;
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IRepository<Cliente> _clienteRepository;
    private readonly IPresupuestoQueryService _consultas;
    private readonly IArticuloQueryService _articuloQueryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITicketPrinterService _ticketPrinterService;
    private readonly IExportadorPresupuestoPdf _exportadorPdf;
    private readonly TimeProvider _reloj;
    private readonly IValidator<CrearPresupuestoDto> _crearValidator;
    private readonly IValidator<ConsultaPresupuestosDto> _consultaValidator;

    public PresupuestoService(
        IRepository<Presupuesto> presupuestoRepository,
        IRepository<Articulo> articuloRepository,
        IRepository<Cliente> clienteRepository,
        IPresupuestoQueryService consultas,
        IArticuloQueryService articuloQueryService,
        IUnitOfWork unitOfWork,
        ITicketPrinterService ticketPrinterService,
        IExportadorPresupuestoPdf exportadorPdf,
        TimeProvider reloj,
        IValidator<CrearPresupuestoDto> crearValidator,
        IValidator<ConsultaPresupuestosDto> consultaValidator)
    {
        _presupuestoRepository = presupuestoRepository ?? throw new ArgumentNullException(nameof(presupuestoRepository));
        _articuloRepository = articuloRepository ?? throw new ArgumentNullException(nameof(articuloRepository));
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _articuloQueryService = articuloQueryService ?? throw new ArgumentNullException(nameof(articuloQueryService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _ticketPrinterService = ticketPrinterService ?? throw new ArgumentNullException(nameof(ticketPrinterService));
        _exportadorPdf = exportadorPdf ?? throw new ArgumentNullException(nameof(exportadorPdf));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _crearValidator = crearValidator ?? throw new ArgumentNullException(nameof(crearValidator));
        _consultaValidator = consultaValidator ?? throw new ArgumentNullException(nameof(consultaValidator));
    }

    public async Task<PresupuestoDto> EmitirAsync(CrearPresupuestoDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var hoy = ObtenerHoy();
        int idPresupuesto;

        try
        {
            var presupuesto = await ConstruirPresupuestoAsync(dto, hoy, idPresupuestoReemplazado: null, cancellationToken);

            await _presupuestoRepository.AddAsync(presupuesto, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            idPresupuesto = presupuesto.Id;
        }
        finally
        {
            // El contexto vive lo que la pantalla: los artículos leídos con tracking no deben quedar retenidos (D-15).
            _unitOfWork.DescartarCambios();
        }

        return await _consultas.ObtenerAsync(idPresupuesto, hoy, cancellationToken)
            ?? throw new InvalidOperationException($"El presupuesto {idPresupuesto} recién guardado no se pudo leer.");
    }

    public async Task<PresupuestoDto> ReemplazarAsync(
        int idPresupuestoOriginal,
        CrearPresupuestoDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _crearValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var hoy = ObtenerHoy();
        int idNuevo;

        try
        {
            var original = await _presupuestoRepository.GetByIdAsync(idPresupuestoOriginal, cancellationToken)
                ?? throw new DomainException($"No se encontró el presupuesto {idPresupuestoOriginal}.");

            // El corregido es una cotización nueva: vigencia desde hoy (PE-05) y precios del catálogo de hoy (PE-06).
            var nuevo = await ConstruirPresupuestoAsync(dto, hoy, original.Id, cancellationToken);

            // Si el original no está pendiente lanza TransicionPresupuestoInvalidaException antes de tocar la base.
            original.Reemplazar();

            // Un solo SaveChanges (PE-02): cerrar el original y emitir el nuevo son todo o nada. El row_version del
            // original detecta que otra terminal lo cobró o lo anuló, y el índice único impide un segundo reemplazo (PE-07).
            await _presupuestoRepository.UpdateAsync(original, cancellationToken);
            await _presupuestoRepository.AddAsync(nuevo, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            idNuevo = nuevo.Id;
        }
        catch (Exception ex) when (ex is ConflictoDeConcurrenciaException or RestriccionDeUnicidadException)
        {
            throw new DomainException(
                $"El presupuesto N.º {idPresupuestoOriginal} cambió mientras lo corregía (por ejemplo, se cobró). Vuelva a abrirlo.",
                ex);
        }
        finally
        {
            _unitOfWork.DescartarCambios();
        }

        return await _consultas.ObtenerAsync(idNuevo, hoy, cancellationToken)
            ?? throw new InvalidOperationException($"El presupuesto {idNuevo} recién guardado no se pudo leer.");
    }

    public async Task CambiarTituloAsync(int idPresupuesto, string? titulo, CancellationToken cancellationToken = default)
    {
        try
        {
            var presupuesto = await _presupuestoRepository.GetByIdAsync(idPresupuesto, cancellationToken)
                ?? throw new DomainException($"No se encontró el presupuesto {idPresupuesto}.");

            // El agregado normaliza y valida la longitud (PE-08).
            presupuesto.CambiarTitulo(titulo);

            await _presupuestoRepository.UpdateAsync(presupuesto, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictoDeConcurrenciaException ex)
        {
            throw new DomainException($"Otro usuario modificó el presupuesto N.º {idPresupuesto}. Vuelva a abrirlo.", ex);
        }
        finally
        {
            _unitOfWork.DescartarCambios();
        }
    }

    public async Task<PresupuestosPaginadosDto> ListarAsync(ConsultaPresupuestosDto consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        await _consultaValidator.ValidateAndThrowAsync(consulta, cancellationToken);

        return await _consultas.ListarAsync(consulta, ObtenerHoy(), cancellationToken);
    }

    public Task<IReadOnlyList<ClienteConPresupuestosDto>> ListarClientesConPresupuestosAsync(CancellationToken cancellationToken = default)
    {
        return _consultas.ListarClientesConPresupuestosAsync(cancellationToken);
    }

    public Task<PresupuestoDto?> ObtenerAsync(int idPresupuesto, CancellationToken cancellationToken = default)
    {
        return _consultas.ObtenerAsync(idPresupuesto, ObtenerHoy(), cancellationToken);
    }

    public async Task<PresupuestoParaVentaDto> RecuperarParaVentaAsync(int idPresupuesto, CancellationToken cancellationToken = default)
    {
        // Lectura sin tracking: recuperar no modifica nada; la conversión ocurre recién al cobrar (PS-12).
        var presupuesto = await ObtenerExistenteAsync(idPresupuesto, cancellationToken);

        if (presupuesto.Estado != EstadoPresupuestoEnum.Pendiente)
        {
            throw new TransicionPresupuestoInvalidaException(presupuesto.Estado, "recuperar para la venta");
        }

        var disponibilidades = await _articuloQueryService.ObtenerDisponibilidadAsync(
            presupuesto.Items.Select(i => i.IdArticulo).Distinct().ToList(),
            cancellationToken);
        var disponibilidadPorId = disponibilidades.ToDictionary(d => d.IdArticulo);

        var renglones = new List<RenglonPresupuestoParaVentaDto>();
        var discrepancias = new List<DiscrepanciaPrecioPresupuestoDto>();
        var faltantes = new List<FaltanteStockDto>();
        var noDisponibles = new List<string>();

        foreach (var item in presupuesto.Items)
        {
            if (!disponibilidadPorId.TryGetValue(item.IdArticulo, out var disponible))
            {
                noDisponibles.Add(item.Descripcion);
                continue;
            }

            // Vigente: se cobra lo pactado aunque el catálogo haya cambiado (PS-02). Vencido: todo a catálogo (PS-03).
            var precioACobrar = presupuesto.EstaVigente ? item.PrecioUnitarioPactado : disponible.PrecioVenta;

            renglones.Add(new RenglonPresupuestoParaVentaDto
            {
                IdArticulo = item.IdArticulo,
                CodigoBarras = disponible.CodigoBarras,
                Descripcion = item.Descripcion,
                Cantidad = item.Cantidad,
                PrecioPactado = item.PrecioUnitarioPactado,
                PrecioActual = disponible.PrecioVenta,
                PrecioACobrar = precioACobrar,
                EsServicio = disponible.EsServicio,
                StockActual = disponible.StockActual
            });

            if (!presupuesto.EstaVigente && disponible.PrecioVenta != item.PrecioUnitarioPactado)
            {
                discrepancias.Add(new DiscrepanciaPrecioPresupuestoDto
                {
                    IdArticulo = item.IdArticulo,
                    Descripcion = item.Descripcion,
                    PrecioPactado = item.PrecioUnitarioPactado,
                    PrecioActualCatalogo = disponible.PrecioVenta
                });
            }

            if (!disponible.EsServicio && item.Cantidad > disponible.StockActual)
            {
                faltantes.Add(FaltantesStock.Crear(disponible, item.Cantidad));
            }
        }

        return new PresupuestoParaVentaDto
        {
            IdPresupuesto = presupuesto.IdPresupuesto,
            Titulo = presupuesto.Titulo,
            IdCliente = presupuesto.IdCliente,
            ClienteNombre = presupuesto.ClienteNombre,
            EstaVigente = presupuesto.EstaVigente,
            FechaVencimiento = presupuesto.FechaVencimiento,
            Descuento = presupuesto.Descuento,
            Renglones = renglones,
            Discrepancias = discrepancias,
            Faltantes = faltantes,
            ArticulosNoDisponibles = noDisponibles
        };
    }

    public async Task AnularAsync(int idPresupuesto, CancellationToken cancellationToken = default)
    {
        try
        {
            var presupuesto = await _presupuestoRepository.GetByIdAsync(idPresupuesto, cancellationToken)
                ?? throw new DomainException($"No se encontró el presupuesto {idPresupuesto}.");

            presupuesto.Anular();

            // El row_version vive en PRESUPUESTOS: UpdateAsync marca la raíz como modificada para que el SaveChanges
            // lo verifique y detecte, por ejemplo, que otra terminal lo convirtió en venta mientras tanto (PS-13).
            await _presupuestoRepository.UpdateAsync(presupuesto, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            // El contexto vive lo que la pantalla: una entidad cargada y no guardada quedaría retenida (D-15).
            _unitOfWork.DescartarCambios();
        }
    }

    public async Task ImprimirAsync(int idPresupuesto, CancellationToken cancellationToken = default)
    {
        var presupuesto = await ObtenerExistenteAsync(idPresupuesto, cancellationToken);

        await _ticketPrinterService.ImprimirPresupuestoAsync(presupuesto, cancellationToken);
    }

    public async Task ExportarPdfAsync(int idPresupuesto, Stream destino, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destino);

        var presupuesto = await ObtenerExistenteAsync(idPresupuesto, cancellationToken);

        await _exportadorPdf.ExportarAsync(presupuesto, destino, cancellationToken);
    }

    /// <summary>
    /// Arma un presupuesto nuevo a partir del DTO: carga los artículos activos y el cliente, toma precio y descripción
    /// del catálogo y deja que el agregado valide sus invariantes. Lo comparten la emisión y la corrección.
    /// </summary>
    private async Task<Presupuesto> ConstruirPresupuestoAsync(
        CrearPresupuestoDto dto,
        DateOnly hoy,
        int? idPresupuestoReemplazado,
        CancellationToken cancellationToken)
    {
        // 1. Cargar los artículos activos y el cliente. No se consulta la caja ni el turno: emitir no mueve
        //    plata ni stock (PS-17).
        var idsArticulos = dto.Items.Select(i => i.IdArticulo).Distinct().ToList();
        var articulos = await _articuloRepository.FindAsync(
            a => idsArticulos.Contains(a.Id),
            includeDeleted: false,
            cancellationToken);
        var articulosPorId = articulos.ToDictionary(a => a.Id);

        if (dto.IdCliente is int idCliente)
        {
            _ = await _clienteRepository.GetByIdAsync(idCliente, includeDeleted: false, cancellationToken)
                ?? throw new DomainException($"No se encontró ningún cliente activo con el identificador {idCliente}.");
        }

        // 2. El precio y la descripción salen del artículo, no del DTO (D-11, PS-11). El precio que vio el
        //    cajero solo sirve para detectar que el ticket quedó desactualizado.
        var renglones = new List<RenglonAPresupuestar>();
        foreach (var item in dto.Items)
        {
            if (!articulosPorId.TryGetValue(item.IdArticulo, out var articulo))
            {
                throw new DomainException($"El artículo '{item.Descripcion}' ya no está disponible: fue dado de baja.");
            }

            if (articulo.PrecioVenta != item.PrecioUnitarioPactado)
            {
                throw new DomainException(
                    $"El precio de '{articulo.Descripcion}' cambió de {item.PrecioUnitarioPactado:C} a {articulo.PrecioVenta:C}. Vuelva a cargar el artículo en el ticket.");
            }

            renglones.Add(new RenglonAPresupuestar(articulo.Id, articulo.Descripcion, item.Cantidad, articulo.PrecioVenta));
        }

        // 3. El agregado valida sus invariantes y calcula los totales.
        return Presupuesto.Emitir(
            dto.IdUsuario,
            dto.IdCliente,
            dto.Titulo,
            renglones,
            dto.Descuento,
            dto.VigenciaDias,
            hoy,
            idPresupuestoReemplazado);
    }

    private async Task<PresupuestoDto> ObtenerExistenteAsync(int idPresupuesto, CancellationToken cancellationToken)
    {
        return await _consultas.ObtenerAsync(idPresupuesto, ObtenerHoy(), cancellationToken)
            ?? throw new DomainException($"No se encontró el presupuesto {idPresupuesto}.");
    }

    /// <summary>
    /// Fecha calendario local de hoy (PS-09). Sale del <see cref="TimeProvider"/> y no de <c>DateTime.Now</c>, así la
    /// vigencia es testeable.
    /// </summary>
    private DateOnly ObtenerHoy()
    {
        return DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);
    }
}
