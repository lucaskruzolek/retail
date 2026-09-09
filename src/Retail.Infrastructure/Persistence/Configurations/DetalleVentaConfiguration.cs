using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class DetalleVentaConfiguration : IEntityTypeConfiguration<DetalleVenta>
{
    public void Configure(EntityTypeBuilder<DetalleVenta> builder)
    {
        builder.ToTable("DETALLE_VENTAS");

        builder.HasKey(dv => dv.Id);

        builder.Property(dv => dv.Id)
            .HasColumnName("id_detalle");

        builder.Property(dv => dv.IdVenta)
            .HasColumnName("id_venta")
            .IsRequired();

        builder.Property(dv => dv.IdArticulo)
            .HasColumnName("id_articulo")
            .IsRequired();

        builder.Property(dv => dv.Cantidad)
            .HasColumnName("cantidad")
            .IsRequired();

        builder.Property(dv => dv.PrecioUnitario)
            .HasColumnName("precio_unitario")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(dv => dv.SubtotalItem)
            .HasColumnName("subtotal_item")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(dv => dv.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(dv => dv.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(dv => dv.IsDeleted);

        builder.HasOne(dv => dv.Venta)
            .WithMany(v => v.Detalles)
            .HasForeignKey(dv => dv.IdVenta)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dv => dv.Articulo)
            .WithMany(a => a.DetallesVentas)
            .HasForeignKey(dv => dv.IdArticulo)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
