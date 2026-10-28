using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Exceptions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Persistencia del agregado <see cref="Pedido"/> (RF-28) contra SQL Server Express: mapeo, índices, checks y
/// control de concurrencia. Los tests de rechazo insertan con SQL crudo y saltan el Dominio a propósito: simulan un
/// script o una herramienta externa, que es lo que la defensa en profundidad del motor relacional debe atajar.
/// </summary>
public class PedidoPersistenciaTests : PedidosPruebaBase
{
    public PedidoPersistenciaTests()
        : base("Pedido")
    {
    }

    [Fact]
    public async Task Pedido_ConRenglones_SePersisteYSeRecupera()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item1 = await SembrarItemAsync(proveedor, costo: 10.10m);
        var item2 = await SembrarItemAsync(proveedor, costo: 25m);
        var pedido = await SembrarBorradorAsync(proveedor, (item1.Articulo!, item1.Catalogo), (item2.Articulo!, item2.Catalogo));
        pedido.Emitir();
        await Contexto.SaveChangesAsync();
        Contexto.ChangeTracker.Clear();

        // Act
        var leido = await Contexto.Pedidos.Include(p => p.Detalles).SingleAsync(p => p.Id == pedido.Id);

        // Assert
        leido.Estado.Should().Be(EstadoPedidoEnum.Emitido);
        leido.IdProveedor.Should().Be(proveedor.Id);
        leido.IdUsuario.Should().Be(IdUsuario);
        leido.FechaEmision.Should().NotBeNull();
        leido.Detalles.Should().HaveCount(2);
        var primero = leido.Detalles.Single(d => d.IdCatalogoProveedor == item1.Catalogo.Id);
        primero.IdArticulo.Should().Be(item1.Articulo!.Id);
        primero.CodigoProveedor.Should().Be(item1.Catalogo.CodigoProveedor);
        primero.Descripcion.Should().Be(item1.Catalogo.DescripcionProveedor);
        primero.Cantidad.Should().Be(2);
        primero.CostoPactado.Should().Be(10.10m);
        leido.TotalEstimado.Should().Be(70.20m);
    }

    [Fact]
    public async Task IndiceBorrador_DosBorradoresMismoProveedor_LanzaRestriccionDeUnicidad()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var (repositorio, unidadDeTrabajo) = NuevoRepositorio(Contexto);
        await repositorio.AddAsync(Pedido.CrearBorrador(proveedor.Id, IdUsuario));
        await unidadDeTrabajo.SaveChangesAsync();

        await repositorio.AddAsync(Pedido.CrearBorrador(proveedor.Id, IdUsuario));

        // Act
        var act = () => unidadDeTrabajo.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<RestriccionDeUnicidadException>();
    }

    [Fact]
    public async Task IndiceBorrador_BorradorYEmitidoMismoProveedor_SePermite()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var emitido = await SembrarBorradorAsync(proveedor, (item.Articulo!, item.Catalogo));
        emitido.Emitir();
        await Contexto.SaveChangesAsync();

        var (repositorio, unidadDeTrabajo) = NuevoRepositorio(Contexto);
        await repositorio.AddAsync(Pedido.CrearBorrador(proveedor.Id, IdUsuario));

        // Act
        var act = () => unidadDeTrabajo.SaveChangesAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UkRenglon_MismoItemDosVeces_Rechazado()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var pedido = await SembrarBorradorAsync(proveedor, (item.Articulo!, item.Catalogo));

        // Act: el renglón del mismo ítem ya existe; se inserta otro por fuera del Dominio
        var act = () => InsertarRenglonCrudoAsync(pedido.Id, item.Articulo!.Id, item.Catalogo.Id, cantidad: 1, costo: 5m);

        // Assert
        var excepcion = await act.Should().ThrowAsync<SqlException>();
        excepcion.Which.Message.Should().Contain("UX_DETALLE_PEDIDOS_pedido_catalogo");
    }

    [Fact]
    public async Task CheckCantidad_Cero_Rechazado()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var pedido = await SembrarBorradorAsync(proveedor);

        // Act
        var act = () => InsertarRenglonCrudoAsync(pedido.Id, item.Articulo!.Id, item.Catalogo.Id, cantidad: 0, costo: 5m);

        // Assert
        var excepcion = await act.Should().ThrowAsync<SqlException>();
        excepcion.Which.Message.Should().Contain("CK_DETALLE_PEDIDOS_Cantidad");
    }

    [Fact]
    public async Task CheckCosto_Cero_Rechazado()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var pedido = await SembrarBorradorAsync(proveedor);

        // Act
        var act = () => InsertarRenglonCrudoAsync(pedido.Id, item.Articulo!.Id, item.Catalogo.Id, cantidad: 1, costo: 0m);

        // Assert
        var excepcion = await act.Should().ThrowAsync<SqlException>();
        excepcion.Which.Message.Should().Contain("CK_DETALLE_PEDIDOS_Costo");
    }

    [Fact]
    public async Task QuitarRenglon_MarcaBajaLogicaYPermiteVolverAAgregarElItem()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item1 = await SembrarItemAsync(proveedor);
        var item2 = await SembrarItemAsync(proveedor);
        var pedido = await SembrarBorradorAsync(proveedor, (item1.Articulo!, item1.Catalogo), (item2.Articulo!, item2.Catalogo));
        var idPedido = pedido.Id;
        (await ContarFilasDeRenglonesAsync(idPedido)).Should().Be(2);
        Contexto.ChangeTracker.Clear();

        var (repositorio, unidadDeTrabajo) = NuevoRepositorio(Contexto);
        var cargado = await repositorio.GetByIdWithIncludesAsync(idPedido, default, p => p.Detalles);
        cargado!.QuitarRenglon(item1.Catalogo.Id);

        // Act
        await repositorio.UpdateAsync(cargado);
        await unidadDeTrabajo.SaveChangesAsync();
        unidadDeTrabajo.DescartarCambios();

        // Assert: la fila sigue (Ley 3, D4) pero el filtro global la oculta del agregado.
        (await ContarFilasDeRenglonesAsync(idPedido)).Should().Be(2);
        var recargado = await repositorio.GetByIdWithIncludesAsync(idPedido, default, p => p.Detalles);
        recargado!.Detalles.Should().ContainSingle(d => d.IdCatalogoProveedor == item2.Catalogo.Id);

        // El índice único filtrado permite volver a agregar el ítem quitado como un renglón nuevo.
        recargado.AgregarRenglon(
            item1.Articulo!.Id,
            item1.Catalogo.Id,
            item1.Catalogo.CodigoProveedor,
            item1.Catalogo.DescripcionProveedor,
            3,
            item1.Catalogo.CostoReposicion);
        await repositorio.UpdateAsync(recargado);
        await unidadDeTrabajo.SaveChangesAsync();

        (await ContarFilasDeRenglonesAsync(idPedido)).Should().Be(3);
    }

    [Fact]
    public async Task RowVersion_DosContextosMarcanRecibido_ElSegundoLanzaConflicto()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var item = await SembrarItemAsync(proveedor);
        var pedido = await SembrarBorradorAsync(proveedor, (item.Articulo!, item.Catalogo));
        pedido.Emitir();
        await Contexto.SaveChangesAsync();

        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        var (repositorioA, unidadA) = NuevoRepositorio(contextoA);
        var (repositorioB, unidadB) = NuevoRepositorio(contextoB);
        var pedidoA = (await repositorioA.GetByIdWithIncludesAsync(pedido.Id, default, p => p.Detalles))!;
        var pedidoB = (await repositorioB.GetByIdWithIncludesAsync(pedido.Id, default, p => p.Detalles))!;

        pedidoA.MarcarRecibido();
        await repositorioA.UpdateAsync(pedidoA);
        await unidadA.SaveChangesAsync();

        pedidoB.MarcarRecibido();
        await repositorioB.UpdateAsync(pedidoB);

        // Act
        var act = () => unidadB.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
    }

    /// <summary>
    /// Guarda la decisión E2: sin <c>UpdateAsync</c> el pedido no se marcaría como modificado y solo se emitiría un
    /// INSERT en DETALLE_PEDIDOS, de modo que el renglón se colaría en un pedido ya emitido por otra terminal. Con
    /// <c>UpdateAsync</c> también se envía el UPDATE de la fila del pedido, y su <c>row_version</c> vencido hace
    /// fallar el guardado.
    /// </summary>
    [Fact]
    public async Task AgregarRenglon_PedidoEmitidoEnOtroContexto_LanzaConflicto()
    {
        // Arrange
        var proveedor = await SembrarProveedorAsync();
        var existente = await SembrarItemAsync(proveedor);
        var nuevo = await SembrarItemAsync(proveedor);
        var borrador = await SembrarBorradorAsync(proveedor, (existente.Articulo!, existente.Catalogo));

        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        var (repositorioA, unidadA) = NuevoRepositorio(contextoA);
        var (repositorioB, unidadB) = NuevoRepositorio(contextoB);
        var pedidoB = (await repositorioB.GetByIdWithIncludesAsync(borrador.Id, default, p => p.Detalles))!;
        var pedidoA = (await repositorioA.GetByIdWithIncludesAsync(borrador.Id, default, p => p.Detalles))!;

        pedidoA.Emitir();
        await repositorioA.UpdateAsync(pedidoA);
        await unidadA.SaveChangesAsync();

        pedidoB.AgregarRenglon(
            nuevo.Articulo!.Id,
            nuevo.Catalogo.Id,
            nuevo.Catalogo.CodigoProveedor,
            nuevo.Catalogo.DescripcionProveedor,
            1,
            nuevo.Catalogo.CostoReposicion);
        await repositorioB.UpdateAsync(pedidoB);

        // Act
        var act = () => unidadB.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();

        await using var contextoFresco = NuevoContexto();
        var leido = await contextoFresco.Pedidos.Include(p => p.Detalles).SingleAsync(p => p.Id == borrador.Id);
        leido.Estado.Should().Be(EstadoPedidoEnum.Emitido);
        leido.Detalles.Should().HaveCount(1);
    }

    private async Task InsertarRenglonCrudoAsync(int idPedido, int idArticulo, int idCatalogo, int cantidad, decimal costo)
    {
        await Contexto.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO DETALLE_PEDIDOS
                (id_pedido, id_articulo, id_catalogo_proveedor, codigo_proveedor, descripcion, cantidad, costo_pactado, created_at)
            VALUES
                ({idPedido}, {idArticulo}, {idCatalogo}, N'X-1', N'Renglón crudo', {cantidad}, {costo}, SYSUTCDATETIME())");
    }

    private async Task<int> ContarFilasDeRenglonesAsync(int idPedido)
    {
        // Cuenta las filas físicas, incluso las marcadas con deleted_at: verifica que quitar un renglón no lo borra.
        return await Contexto.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM DETALLE_PEDIDOS WHERE id_pedido = {idPedido}")
            .SingleAsync();
    }
}
