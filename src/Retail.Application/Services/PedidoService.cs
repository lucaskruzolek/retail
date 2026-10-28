using FluentValidation;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Implementación de los casos de uso del pedido a proveedor (RF-28) y de su contrato de frontera con Compras
/// (RF-29). Toda modificación pasa por la raíz <see cref="Pedido"/> (Ley 2); las lecturas se delegan en
/// <see cref="IPedidoQueryService"/> (Ley 8).
/// </summary>
public class PedidoService : IPedidoService
{
    private const int MaximoIntentosAgregar = 2;

    private readonly IRepository<Pedido> _pedidos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPedidoQueryService _consultas;
    private readonly ICatalogoProveedorQueryService _catalogo;
    private readonly IValidator<AgregarAlPedidoDto> _agregarValidator;
    private readonly IValidator<CambiarCantidadRenglonDto> _cambiarCantidadValidator;
    private readonly IValidator<CancelarPedidoDto> _cancelarValidator;

    public PedidoService(
        IRepository<Pedido> pedidos,
        IUnitOfWork unitOfWork,
        IPedidoQueryService consultas,
        ICatalogoProveedorQueryService catalogo,
        IValidator<AgregarAlPedidoDto> agregarValidator,
        IValidator<CambiarCantidadRenglonDto> cambiarCantidadValidator,
        IValidator<CancelarPedidoDto> cancelarValidator)
    {
        _pedidos = pedidos ?? throw new ArgumentNullException(nameof(pedidos));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _catalogo = catalogo ?? throw new ArgumentNullException(nameof(catalogo));
        _agregarValidator = agregarValidator ?? throw new ArgumentNullException(nameof(agregarValidator));
        _cambiarCantidadValidator = cambiarCantidadValidator ?? throw new ArgumentNullException(nameof(cambiarCantidadValidator));
        _cancelarValidator = cancelarValidator ?? throw new ArgumentNullException(nameof(cancelarValidator));
    }

