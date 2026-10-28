using FluentAssertions;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Consultas de <see cref="PedidoQueryService"/> (RF-28, RF-29, §5.2 del rector del Track 4.3) contra SQL Server Express.
/// </summary>
public class PedidoQueryServiceTests : PedidosPruebaBase
{
    public PedidoQueryServiceTests()
        : base("PedidoQuery")
    {
    }

    [Fact]
    public async Task ListarEmitidosPorProveedor_SoloEmitidosDelProveedorEnOrdenDescendente()
    {
        // Arrange
        var proveedorA = await SembrarProveedorAsync("Proveedor A", "30-11111111-8");
        var proveedorB = await SembrarProveedorAsync("Proveedor B", "30-11223344-6");
        var viejo = await SembrarEmitidoAsync(proveedorA);
        var reciente = await SembrarEmitidoAsync(proveedorA);
        var recibido = await SembrarEmitidoAsync(proveedorA);
        recibido.MarcarRecibido();
        var cancelado = await SembrarEmitidoAsync(proveedorA);
        cancelado.Cancelar("Ya no se necesita");
        await SembrarEmitidoAsync(proveedorB);
        var itemBorrador = await SembrarItemAsync(proveedorA);
        await SembrarBorradorAsync(proveedorA, (itemBorrador.Articulo!, itemBorrador.Catalogo));
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ListarEmitidosPorProveedorAsync(proveedorA.Id);

        // Assert
        resultado.Select(p => p.IdPedido).Should().Equal(reciente.Id, viejo.Id);
        resultado.Should().OnlyContain(p => p.CantidadRenglones == 1 && p.TotalEstimado == 20m);
    }

    [Fact]
    public async Task ObtenerParaCompra_PedidoNoEmitido_DevuelveNull()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var borrador = await SembrarBorradorAsync(proveedor, (item.Articulo!, item.Catalogo));
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ObtenerParaCompraAsync(borrador.Id);
        var inexistente = await servicio.ObtenerParaCompraAsync(999_999);

