using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Pedidos;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class PedidoServiceTests
{
    private const int IdProveedor = 7;
    private const int IdCatalogo = 11;
    private const int IdArticulo = 21;
    private const int IdUsuario = 3;

    private readonly IRepository<Pedido> _pedidos = Substitute.For<IRepository<Pedido>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPedidoQueryService _consultas = Substitute.For<IPedidoQueryService>();
    private readonly ICatalogoProveedorQueryService _catalogo = Substitute.For<ICatalogoProveedorQueryService>();
    private readonly PedidoService _service;

    public PedidoServiceTests()
    {
        _service = new PedidoService(
            _pedidos,
            _unitOfWork,
            _consultas,
            _catalogo,
            new AgregarVariosAlPedidoValidator(),
            new CambiarCantidadRenglonValidator(),
            new CancelarPedidoValidator());
    }

    private static CatalogoProveedor CrearItem(int id = IdCatalogo, int idProveedor = IdProveedor)
    {
        return new CatalogoProveedor
        {
            Id = id,
            IdProveedor = idProveedor,
            CodigoProveedor = id == IdCatalogo ? "LIB-001" : $"LIB-{id}",
            DescripcionProveedor = id == IdCatalogo ? "Cuaderno rayado" : $"Ítem {id}",
            CostoReposicion = 1500m
        };
    }

    private static AgregarVariosAlPedidoDto CrearLote(params int[] idsCatalogo)
    {
        var ids = idsCatalogo.Length == 0 ? new[] { IdCatalogo } : idsCatalogo;

        return new AgregarVariosAlPedidoDto
        {
            IdUsuario = IdUsuario,
            Renglones = ids.Select(id => new RenglonAAgregarDto { IdCatalogoProveedor = id, Cantidad = 4 }).ToList()
        };
    }

    private static Pedido CrearBorrador(int id = 100)
    {
        var pedido = Pedido.CrearBorrador(IdProveedor, IdUsuario);
        pedido.Id = id;
        pedido.AgregarRenglon(IdArticulo, IdCatalogo, "LIB-001", "Cuaderno rayado", 2, 1500m);

        return pedido;
    }

    private static PedidoResumenDto CrearResumen(int id = 100)
    {
        return new PedidoResumenDto { IdPedido = id, IdProveedor = IdProveedor, Estado = EstadoPedidoEnum.Borrador };
    }

    /// <summary>
    /// Prepara el catálogo con los ítems dados y vincula a un artículo solo los indicados en <paramref name="vinculados"/>
    /// (el artículo de cada ítem es su id + 10).
    /// </summary>
    private void PrepararCatalogo(IReadOnlyList<CatalogoProveedor> items, params int[] vinculados)
    {
        _catalogo.ObtenerPorIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(items);
        IReadOnlyDictionary<int, int> articulos = vinculados.ToDictionary(id => id, id => id + 10);
        _consultas.ObtenerArticulosVinculadosAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(articulos);
    }

    private void PrepararCatalogoYArticulo()
    {
        PrepararCatalogo(new[] { CrearItem() }, IdCatalogo);
    }

    private void PrepararPedidoExistente(Pedido pedido)
    {
        _pedidos.GetByIdWithIncludesAsync(pedido.Id, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Pedido, object>>[]>())
            .Returns(pedido);
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_SinBorrador_CreaBorradorConElRenglon()
    {
        PrepararCatalogoYArticulo();
        _consultas.ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns((int?)null);
        _consultas.ObtenerResumenAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(CrearResumen(0));

        var resultado = await _service.AgregarVariosAlPedidoAsync(CrearLote());

        resultado.Pedidos.Should().ContainSingle();
        resultado.Omitidos.Should().BeEmpty();
        await _pedidos.Received(1).AddAsync(
            Arg.Is<Pedido>(p => p.Estado == EstadoPedidoEnum.Borrador
                && p.IdProveedor == IdProveedor
                && p.IdUsuario == IdUsuario
                && p.Detalles.Count == 1
                && p.Detalles.Single().IdArticulo == IdArticulo
                && p.Detalles.Single().CodigoProveedor == "LIB-001"
                && p.Detalles.Single().Descripcion == "Cuaderno rayado"
                && p.Detalles.Single().Cantidad == 4
                && p.Detalles.Single().CostoPactado == 1500m),
            Arg.Any<CancellationToken>());
        await _pedidos.DidNotReceive().UpdateAsync(Arg.Any<Pedido>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_BorradorExistente_AgregaAlMismoPedido()
    {
        PrepararCatalogoYArticulo();
        var existente = CrearBorrador();
        PrepararPedidoExistente(existente);
        _consultas.ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns(existente.Id);
        _consultas.ObtenerResumenAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(CrearResumen());

        var resultado = await _service.AgregarVariosAlPedidoAsync(CrearLote());

        resultado.Pedidos.Single().IdPedido.Should().Be(existente.Id);
        existente.Detalles.Single().Cantidad.Should().Be(6);
        await _pedidos.Received(1).UpdateAsync(existente, Arg.Any<CancellationToken>());
        await _pedidos.DidNotReceive().AddAsync(Arg.Any<Pedido>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_DosProveedores_GuardaAmbosBorradoresEnUnSoloSaveChanges()
    {
        const int otroProveedor = 8;
        const int otroItem = 12;
        PrepararCatalogo(new[] { CrearItem(), CrearItem(otroItem, otroProveedor) }, IdCatalogo, otroItem);
        var existente = CrearBorrador();
        PrepararPedidoExistente(existente);
        _consultas.ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns(existente.Id);
        _consultas.ObtenerIdBorradorAsync(otroProveedor, Arg.Any<CancellationToken>()).Returns((int?)null);
        _consultas.ObtenerResumenAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(CrearResumen());

        var resultado = await _service.AgregarVariosAlPedidoAsync(CrearLote(IdCatalogo, otroItem));

        resultado.Pedidos.Should().HaveCount(2);
        await _pedidos.Received(1).UpdateAsync(existente, Arg.Any<CancellationToken>());
        await _pedidos.Received(1).AddAsync(
            Arg.Is<Pedido>(p => p.IdProveedor == otroProveedor && p.Detalles.Single().IdCatalogoProveedor == otroItem),
            Arg.Any<CancellationToken>());
        // Una sola transacción para todo el lote (A-17).
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_ItemInexistente_LoOmiteYAgregaLosDemas()
    {
        const int inexistente = 99;
        PrepararCatalogoYArticulo();
        _consultas.ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns((int?)null);
        _consultas.ObtenerResumenAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(CrearResumen());

        var resultado = await _service.AgregarVariosAlPedidoAsync(CrearLote(IdCatalogo, inexistente));

        resultado.Pedidos.Should().ContainSingle();
        resultado.Omitidos.Should().ContainSingle().Which.Should().BeEquivalentTo(new RenglonOmitidoDto
        {
            IdCatalogoProveedor = inexistente,
            Motivo = MotivoOmisionPedidoEnum.ItemInexistente
        });
        await _pedidos.Received(1).AddAsync(Arg.Is<Pedido>(p => p.Detalles.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_ItemSinIncorporar_LoOmiteConSuCodigoYDescripcion()
    {
        const int sinIncorporar = 12;
        PrepararCatalogo(new[] { CrearItem(), CrearItem(sinIncorporar) }, IdCatalogo);
        _consultas.ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns((int?)null);
        _consultas.ObtenerResumenAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(CrearResumen());

        var resultado = await _service.AgregarVariosAlPedidoAsync(CrearLote(IdCatalogo, sinIncorporar));

        resultado.Omitidos.Should().ContainSingle().Which.Should().BeEquivalentTo(new RenglonOmitidoDto
        {
            IdCatalogoProveedor = sinIncorporar,
            CodigoProveedor = "LIB-12",
            Descripcion = "Ítem 12",
            Motivo = MotivoOmisionPedidoEnum.SinIncorporar
        });
        await _pedidos.Received(1).AddAsync(Arg.Is<Pedido>(p => p.Detalles.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_TodosOmitidos_NoGuardaNiCreaBorrador()
    {
        PrepararCatalogo(new[] { CrearItem() });

        var resultado = await _service.AgregarVariosAlPedidoAsync(CrearLote());

        resultado.Pedidos.Should().BeEmpty();
        resultado.Omitidos.Should().ContainSingle(o => o.Motivo == MotivoOmisionPedidoEnum.SinIncorporar);
        await _pedidos.DidNotReceive().AddAsync(Arg.Any<Pedido>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_DtoInvalido_LanzaValidationException()
    {
        var dto = new AgregarVariosAlPedidoDto
        {
            IdUsuario = IdUsuario,
            Renglones = new[] { new RenglonAAgregarDto { IdCatalogoProveedor = IdCatalogo, Cantidad = 0 } }
        };

        var act = () => _service.AgregarVariosAlPedidoAsync(dto);

        await act.Should().ThrowAsync<ValidationException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_ViolacionDeUnicidad_ReintentaUnaVezYGuarda()
    {
        PrepararCatalogoYArticulo();
        _consultas.ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns((int?)null);
        _consultas.ObtenerResumenAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(CrearResumen());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new RestriccionDeUnicidadException()), Task.FromResult(1));

        var resultado = await _service.AgregarVariosAlPedidoAsync(CrearLote());

        resultado.Pedidos.Should().ContainSingle();
        await _consultas.Received(2).ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_ConflictoDeConcurrencia_ReintentaUnaVezYGuarda()
    {
        PrepararCatalogoYArticulo();
        var existente = CrearBorrador();
        PrepararPedidoExistente(existente);
        _consultas.ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns(existente.Id);
        _consultas.ObtenerResumenAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(CrearResumen());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new ConflictoDeConcurrenciaException()), Task.FromResult(1));

        var resultado = await _service.AgregarVariosAlPedidoAsync(CrearLote());

        resultado.Pedidos.Single().IdPedido.Should().Be(existente.Id);
        await _consultas.Received(2).ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarVariosAlPedidoAsync_DosFallos_LanzaDomainExceptionLegible()
    {
        PrepararCatalogoYArticulo();
        _consultas.ObtenerIdBorradorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns((int?)null);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new RestriccionDeUnicidadException());

        var act = () => _service.AgregarVariosAlPedidoAsync(CrearLote());

        var excepcion = await act.Should().ThrowAsync<DomainException>();
        excepcion.WithMessage("Otro usuario está modificando*");
        excepcion.Which.InnerException.Should().BeOfType<RestriccionDeUnicidadException>();
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerVistaPreviaAsync_DelegaEnQueryService()
    {
        var ids = new[] { IdCatalogo };
        var esperado = new VistaPreviaPedidoDto();
        _consultas.ObtenerVistaPreviaAsync(ids, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _service.ObtenerVistaPreviaAsync(ids);

        resultado.Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task EmitirAsync_Borrador_ActualizaRaizGuardaYDescartaCambios()
    {
        var pedido = CrearBorrador();
        PrepararPedidoExistente(pedido);

        await _service.EmitirAsync(pedido.Id);

        pedido.Estado.Should().Be(EstadoPedidoEnum.Emitido);
        Received.InOrder(() =>
        {
            _pedidos.UpdateAsync(pedido, Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _unitOfWork.DescartarCambios();
        });
    }

    [Fact]
    public async Task EmitirAsync_PedidoInexistente_LanzaDomainException()
    {
        _pedidos.GetByIdWithIncludesAsync(5, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Pedido, object>>[]>())
            .Returns((Pedido?)null);

        var act = () => _service.EmitirAsync(5);

        await act.Should().ThrowAsync<DomainException>().WithMessage("El pedido 5 no existe.");
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task EmitirAsync_SinRenglones_PropagaDomainExceptionYDescartaCambios()
    {
        var vacio = CrearBorrador();
        vacio.QuitarRenglon(IdCatalogo);
        PrepararPedidoExistente(vacio);

        var act = () => _service.EmitirAsync(vacio.Id);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*sin renglones*");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task CambiarCantidadAsync_RenglonExistente_ActualizaYGuarda()
    {
        var pedido = CrearBorrador();
        PrepararPedidoExistente(pedido);

        await _service.CambiarCantidadAsync(new CambiarCantidadRenglonDto { IdPedido = pedido.Id, IdCatalogoProveedor = IdCatalogo, Cantidad = 9 });

        pedido.Detalles.Single().Cantidad.Should().Be(9);
        await _pedidos.Received(1).UpdateAsync(pedido, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task QuitarRenglonAsync_RenglonExistente_QuitaYGuarda()
    {
        var pedido = CrearBorrador();
        PrepararPedidoExistente(pedido);

        await _service.QuitarRenglonAsync(pedido.Id, IdCatalogo);

        pedido.Detalles.Should().BeEmpty();
        await _pedidos.Received(1).UpdateAsync(pedido, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task CancelarAsync_PedidoEmitido_CancelaYGuarda()
    {
        var pedido = CrearBorrador();
        pedido.Emitir();
        PrepararPedidoExistente(pedido);

        await _service.CancelarAsync(new CancelarPedidoDto { IdPedido = pedido.Id, Motivo = "Ya no se necesita" });

        pedido.Estado.Should().Be(EstadoPedidoEnum.Cancelado);
        pedido.MotivoCancelacion.Should().Be("Ya no se necesita");
        await _pedidos.Received(1).UpdateAsync(pedido, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
    }

    [Fact]
    public async Task CancelarAsync_MotivoVacio_LanzaValidationException()
    {
        var act = () => _service.CancelarAsync(new CancelarPedidoDto { IdPedido = 1, Motivo = " " });

        await act.Should().ThrowAsync<ValidationException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarEmitidosPorProveedorAsync_DelegaEnQueryService()
    {
        IReadOnlyList<PedidoEmitidoDto> esperado = new[]
        {
            new PedidoEmitidoDto { IdPedido = 1, FechaEmision = DateTime.UtcNow, CantidadRenglones = 2, TotalEstimado = 3000m }
        };
        _consultas.ListarEmitidosPorProveedorAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _service.ListarEmitidosPorProveedorAsync(IdProveedor);

        resultado.Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task ObtenerParaCompraAsync_DelegaEnQueryService()
    {
        var esperado = new PedidoParaCompraDto { IdPedido = 1, IdProveedor = IdProveedor, Renglones = Array.Empty<RenglonPedidoParaCompraDto>() };
        _consultas.ObtenerParaCompraAsync(1, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _service.ObtenerParaCompraAsync(1);

        resultado.Should().BeSameAs(esperado);
    }
}
