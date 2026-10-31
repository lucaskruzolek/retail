using FluentValidation;
using Retail.Application.DTOs.Compras;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación del caso de uso de Compras a Proveedores (RF-19, RF-21).
/// Orquesta el registro transaccional ACID de facturas y remitos, el incremento de stock físico
/// y el recálculo automático de precios de venta por markup en el artículo adquirido y sus
/// presentaciones derivadas.
/// </summary>
public class CompraService : ICompraService
{
    private readonly IRepository<Compra> _compraRepository;
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IRepository<Proveedor> _proveedorRepository;
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearCompraDto> _validator;

    public CompraService(
        IRepository<Compra> compraRepository,
        IRepository<Articulo> articuloRepository,
        IRepository<Proveedor> proveedorRepository,
        IRepository<Usuario> usuarioRepository,
        IUnitOfWork unitOfWork,
        IValidator<CrearCompraDto> validator)
    {
        _compraRepository = compraRepository ?? throw new ArgumentNullException(nameof(compraRepository));
        _articuloRepository = articuloRepository ?? throw new ArgumentNullException(nameof(articuloRepository));
        _proveedorRepository = proveedorRepository ?? throw new ArgumentNullException(nameof(proveedorRepository));
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <inheritdoc/>
    public async Task<CompraResponseDto> RegistrarCompraAsync(
        CrearCompraDto dto,
        CancellationToken cancellationToken = default)
    {
        // ── FASE 1: Validación de contrato de entrada ─────────────────────────
        await _validator.ValidateAndThrowAsync(dto, cancellationToken);

        // ── FASE 2: Verificación de existencia de referencias ─────────────────
        var proveedor = await _proveedorRepository.GetByIdAsync(dto.IdProveedor, cancellationToken)
            ?? throw new DomainException($"El proveedor con Id {dto.IdProveedor} no existe o fue dado de baja.");

        _ = await _usuarioRepository.GetByIdAsync(dto.IdUsuario, cancellationToken)
            ?? throw new DomainException($"El usuario con Id {dto.IdUsuario} no existe o fue dado de baja.");

        // C-1: Verificar que el comprobante no esté registrado antes de cargar artículos.
        var comprobanteExiste = (await _compraRepository.FindAsync(
            c => c.IdProveedor == dto.IdProveedor
                 && c.TipoComprobante == dto.TipoComprobante.Trim()
                 && c.NumeroComprobante == dto.NumeroComprobante.Trim(),
            cancellationToken)).Count > 0;

        if (comprobanteExiste)
        {
            throw new DomainException(
                $"El comprobante {dto.TipoComprobante.Trim()} Nro. {dto.NumeroComprobante.Trim()} " +
                $"ya fue registrado para este proveedor.");
        }

        // ── FASE 3: Pre-carga optimizada de artículos y sus derivadas (RF-21) ─
        var idsArticulos = dto.Items.Select(i => i.IdArticulo).Distinct().ToHashSet();

        // Carga los artículos adquiridos con tracking para poder mutar stock y costos.
        var articulosCargados = await _articuloRepository.FindAsync(
            a => idsArticulos.Contains(a.Id),
            cancellationToken);

        // C-3: Diccionario O(1) por IdArticulo; evita recorrer la lista por cada renglón (AGENTS.md §Ley 8).
        var articulosPorId = articulosCargados.ToDictionary(a => a.Id);

        // Valida que todos los artículos solicitados existan y no sean servicios.
        foreach (var item in dto.Items)
        {
            if (!articulosPorId.TryGetValue(item.IdArticulo, out var articuloValidacion))
            {
                throw new DomainException($"El artículo con Id {item.IdArticulo} no existe o fue dado de baja.");
            }

            if (articuloValidacion.EsServicio)
            {
                throw new DomainException($"El artículo '{articuloValidacion.Descripcion}' es un servicio y no puede formar parte de una compra física.");
            }
        }

        // Pre-carga las presentaciones derivadas de los artículos adquiridos para la propagación (RF-21).
        // Se carga con tracking porque RecalcularCostoDesdeOrigen muta CostoReposicion y PrecioVenta.
        var presentacionesDerivadas = await _articuloRepository.FindAsync(
            a => a.IdArticuloOrigen.HasValue && idsArticulos.Contains(a.IdArticuloOrigen.Value),
            cancellationToken);

        // Índice para O(1) en el bucle interno: presentaciones agrupadas por IdArticuloOrigen.
        var presentacionesPorOrigen = presentacionesDerivadas
            .GroupBy(a => a.IdArticuloOrigen!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── FASE 4: Construcción del Agregado Compra (método fábrica DDD) ─────
        var compra = Compra.Registrar(
            dto.IdProveedor,
            dto.IdUsuario,
            dto.TipoComprobante,
            dto.NumeroComprobante,
            dto.FechaEmision);

        // ── FASE 5: Bucle atómico de renglones — stock, costos y precios ──────
        var articulosActualizados = 0;

        foreach (var item in dto.Items)
        {
            // C-3: Lookup O(1) desde el diccionario armado en FASE 3.
            var articulo = articulosPorId[item.IdArticulo];

            // 5a. Agregar renglón al agregado Compra (calcula subtotales internamente).
            compra.AgregarDetalle(item.IdArticulo, item.Cantidad, item.CostoUnitario);

            // 5b. Incrementar el stock físico (el dominio valida cantidad > 0 y rechaza servicios).
            articulo.IncrementarStock(item.Cantidad);

            // 5c. Recalcular precio de venta si el costo de reposición cambió (RF-19).
            if (item.CostoUnitario != articulo.CostoReposicion)
            {
                articulo.ActualizarCostoYRecalcularPrecio(item.CostoUnitario);
                articulosActualizados++;

                // 5d. Propagar el nuevo costo a las presentaciones derivadas (RF-21).
                if (presentacionesPorOrigen.TryGetValue(articulo.Id, out var derivadas))
                {
                    foreach (var derivada in derivadas)
                    {
                        // RecalcularCostoDesdeOrigen usa internamente UnidadesPorOrigen.
                        derivada.RecalcularCostoDesdeOrigen(articulo.CostoReposicion);
                        await _articuloRepository.UpdateAsync(derivada, cancellationToken);
                    }
                }
            }

            await _articuloRepository.UpdateAsync(articulo, cancellationToken);
        }

        // ── FASE 6: Persistencia atómica ACID en un único SaveChanges ─────────
        await _compraRepository.AddAsync(compra, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // C-4 (D-15): Liberar entidades tras guardar. La siguiente compra desde esta pantalla cargará
        // stock y row_version frescos y no chocará con un conflicto falso (mismo patrón que VentaService).
        _unitOfWork.DescartarCambios();

        // ── FASE 7: Proyección a DTO de respuesta ─────────────────────────────
        return new CompraResponseDto
        {
            IdCompra = compra.Id,
            IdProveedor = compra.IdProveedor,
            ProveedorRazonSocial = proveedor.RazonSocial,
            TipoComprobante = compra.TipoComprobante,
            NumeroComprobante = compra.NumeroComprobante,
            FechaEmision = compra.FechaEmision,
            Subtotal = compra.Subtotal,
            Total = compra.Total,
            ArticulosActualizados = articulosActualizados,
            Items = dto.Items,
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CompraResponseDto>> ListarComprasAsync(
        CancellationToken cancellationToken = default)
    {
        // Lectura proyectada sin tracking. Se carga con navegación al proveedor para mostrar la razón social.
        var compras = await _compraRepository.ListAllAsync(cancellationToken);

        return compras
            .Select(c => new CompraResponseDto
            {
                IdCompra = c.Id,
                IdProveedor = c.IdProveedor,
                ProveedorRazonSocial = c.Proveedor?.RazonSocial,
                TipoComprobante = c.TipoComprobante,
                NumeroComprobante = c.NumeroComprobante,
                FechaEmision = c.FechaEmision,
                Subtotal = c.Subtotal,
                Total = c.Total,
                ArticulosActualizados = 0, // No disponible en consultas de listado.
                Items = Array.Empty<DetalleCompraDto>(),
            })
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<CompraResponseDto?> ObtenerCompraPorIdAsync(
        int idCompra,
        CancellationToken cancellationToken = default)
    {
        var compra = await _compraRepository.GetByIdAsync(idCompra, cancellationToken);

        if (compra is null)
        {
            return null;
        }

        return new CompraResponseDto
        {
            IdCompra = compra.Id,
            IdProveedor = compra.IdProveedor,
            ProveedorRazonSocial = compra.Proveedor?.RazonSocial,
            TipoComprobante = compra.TipoComprobante,
            NumeroComprobante = compra.NumeroComprobante,
            FechaEmision = compra.FechaEmision,
            Subtotal = compra.Subtotal,
            Total = compra.Total,
            ArticulosActualizados = 0,
            Items = compra.Detalles
                .Select(d => new DetalleCompraDto
                {
                    IdArticulo = d.IdArticulo,
                    Cantidad = d.Cantidad,
                    CostoUnitario = d.CostoUnitario,
                })
                .ToList()
                .AsReadOnly(),
        };
    }
}
