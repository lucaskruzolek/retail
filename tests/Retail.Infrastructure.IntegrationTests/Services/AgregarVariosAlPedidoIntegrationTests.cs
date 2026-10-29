using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Services;
using Retail.Application.Validators.Pedidos;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// <see cref="PedidoService.AgregarVariosAlPedidoAsync"/> de punta a punta contra SQL Server Express, con el
/// repositorio, la unidad de trabajo y las consultas reales (A-17).
/// </summary>
public class AgregarVariosAlPedidoIntegrationTests : PedidosPruebaBase
{
    public AgregarVariosAlPedidoIntegrationTests()
        : base("PedidoLote")
    {
    }

    private static PedidoService CrearServicio(RetailDbContext contexto)
    {
        var (repositorio, unidadDeTrabajo) = NuevoRepositorio(contexto);

        return new PedidoService(
            repositorio,
            unidadDeTrabajo,
            new PedidoQueryService(contexto),
            new CatalogoProveedorQueryService(contexto),
            new AgregarVariosAlPedidoValidator(),
            new CambiarCantidadRenglonValidator(),
            new CancelarPedidoValidator());
    }

    private static AgregarVariosAlPedidoDto CrearLote(int idUsuario, params (int IdCatalogo, int Cantidad)[] renglones)
    {
        return new AgregarVariosAlPedidoDto
        {
            IdUsuario = idUsuario,
            Renglones = renglones
                .Select(r => new RenglonAAgregarDto { IdCatalogoProveedor = r.IdCatalogo, Cantidad = r.Cantidad })
                .ToList()
        };
    }

    [Fact]
    public async Task AgregarVariosAlPedido_DosProveedores_SumaAlBorradorExistenteYCreaElOtro()
    {
        // Arrange
        var proveedorA = await SembrarProveedorAsync("Proveedor A", "30-11111111-8");
        var proveedorB = await SembrarProveedorAsync("Proveedor B", "30-11223344-6");
        var itemA = await SembrarItemAsync(proveedorA);
        var itemB = await SembrarItemAsync(proveedorB, costo: 50m);
        var borradorA = await SembrarBorradorAsync(proveedorA, (itemA.Articulo!, itemA.Catalogo));
        await using var contexto = NuevoContexto();
        var servicio = CrearServicio(contexto);

        // Act
        var resultado = await servicio.AgregarVariosAlPedidoAsync(
            CrearLote(IdUsuario, (itemA.Catalogo.Id, 3), (itemB.Catalogo.Id, 4)));

        // Assert
        resultado.Omitidos.Should().BeEmpty();
        resultado.Pedidos.Should().HaveCount(2);

        await using var verificacion = NuevoContexto();
        var pedidos = await verificacion.Pedidos.Include(p => p.Detalles).AsNoTracking().ToListAsync();
        pedidos.Should().HaveCount(2).And.OnlyContain(p => p.Estado == EstadoPedidoEnum.Borrador);
        pedidos.Single(p => p.Id == borradorA.Id).Detalles.Single().Cantidad.Should().Be(5, "2 sembrados + 3 del lote");
        var nuevoB = pedidos.Single(p => p.IdProveedor == proveedorB.Id);
        nuevoB.Detalles.Single().Should().BeEquivalentTo(new
        {
            IdArticulo = itemB.Articulo!.Id,
            IdCatalogoProveedor = itemB.Catalogo.Id,
            Cantidad = 4,
            CostoPactado = 50m
        });
    }

    [Fact]
    public async Task AgregarVariosAlPedido_FallaElGuardadoDeUnProveedor_NoGuardaNingunBorrador()
    {
        // Arrange: el borrador nuevo de B referencia a un usuario inexistente y viola su FK. El de A ya existe y solo
        // se modificaría: si el lote no fuera atómico, A quedaría con el renglón agregado.
        var proveedorA = await SembrarProveedorAsync("Proveedor A", "30-11111111-8");
        var proveedorB = await SembrarProveedorAsync("Proveedor B", "30-11223344-6");
        var itemA = await SembrarItemAsync(proveedorA);
        var itemB = await SembrarItemAsync(proveedorB);
        var borradorA = await SembrarBorradorAsync(proveedorA, (itemA.Articulo!, itemA.Catalogo));
        const int usuarioInexistente = 999_999;
        await using var contexto = NuevoContexto();
        var servicio = CrearServicio(contexto);

        // Act
        var act = () => servicio.AgregarVariosAlPedidoAsync(
            CrearLote(usuarioInexistente, (itemA.Catalogo.Id, 3), (itemB.Catalogo.Id, 4)));

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();

        await using var verificacion = NuevoContexto();
        var pedidos = await verificacion.Pedidos.Include(p => p.Detalles).AsNoTracking().ToListAsync();
        pedidos.Should().ContainSingle().Which.Id.Should().Be(borradorA.Id);
        pedidos.Single().Detalles.Single().Cantidad.Should().Be(2, "la transacción se revirtió entera");
    }
}
