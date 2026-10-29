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
    private readonly IValidator<AgregarVariosAlPedidoDto> _agregarValidator;
    private readonly IValidator<CambiarCantidadRenglonDto> _cambiarCantidadValidator;
    private readonly IValidator<CancelarPedidoDto> _cancelarValidator;

    public PedidoService(
        IRepository<Pedido> pedidos,
        IUnitOfWork unitOfWork,
        IPedidoQueryService consultas,
        ICatalogoProveedorQueryService catalogo,
        IValidator<AgregarVariosAlPedidoDto> agregarValidator,
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

    public async Task<ResultadoAgregarAlPedidoDto> AgregarVariosAlPedidoAsync(AgregarVariosAlPedidoDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _agregarValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var ids = dto.Renglones.Select(r => r.IdCatalogoProveedor).ToList();
        var items = (await _catalogo.ObtenerPorIdsAsync(ids, cancellationToken)).ToDictionary(i => i.Id);
        var articulos = await _consultas.ObtenerArticulosVinculadosAsync(ids, cancellationToken);

        // Los ítems que no se pueden pedir se informan y no abortan el lote: el modal ya se los mostró al usuario
        // antes de confirmar (H2).
        var resueltos = new List<RenglonResuelto>();
        var omitidos = new List<RenglonOmitidoDto>();
        foreach (var renglon in dto.Renglones)
        {
            if (!items.TryGetValue(renglon.IdCatalogoProveedor, out var item))
            {
                omitidos.Add(new RenglonOmitidoDto
                {
                    IdCatalogoProveedor = renglon.IdCatalogoProveedor,
                    Motivo = MotivoOmisionPedidoEnum.ItemInexistente
                });
            }
            else if (!articulos.TryGetValue(item.Id, out var idArticulo))
            {
                omitidos.Add(new RenglonOmitidoDto
                {
                    IdCatalogoProveedor = item.Id,
                    CodigoProveedor = item.CodigoProveedor,
                    Descripcion = item.DescripcionProveedor,
                    Motivo = MotivoOmisionPedidoEnum.SinIncorporar
                });
            }
            else
            {
                resueltos.Add(new RenglonResuelto(item, idArticulo, renglon.Cantidad));
            }
        }

        if (resueltos.Count == 0)
        {
            // ObtenerPorIdsAsync rastrea los ítems: el contexto no debe retenerlos (D-15).
            _unitOfWork.DescartarCambios();
            return new ResultadoAgregarAlPedidoDto { Omitidos = omitidos };
        }

        var idsPedidos = await AgregarConReintentoAsync(dto.IdUsuario, resueltos, cancellationToken);

        var pedidos = new List<PedidoResumenDto>();
        foreach (var idPedido in idsPedidos)
        {
            pedidos.Add(await _consultas.ObtenerResumenAsync(idPedido, cancellationToken)
                ?? throw new InvalidOperationException($"El pedido {idPedido} recién guardado no se pudo leer."));
        }

        return new ResultadoAgregarAlPedidoDto { Pedidos = pedidos, Omitidos = omitidos };
    }

    public Task<VistaPreviaPedidoDto> ObtenerVistaPreviaAsync(IReadOnlyCollection<int> idsCatalogoProveedor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idsCatalogoProveedor);

        return _consultas.ObtenerVistaPreviaAsync(idsCatalogoProveedor, cancellationToken);
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

    private async Task<IReadOnlyList<int>> AgregarConReintentoAsync(
        int idUsuario,
        IReadOnlyList<RenglonResuelto> renglones,
        CancellationToken cancellationToken)
    {
        // Dos terminales pueden crear a la vez el primer borrador de un proveedor (violan el índice único filtrado,
        // A-7) o agregar al mismo borrador (chocan en row_version, E2). En ambos casos se relee y se reintenta una vez
        // el lote entero: la segunda pasada ve el borrador ajeno y suma sus renglones encima.
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await AgregarUnaVezAsync(idUsuario, renglones, cancellationToken);
            }
            catch (Exception ex) when (ex is RestriccionDeUnicidadException or ConflictoDeConcurrenciaException)
            {
                if (intento >= MaximoIntentosAgregar)
                {
                    throw new DomainException("Otro usuario está modificando el pedido de este proveedor. Intente de nuevo.", ex);
                }
            }
        }
    }

    /// <summary>
    /// Un intento de agregar el lote: por cada proveedor relee su borrador (o crea uno) y le agrega sus renglones;
    /// después guarda todos los borradores en un único <c>SaveChanges</c>, es decir, en una sola transacción (A-17).
    /// Devuelve los ids de los pedidos tocados. Los errores de concurrencia y unicidad los resuelve quien lo llama.
    /// </summary>
    private async Task<IReadOnlyList<int>> AgregarUnaVezAsync(
        int idUsuario,
        IReadOnlyList<RenglonResuelto> renglones,
        CancellationToken cancellationToken)
    {
        try
        {
            var pedidos = new List<Pedido>();
            foreach (var grupo in renglones.GroupBy(r => r.Item.IdProveedor))
            {
                var idBorrador = await _consultas.ObtenerIdBorradorAsync(grupo.Key, cancellationToken);
                var existente = idBorrador.HasValue
                    ? await CargarPedidoAsync(idBorrador.Value, cancellationToken)
                    : null;
                var pedido = existente ?? Pedido.CrearBorrador(grupo.Key, idUsuario);

                // La foto de cada renglón (código, descripción y costo) es la del catálogo en este momento.
                foreach (var renglon in grupo)
                {
                    pedido.AgregarRenglon(
                        renglon.IdArticulo,
                        renglon.Item.Id,
                        renglon.Item.CodigoProveedor,
                        renglon.Item.DescripcionProveedor,
                        renglon.Cantidad,
                        renglon.Item.CostoReposicion);
                }

                if (existente is null)
                {
                    await _pedidos.AddAsync(pedido, cancellationToken);
                }
                else
                {
                    // E2: ver GuardarRaizAsync. Acá no se guarda todavía: el lote se guarda junto, abajo.
                    await _pedidos.UpdateAsync(pedido, cancellationToken);
                }

                pedidos.Add(pedido);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return pedidos.Select(p => p.Id).ToList();
        }
        finally
        {
            // D-15: éxito o fallo, el contexto no retiene los agregados (si el guardado falló, ya se descartó solo).
            _unitOfWork.DescartarCambios();
        }
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

    /// <summary>
    /// Renglón del lote ya validado: el ítem de catálogo existe y está vinculado a un artículo.
    /// </summary>
    private sealed record RenglonResuelto(CatalogoProveedor Item, int IdArticulo, int Cantidad);
}
