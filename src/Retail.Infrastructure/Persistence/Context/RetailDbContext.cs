using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Context;

/// <summary>
/// Contexto principal de persistencia en Entity Framework Core 8 para la base de datos local de Retail.
/// </summary>
public class RetailDbContext : DbContext, IRetailDbContext
{
    public RetailDbContext(DbContextOptions<RetailDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Rol> Roles => Set<Rol>();

    public DbSet<Rubro> Rubros => Set<Rubro>();

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Marca> Marcas => Set<Marca>();

    public DbSet<TerminoClasificacion> TerminosClasificacion => Set<TerminoClasificacion>();

    public DbSet<Articulo> Articulos => Set<Articulo>();

    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    public DbSet<CatalogoProveedor> CatalogosProveedores => Set<CatalogoProveedor>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<CobranzaCliente> CobranzasClientes => Set<CobranzaCliente>();

    public DbSet<TurnoCaja> TurnosCaja => Set<TurnoCaja>();

    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();

    public DbSet<Venta> Ventas => Set<Venta>();

    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();

    public DbSet<PagoVenta> PagosVenta => Set<PagoVenta>();

    public DbSet<ComprobanteFiscal> ComprobantesFiscales => Set<ComprobanteFiscal>();

    public DbSet<Presupuesto> Presupuestos => Set<Presupuesto>();

    public DbSet<DetallePresupuesto> DetallesPresupuesto => Set<DetallePresupuesto>();

    public DbSet<Compra> Compras => Set<Compra>();

    public DbSet<DetalleCompra> DetallesCompra => Set<DetalleCompra>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<RegistroBackup> RegistrosBackup => Set<RegistroBackup>();
    public DbSet<ConfiguracionComercial> ConfiguracionesComerciales => Set<ConfiguracionComercial>();

    public DbSet<RegistroRestauracion> RegistrosRestauracion => Set<RegistroRestauracion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Cargar configuraciones Fluent API individuales desde este ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RetailDbContext).Assembly);

        // 2. Aplicar Global Query Filter para Soft Delete en todas las entidades derivadas de BaseEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
                var nullConstant = Expression.Constant(null, typeof(DateTime?));
                var condition = Expression.Equal(property, nullConstant);
                var lambda = Expression.Lambda(condition, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.MarkAsDeleted();
            }
            else if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified && entry.Entity is CatalogoProveedor)
            {
                UniformarColumnasDeClasificacion(entry);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Si cambió alguna columna de la clasificación sugerida de un ítem del catálogo, envía las tres (Track 6.6, M-82).
    /// EF Core manda en cada UPDATE solo las columnas modificadas, y un ítem sin marca o sin categoría deja la suya en
    /// nulo; así los UPDATE tienen formas distintas y cada lote de 42 queda con un texto distinto. SQL Server busca el
    /// plan por el texto exacto, de modo que compilaba un plan por lote (731 compilaciones y 146 s para clasificar
    /// 30.000 ítems). Con las tres columnas siempre presentes, los lotes repiten el texto y reutilizan el plan. Los
    /// valores guardados no cambian: a lo sumo se reescribe un nulo con un nulo.
    /// </summary>
    private static void UniformarColumnasDeClasificacion(EntityEntry entry)
    {
        string[] columnas =
        [
            nameof(CatalogoProveedor.IdCategoriaSugerida),
            nameof(CatalogoProveedor.IdMarcaSugerida),
            nameof(CatalogoProveedor.FechaClasificacion)
        ];

        if (!columnas.Any(c => entry.Property(c).IsModified))
        {
            return;
        }

        foreach (var columna in columnas)
        {
            entry.Property(columna).IsModified = true;
        }
    }
}
