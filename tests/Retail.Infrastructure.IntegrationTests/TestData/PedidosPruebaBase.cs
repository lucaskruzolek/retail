using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.TestData;

/// <summary>
/// Base de las pruebas de integración de pedidos: crea y borra una base propia <c>RetailDb_Test_*</c> por prueba y
/// ofrece los datos maestros (proveedor, usuario, artículos e ítems de catálogo) que un pedido necesita.
/// </summary>
public abstract class PedidosPruebaBase : IAsyncLifetime
{
    private readonly string _dbName;
    private readonly DbContextOptions<RetailDbContext> _opciones;
    private int _secuencia;

    protected PedidosPruebaBase(string prefijo)
    {
        _dbName = $"RetailDb_Test_{prefijo}_{Guid.NewGuid():N}";
        _opciones = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;
    }

    protected RetailDbContext Contexto { get; private set; } = null!;

    protected int IdUsuario { get; private set; }

    public async Task InitializeAsync()
    {
        Contexto = new RetailDbContext(_opciones);
        await Contexto.Database.MigrateAsync();

        var usuario = Usuario.Crear(
            "operador.pedidos", "Ana", "Perez", "hash-de-prueba", RolUsuarioEnum.Gerente, "ana@example.com");
        Contexto.Usuarios.Add(usuario);
        await Contexto.SaveChangesAsync();
        IdUsuario = usuario.Id;
    }

    public async Task DisposeAsync()
    {
        await Contexto.Database.EnsureDeletedAsync();
        await Contexto.DisposeAsync();
    }

    /// <summary>
    /// Un contexto independiente sobre la misma base, para simular a otra terminal.
    /// </summary>
    protected RetailDbContext NuevoContexto()
    {
        return new RetailDbContext(_opciones);
    }

    protected static (Repository<Pedido> Repositorio, UnitOfWork UnidadDeTrabajo) NuevoRepositorio(RetailDbContext contexto)
    {
        return (new Repository<Pedido>(contexto), new UnitOfWork(contexto));
    }

    protected async Task<Proveedor> SembrarProveedorAsync(string razonSocial = "Distribuidora Sur", string cuit = "30-11111111-8")
    {
        var proveedor = Proveedor.Crear(razonSocial, cuit);
        Contexto.Proveedores.Add(proveedor);
        await Contexto.SaveChangesAsync();
        return proveedor;
    }

    /// <summary>
    /// Crea un ítem de catálogo del proveedor y, si se pide, el artículo vigente vinculado a él.
    /// </summary>
    protected async Task<(Articulo? Articulo, CatalogoProveedor Catalogo)> SembrarItemAsync(
        Proveedor proveedor,
        bool vincularArticulo = true,
        decimal costo = 100m)
    {
        var numero = ++_secuencia;
        var catalogo = new CatalogoProveedor
        {
            IdProveedor = proveedor.Id,
            CodigoProveedor = $"COD-{numero}",
            DescripcionProveedor = $"Ítem de catálogo {numero}",
            CostoReposicion = costo
        };
        Contexto.CatalogosProveedores.Add(catalogo);
        await Contexto.SaveChangesAsync();

        if (!vincularArticulo)
        {
            return (null, catalogo);
        }

        var articulo = new Articulo
        {
            Descripcion = $"Artículo {numero}",
            CodigoBarras = $"779000000{numero:D4}",
            CostoReposicion = costo,
            PorcentajeGanancia = 40m,
            PrecioVenta = costo * 1.4m,
            StockActual = 5,
            StockMinimo = 1
        };
        catalogo.AsignarArticulo(articulo);
        articulo.MarcarFuentePrincipal(catalogo);
        Contexto.Articulos.Add(articulo);
        await Contexto.SaveChangesAsync();
        return (articulo, catalogo);
    }

    /// <summary>
    /// Guarda un borrador del proveedor con un renglón por cada ítem indicado (cantidad 2, costo del catálogo).
    /// </summary>
    protected async Task<Pedido> SembrarBorradorAsync(
        Proveedor proveedor,
        params (Articulo Articulo, CatalogoProveedor Catalogo)[] items)
    {
        var pedido = Pedido.CrearBorrador(proveedor.Id, IdUsuario);
        foreach (var (articulo, catalogo) in items)
        {
            pedido.AgregarRenglon(
                articulo.Id,
                catalogo.Id,
                catalogo.CodigoProveedor,
                catalogo.DescripcionProveedor,
                2,
                catalogo.CostoReposicion);
        }

        Contexto.Pedidos.Add(pedido);
        await Contexto.SaveChangesAsync();
        return pedido;
    }
}
