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
    public async Task ObtenerArticulosVinculados_VinculadoYSinVincular_SoloDevuelveElVinculado()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var vinculado = await SembrarItemAsync(proveedor);
        var sinVincular = await SembrarItemAsync(proveedor, vincularArticulo: false);
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var articulos = await servicio.ObtenerArticulosVinculadosAsync(new[] { vinculado.Catalogo.Id, sinVincular.Catalogo.Id });

        // Assert
        articulos.Should().Equal(new Dictionary<int, int> { [vinculado.Catalogo.Id] = vinculado.Articulo!.Id });
    }

    [Fact]
    public async Task ObtenerArticulosVinculados_ArticuloDadoDeBaja_NoLoDevuelve()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        item.Articulo!.MarkAsDeleted();
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var articulos = await servicio.ObtenerArticulosVinculadosAsync(new[] { item.Catalogo.Id });

        // Assert
        articulos.Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerArticulosVinculados_FuenteAdicional_DevuelveElArticuloDelQueEsFuente()
    {
        // Arrange: el artículo tiene su principal en Sur y una fuente adicional en Norte (Track 4.3, J6)
        var sur = await SembrarProveedorAsync();
        var norte = await SembrarProveedorAsync("Mayorista Norte", "30-50001091-2");
        var principal = await SembrarItemAsync(sur);
        var adicional = await SembrarItemAsync(norte, vincularArticulo: false);
        adicional.Catalogo.AsignarArticulo(principal.Articulo!);
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var articulos = await servicio.ObtenerArticulosVinculadosAsync(new[] { principal.Catalogo.Id, adicional.Catalogo.Id });

        // Assert
        articulos.Should().Equal(new Dictionary<int, int>
        {
            [principal.Catalogo.Id] = principal.Articulo!.Id,
            [adicional.Catalogo.Id] = principal.Articulo!.Id
        });
    }

    [Fact]
    public async Task ObtenerVistaPrevia_ArticuloConVariasFuentes_LasTraeOrdenadasPorCostoConSuBorrador()
    {
        // Arrange (Track 4.3, K1): principal en Sur a 100 y adicional en Norte a 80, con 2 unidades ya pedidas a Norte
        var (sur, norte, principal, adicional) = await SembrarArticuloConDosFuentesAsync();
        var borradorNorte = await SembrarBorradorAsync(norte, (principal.Articulo!, adicional));
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act: se pide la principal, como desde Artículos (K2)
        var vista = await servicio.ObtenerVistaPreviaAsync(new[] { principal.Catalogo.Id });

        // Assert
        var renglon = vista.Renglones.Should().ContainSingle().Subject;
        renglon.IdCatalogoProveedor.Should().Be(principal.Catalogo.Id);
        renglon.Fuentes.Select(f => (f.IdCatalogoProveedor, f.EsPrincipal, f.CantidadEnBorrador)).Should().Equal(
            (adicional.Id, false, 2),
            (principal.Catalogo.Id, true, 0));
        vista.Destinos.Select(d => (d.IdProveedor, d.IdBorrador)).Should().BeEquivalentTo(new (int, int?)[]
        {
            (sur.Id, null),
            (norte.Id, borradorNorte.Id)
        });
    }

    [Fact]
    public async Task ObtenerVistaPrevia_ConProveedorPreferido_ArrancaEnLaFuenteDeEseProveedor()
    {
        // Arrange (K6)
        var (_, norte, principal, adicional) = await SembrarArticuloConDosFuentesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var vista = await servicio.ObtenerVistaPreviaAsync(new[] { principal.Catalogo.Id }, idProveedorPreferido: norte.Id);

        // Assert
        var renglon = vista.Renglones.Should().ContainSingle().Subject;
        renglon.IdCatalogoProveedor.Should().Be(adicional.Id);
        renglon.IdProveedor.Should().Be(norte.Id);
        renglon.CostoActual.Should().Be(80m);
        renglon.Fuentes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObtenerVistaPrevia_ProveedorPreferidoSinFuenteDelArticulo_ArrancaEnElItemPedido()
    {
        // Arrange (K2 cuando K6 no aplica)
        var sur = await SembrarProveedorAsync();
        var norte = await SembrarProveedorAsync("Mayorista Norte", "30-50001091-2");
        var item = await SembrarItemAsync(sur);
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var vista = await servicio.ObtenerVistaPreviaAsync(new[] { item.Catalogo.Id }, idProveedorPreferido: norte.Id);

        // Assert
        vista.Renglones.Should().ContainSingle().Which.IdCatalogoProveedor.Should().Be(item.Catalogo.Id);
    }

    /// <summary>
    /// Un artículo con su principal en Sur (costo 100) y una fuente adicional en Norte (costo 80).
    /// </summary>
    private async Task<(Proveedor Sur, Proveedor Norte, (Articulo? Articulo, CatalogoProveedor Catalogo) Principal, CatalogoProveedor Adicional)>
        SembrarArticuloConDosFuentesAsync()
    {
        var sur = await SembrarProveedorAsync();
        var norte = await SembrarProveedorAsync("Mayorista Norte", "30-50001091-2");
        var principal = await SembrarItemAsync(sur, costo: 100m);
        var adicional = await SembrarItemAsync(norte, vincularArticulo: false, costo: 80m);
        adicional.Catalogo.AsignarArticulo(principal.Articulo!);
        await Contexto.SaveChangesAsync();
        return (sur, norte, principal, adicional.Catalogo);
    }

    [Fact]
    public async Task ObtenerVistaPrevia_ConBorradorExistente_InformaDestinoStockYCantidadYaPedida()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var enBorrador = await SembrarItemAsync(proveedor, costo: 80m);
        var nuevo = await SembrarItemAsync(proveedor, costo: 120m);
        var borrador = await SembrarBorradorAsync(proveedor, (enBorrador.Articulo!, enBorrador.Catalogo));
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act: el orden pedido se respeta en los renglones.
        var vista = await servicio.ObtenerVistaPreviaAsync(new[] { nuevo.Catalogo.Id, enBorrador.Catalogo.Id });

        // Assert
        vista.Omitidos.Should().BeEmpty();
        vista.Destinos.Should().ContainSingle().Which.Should().BeEquivalentTo(new DestinoPedidoDto
        {
            IdProveedor = proveedor.Id,
            RazonSocialProveedor = proveedor.RazonSocial,
            IdBorrador = borrador.Id,
            CantidadRenglonesBorrador = 1
        });
        vista.Renglones.Select(r => r.IdCatalogoProveedor).Should().Equal(nuevo.Catalogo.Id, enBorrador.Catalogo.Id);
        vista.Renglones[0].Should().BeEquivalentTo(new RenglonVistaPreviaDto
        {
            IdCatalogoProveedor = nuevo.Catalogo.Id,
            IdProveedor = proveedor.Id,
            CodigoProveedor = nuevo.Catalogo.CodigoProveedor,
            Descripcion = nuevo.Catalogo.DescripcionProveedor,
            CostoActual = 120m,
            IdArticulo = nuevo.Articulo!.Id,
            StockActual = 5,
            StockMinimo = 1,
            CantidadEnBorrador = 0
        }, opciones => opciones.Excluding(r => r.Fuentes));
        vista.Renglones[1].CantidadEnBorrador.Should().Be(2);
    }

    [Fact]
    public async Task ObtenerVistaPrevia_SinBorradorEItemsNoPedibles_InformaDestinoNuevoYOmitidos()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var pedible = await SembrarItemAsync(proveedor);
        var sinIncorporar = await SembrarItemAsync(proveedor, vincularArticulo: false);
        const int inexistente = 999_999;
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var vista = await servicio.ObtenerVistaPreviaAsync(new[] { pedible.Catalogo.Id, sinIncorporar.Catalogo.Id, inexistente });

        // Assert
        var destino = vista.Destinos.Should().ContainSingle().Which;
        destino.IdBorrador.Should().BeNull("el proveedor no tiene borrador: se creará al confirmar");
        destino.CantidadRenglonesBorrador.Should().Be(0);
        vista.Renglones.Should().ContainSingle().Which.IdCatalogoProveedor.Should().Be(pedible.Catalogo.Id);
        vista.Omitidos.Should().BeEquivalentTo(new[]
        {
            new RenglonOmitidoDto
            {
                IdCatalogoProveedor = sinIncorporar.Catalogo.Id,
                CodigoProveedor = sinIncorporar.Catalogo.CodigoProveedor,
                Descripcion = sinIncorporar.Catalogo.DescripcionProveedor,
                Motivo = MotivoOmisionPedidoEnum.SinIncorporar
            },
            new RenglonOmitidoDto { IdCatalogoProveedor = inexistente, Motivo = MotivoOmisionPedidoEnum.ItemInexistente }
        });
    }

    [Fact]
    public async Task ObtenerVistaPrevia_RenglonQuitadoDelBorrador_NoCuentaComoYaPedido()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var quitado = await SembrarItemAsync(proveedor);
        var queda = await SembrarItemAsync(proveedor);
        var borrador = await SembrarBorradorAsync(proveedor, (quitado.Articulo!, quitado.Catalogo), (queda.Articulo!, queda.Catalogo));
        borrador.QuitarRenglon(quitado.Catalogo.Id);
        await Contexto.SaveChangesAsync();
        var servicio = new PedidoQueryService(NuevoContexto());

        // Act
        var vista = await servicio.ObtenerVistaPreviaAsync(new[] { quitado.Catalogo.Id });

        // Assert: la baja lógica del renglón (D4) lo saca de la cantidad ya pedida y del conteo de renglones.
        vista.Renglones.Single().CantidadEnBorrador.Should().Be(0);
        vista.Destinos.Single().CantidadRenglonesBorrador.Should().Be(1);
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