        // Assert
        resultado.Should().BeNull();
        inexistente.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerParaCompra_PedidoEmitido_DevuelveRenglonesConDatosDelArticulo()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor, costo: 12.50m);
        var emitido = await SembrarBorradorAsync(proveedor, (item.Articulo!, item.Catalogo));
        emitido.Emitir();
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ObtenerParaCompraAsync(emitido.Id);

        // Assert
        resultado.Should().NotBeNull();
        resultado!.IdPedido.Should().Be(emitido.Id);
        resultado.IdProveedor.Should().Be(proveedor.Id);
        var renglon = resultado.Renglones.Should().ContainSingle().Which;
        renglon.IdArticulo.Should().Be(item.Articulo!.Id);
        renglon.CodigoBarras.Should().Be(item.Articulo.CodigoBarras);
        renglon.DescripcionArticulo.Should().Be(item.Articulo.Descripcion);
        renglon.CodigoProveedor.Should().Be(item.Catalogo.CodigoProveedor);
        renglon.Cantidad.Should().Be(2);
        renglon.CostoPactado.Should().Be(12.50m);
    }

    [Fact]
    public async Task ObtenerParaCompra_ArticuloDadoDeBaja_OmiteRenglon()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var vigente = await SembrarItemAsync(proveedor);
        var dadoDeBaja = await SembrarItemAsync(proveedor);
        var emitido = await SembrarBorradorAsync(proveedor, (vigente.Articulo!, vigente.Catalogo), (dadoDeBaja.Articulo!, dadoDeBaja.Catalogo));
        emitido.Emitir();
        dadoDeBaja.Articulo!.MarkAsDeleted();
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var resultado = await servicio.ObtenerParaCompraAsync(emitido.Id);

        // Assert
        resultado!.Renglones.Should().ContainSingle().Which.IdArticulo.Should().Be(vigente.Articulo!.Id);
    }

    [Fact]
    public async Task ObtenerIdArticuloVinculado_ItemVinculado_DevuelveArticulo()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var id = await servicio.ObtenerIdArticuloVinculadoAsync(item.Catalogo.Id);

        // Assert
        id.Should().Be(item.Articulo!.Id);
    }

    [Fact]
    public async Task ObtenerIdArticuloVinculado_ItemSinVincular_DevuelveNull()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor, vincularArticulo: false);
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var id = await servicio.ObtenerIdArticuloVinculadoAsync(item.Catalogo.Id);

        // Assert
        id.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerIdBorrador_ConBorrador_DevuelveId()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var borrador = await SembrarBorradorAsync(proveedor, (item.Articulo!, item.Catalogo));
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var id = await servicio.ObtenerIdBorradorAsync(proveedor.Id);

        // Assert
        id.Should().Be(borrador.Id);
    }

    [Fact]
    public async Task ObtenerIdBorrador_SoloEmitidos_DevuelveNull()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        await SembrarEmitidoAsync(proveedor);
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var id = await servicio.ObtenerIdBorradorAsync(proveedor.Id);

        // Assert
        id.Should().BeNull();
    }

    [Fact]
    public async Task Listar_FiltraPorEstadoYProveedorYPagina()
    {
        // Arrange
        var proveedorA = await SembrarProveedorAsync("Proveedor A", "30-11111111-8");
        var proveedorB = await SembrarProveedorAsync("Proveedor B", "30-11223344-6");
        var primero = await SembrarEmitidoAsync(proveedorA, costo: 10.10m);
        var segundo = await SembrarEmitidoAsync(proveedorA, costo: 5m);
        await SembrarEmitidoAsync(proveedorB);
        var recibido = await SembrarEmitidoAsync(proveedorA);
        recibido.MarcarRecibido();
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var paginaUno = await servicio.ListarAsync(new ConsultaPedidosDto
        {
            Estado = EstadoPedidoEnum.Emitido,
            IdProveedor = proveedorA.Id,
            Pagina = 1,
            TamanoPagina = 1
        });
        var paginaDos = await servicio.ListarAsync(new ConsultaPedidosDto
        {
            Estado = EstadoPedidoEnum.Emitido,
            IdProveedor = proveedorA.Id,
            Pagina = 2,
            TamanoPagina = 1
        });

        // Assert: más reciente primero
        paginaUno.TotalRegistros.Should().Be(2);
        paginaUno.TotalPaginas.Should().Be(2);
        var filaUno = paginaUno.Items.Should().ContainSingle().Which;
        filaUno.IdPedido.Should().Be(segundo.Id);
        filaUno.RazonSocialProveedor.Should().Be("Proveedor A");
        filaUno.Estado.Should().Be(EstadoPedidoEnum.Emitido);
        filaUno.CantidadRenglones.Should().Be(1);
        filaUno.TotalEstimado.Should().Be(10m);
        var filaDos = paginaDos.Items.Should().ContainSingle().Which;
        filaDos.IdPedido.Should().Be(primero.Id);
        filaDos.TotalEstimado.Should().Be(20.20m);
    }

    [Fact]
    public async Task ObtenerDetalle_ArticuloDadoDeBaja_LoIncluyeYLoMarca()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var vigente = await SembrarItemAsync(proveedor);
        var dadoDeBaja = await SembrarItemAsync(proveedor);
        var pedido = await SembrarBorradorAsync(proveedor, (vigente.Articulo!, vigente.Catalogo), (dadoDeBaja.Articulo!, dadoDeBaja.Catalogo));
        dadoDeBaja.Articulo!.MarkAsDeleted();
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var detalle = await servicio.ObtenerDetalleAsync(pedido.Id);

        // Assert
        detalle.Should().NotBeNull();
        detalle!.RazonSocialProveedor.Should().Be(proveedor.RazonSocial);
        detalle.Estado.Should().Be(EstadoPedidoEnum.Borrador);
        detalle.CantidadRenglones.Should().Be(2);
        detalle.TotalEstimado.Should().Be(400m);
        detalle.Renglones.Select(r => r.IdArticulo).Should().Equal(vigente.Articulo!.Id, dadoDeBaja.Articulo.Id);
        detalle.Renglones.Select(r => r.ArticuloDadoDeBaja).Should().Equal(false, true);
        detalle.Renglones[0].Subtotal.Should().Be(200m);
    }

    [Fact]
    public async Task ObtenerDetalle_PedidoDadoDeBaja_DevuelveNull()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var pedido = await SembrarBorradorAsync(proveedor, (item.Articulo!, item.Catalogo));
        pedido.MarkAsDeleted();
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var detalle = await servicio.ObtenerDetalleAsync(pedido.Id);

        // Assert
        detalle.Should().BeNull();
    }

    /// <summary>
    /// Guarda un pedido emitido de un renglón (cantidad 2) sobre un ítem nuevo del proveedor.
    /// </summary>
    private async Task<Pedido> SembrarEmitidoAsync(Proveedor proveedor, decimal costo = 10m)
    {
        var item = await SembrarItemAsync(proveedor, costo: costo);
        var pedido = await SembrarBorradorAsync(proveedor, (item.Articulo!, item.Catalogo));
        pedido.Emitir();
        await Contexto.SaveChangesAsync();
        return pedido;
    }
}
