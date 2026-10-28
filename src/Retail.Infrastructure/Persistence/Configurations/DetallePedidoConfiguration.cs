using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="DetallePedido"/> a DETALLE_PEDIDOS (RF-28). Entidad interna del agregado <see cref="Pedido"/>.
/// </summary>
public class DetallePedidoConfiguration : IEntityTypeConfiguration<DetallePedido>
{
    public void Configure(EntityTypeBuilder<DetallePedido> builder)
    {
        builder.ToTable("DETALLE_PEDIDOS", t =>
        {
            // Defensa en profundidad de las invariantes del agregado (el dominio ya las exige).
            t.HasCheckConstraint("CK_DETALLE_PEDIDOS_Cantidad", "[cantidad] >= 1");
            t.HasCheckConstraint("CK_DETALLE_PEDIDOS_Costo", "[costo_pactado] > 0");
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id_detalle_pedido");

        builder.Property(d => d.IdPedido)
            .HasColumnName("id_pedido")
            .IsRequired();

        builder.Property(d => d.IdArticulo)
            .HasColumnName("id_articulo")
            .IsRequired();

        builder.Property(d => d.IdCatalogoProveedor)
            .HasColumnName("id_catalogo_proveedor")
            .IsRequired();

        // Un ítem de catálogo figura una sola vez por pedido: agregarlo de nuevo suma la cantidad (dominio). El filtro
        // excluye los renglones quitados (baja lógica, D4): un ítem quitado del borrador se puede volver a agregar.
        builder.HasIndex(d => new { d.IdPedido, d.IdCatalogoProveedor })
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL")
            .HasDatabaseName("UX_DETALLE_PEDIDOS_pedido_catalogo");

        // Foto del catálogo al agregar el renglón.
        builder.Property(d => d.CodigoProveedor)
            .HasColumnName("codigo_proveedor")
            .HasMaxLength(Pedido.LongitudMaximaCodigoProveedor)
            .IsRequired();

        builder.Property(d => d.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(Pedido.LongitudMaximaDescripcion)
            .IsRequired();

        builder.Property(d => d.Cantidad)
            .HasColumnName("cantidad")
            .IsRequired();

        builder.Property(d => d.CostoPactado)
            .HasColumnName("costo_pactado")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(d => d.IsDeleted);
        builder.Ignore(d => d.SubtotalEstimado);

        builder.HasOne(d => d.Articulo)
            .WithMany()
            .HasForeignKey(d => d.IdArticulo)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CatalogoProveedor)
            .WithMany()
            .HasForeignKey(d => d.IdCatalogoProveedor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