    public async Task<PedidoResumenDto> AgregarAlPedidoAsync(AgregarAlPedidoDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _agregarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var item = await _catalogo.ObtenerPorIdAsync(dto.IdCatalogoProveedor, cancellationToken)
            ?? throw new DomainException($"El ítem de catálogo {dto.IdCatalogoProveedor} no existe.");

        var idArticulo = await _consultas.ObtenerIdArticuloVinculadoAsync(item.Id, cancellationToken)
            ?? throw new DomainException(
                $"El ítem '{item.DescripcionProveedor}' (código {item.CodigoProveedor}) todavía no es un artículo. " +
                "Incorpórelo al catálogo de artículos antes de pedirlo.");

        // Dos terminales pueden crear a la vez el primer borrador de un proveedor (violan el índice único filtrado,
        // A-7) o agregar al mismo borrador (chocan en row_version, E2). En ambos casos se relee y se reintenta una vez:
        // la segunda pasada ve el borrador ajeno y suma su renglón encima.
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await AgregarUnaVezAsync(dto, item, idArticulo, cancellationToken);
            }
            catch (Exception ex) when (ex is RestriccionDeUnicidadException or ConflictoDeConcurrenciaException)
            {
                if (intento >= MaximoIntentosAgregar)
                {
                    throw new DomainException("Otro usuario está modificando el pedido de este proveedor. Intente de nuevo.", ex);
                }

                _unitOfWork.DescartarCambios();
            }
        }
    }

    public Task<PedidosPaginadosDto> ListarPedidosAsync(ConsultaPedidosDto consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return _consultas.ListarAsync(consulta, cancellationToken);
    }

    public Task<PedidoDetalleDto?> ObtenerDetalleAsync(int idPedido, CancellationToken cancellationToken = default)
    {
        return _consultas.ObtenerDetalleAsync(idPedido, cancellationToken);
    }

    public Task<IReadOnlyList<PedidoEmitidoDto>> ListarEmitidosPorProveedorAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        return _consultas.ListarEmitidosPorProveedorAsync(idProveedor, cancellationToken);
    }

    public Task<PedidoParaCompraDto?> ObtenerParaCompraAsync(int idPedido, CancellationToken cancellationToken = default)
    {
        return _consultas.ObtenerParaCompraAsync(idPedido, cancellationToken);
    }

    public async Task CambiarCantidadAsync(CambiarCantidadRenglonDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _cambiarCantidadValidator.ValidateAndThrowAsync(dto, cancellationToken);

        try
        {
            var pedido = await CargarPedidoAsync(dto.IdPedido, cancellationToken);
            pedido.CambiarCantidad(dto.IdCatalogoProveedor, dto.Cantidad);

            await GuardarRaizAsync(pedido, cancellationToken);
        }
        finally
        {
            // El contexto vive lo que la pantalla: una entidad cargada y no guardada quedaría retenida y la próxima
            // lectura vería datos viejos (D-15).
            _unitOfWork.DescartarCambios();
        }
    }

    public async Task QuitarRenglonAsync(int idPedido, int idCatalogoProveedor, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idPedido, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idCatalogoProveedor, 0);

        try
        {
            var pedido = await CargarPedidoAsync(idPedido, cancellationToken);
            pedido.QuitarRenglon(idCatalogoProveedor);

            await GuardarRaizAsync(pedido, cancellationToken);
        }
        finally
        {
            // D-15: ver CambiarCantidadAsync.
            _unitOfWork.DescartarCambios();
        }
    }

    public async Task EmitirAsync(int idPedido, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idPedido, 0);

        try
        {
            var pedido = await CargarPedidoAsync(idPedido, cancellationToken);
            pedido.Emitir();

            await GuardarRaizAsync(pedido, cancellationToken);
        }
        finally
        {
            // D-15: ver CambiarCantidadAsync.
            _unitOfWork.DescartarCambios();
        }
    }

    public async Task CancelarAsync(CancelarPedidoDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _cancelarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        try
        {
            var pedido = await CargarPedidoAsync(dto.IdPedido, cancellationToken);
            pedido.Cancelar(dto.Motivo);

            await GuardarRaizAsync(pedido, cancellationToken);
        }
        finally
        {
            // D-15: ver CambiarCantidadAsync.
            _unitOfWork.DescartarCambios();
        }
    }

    /// <summary>
    /// Un intento de agregar el renglón: relee el borrador del proveedor (o crea uno), agrega, guarda y devuelve el
    /// resumen. Los errores de concurrencia y unicidad los resuelve quien lo llama.
    /// </summary>
    private async Task<PedidoResumenDto> AgregarUnaVezAsync(
        AgregarAlPedidoDto dto,
        CatalogoProveedor item,
        int idArticulo,
        CancellationToken cancellationToken)
    {
        int idPedido;

        try
        {
            var idBorrador = await _consultas.ObtenerIdBorradorAsync(item.IdProveedor, cancellationToken);
            var existente = idBorrador.HasValue
                ? await CargarPedidoAsync(idBorrador.Value, cancellationToken)
                : null;
            var pedido = existente ?? Pedido.CrearBorrador(item.IdProveedor, dto.IdUsuario);

            // La foto del renglón (código, descripción y costo) es la del catálogo en este momento.
            pedido.AgregarRenglon(
                idArticulo,
                item.Id,
                item.CodigoProveedor,
                item.DescripcionProveedor,
                dto.Cantidad,
                item.CostoReposicion);

            if (existente is null)
            {
                await _pedidos.AddAsync(pedido, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await GuardarRaizAsync(pedido, cancellationToken);
            }

            idPedido = pedido.Id;
        }
        finally
        {
            // D-15: éxito o fallo, el contexto no retiene el agregado (si el guardado falló, ya se descartó solo).
            _unitOfWork.DescartarCambios();
        }

        return await _consultas.ObtenerResumenAsync(idPedido, cancellationToken)
            ?? throw new InvalidOperationException($"El pedido {idPedido} recién guardado no se pudo leer.");
    }

    private async Task<Pedido> CargarPedidoAsync(int idPedido, CancellationToken cancellationToken)
    {
        return await _pedidos.GetByIdWithIncludesAsync(idPedido, cancellationToken, p => p.Detalles)
            ?? throw new DomainException($"El pedido {idPedido} no existe.");
    }

    private async Task GuardarRaizAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        // El row_version vive en PEDIDOS. Si solo cambian filas de DETALLE_PEDIDOS no se emite un UPDATE de PEDIDOS y
        // la concurrencia nunca se controlaría. Update marca la raíz como Modified, así que toda operación versiona
        // el agregado completo (E2).
        await _pedidos.UpdateAsync(pedido, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
