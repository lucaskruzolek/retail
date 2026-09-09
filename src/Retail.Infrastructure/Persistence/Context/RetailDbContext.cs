using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
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

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Marca> Marcas => Set<Marca>();

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
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
