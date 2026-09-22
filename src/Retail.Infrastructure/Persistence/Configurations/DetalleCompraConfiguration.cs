using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class DetalleCompraConfiguration : IEntityTypeConfiguration<DetalleCompra>
{
    public void Configure(EntityTypeBuilder<DetalleCompra> builder)
    {
        builder.ToTable("DETALLE_COMPRAS", t =>
        {
            t.HasCheckConstraint("CK_DETALLE_COMPRAS_Valores", "[cantidad] > 0 AND [costo_unitario] >= 0 AND [subtotal_item] >= 0");
        });

        builder.HasKey(dc => dc.Id);

        builder.Property(dc => dc.Id)
            .HasColumnName("id_detalle_compra");

        builder.Property(dc => dc.IdCompra)
            .HasColumnName("id_compra")
            .IsRequired();

        builder.Property(dc => dc.IdArticulo)
            .HasColumnName("id_articulo")
            .IsRequired();

        builder.Property(dc => dc.Cantidad)
            .HasColumnName("cantidad")
            .IsRequired();

        builder.Property(dc => dc.CostoUnitario)
            .HasColumnName("costo_unitario")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(dc => dc.SubtotalItem)
            .HasColumnName("subtotal_item")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(dc => dc.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(dc => dc.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(dc => dc.IsDeleted);

        builder.HasOne(dc => dc.Compra)
            .WithMany(c => c.Detalles)
            .HasForeignKey(dc => dc.IdCompra)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dc => dc.Articulo)
            .WithMany(a => a.DetallesCompras)
            .HasForeignKey(dc => dc.IdArticulo)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
