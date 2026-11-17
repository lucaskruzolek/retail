using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class DetallePresupuestoConfiguration : IEntityTypeConfiguration<DetallePresupuesto>
{
    public void Configure(EntityTypeBuilder<DetallePresupuesto> builder)
    {
        builder.ToTable("DETALLE_PRESUPUESTOS", t =>
        {
            t.HasCheckConstraint("CK_DETALLE_PRESUPUESTOS_Valores", "[cantidad] > 0 AND [precio_unitario_pactado] >= 0 AND [subtotal_item] >= 0");
        });

        builder.HasKey(dp => dp.Id);

        builder.Property(dp => dp.Id)
            .HasColumnName("id_detalle_presupuesto");

        builder.Property(dp => dp.IdPresupuesto)
            .HasColumnName("id_presupuesto")
            .IsRequired();

        builder.Property(dp => dp.IdArticulo)
            .HasColumnName("id_articulo")
            .IsRequired();

        // Foto de la descripción del artículo al emitir (PS-11).
        builder.Property(dp => dp.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(DetallePresupuesto.LongitudMaximaDescripcion)
            .IsRequired();

        builder.Property(dp => dp.Cantidad)
            .HasColumnName("cantidad")
            .IsRequired();

        builder.Property(dp => dp.PrecioUnitarioPactado)
            .HasColumnName("precio_unitario_pactado")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(dp => dp.SubtotalItem)
            .HasColumnName("subtotal_item")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(dp => dp.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(dp => dp.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(dp => dp.IsDeleted);

        builder.HasOne(dp => dp.Presupuesto)
            .WithMany(p => p.Detalles)
            .HasForeignKey(dp => dp.IdPresupuesto)
            // Ley 3: no hay borrado físico. La cascada es solo la regla del motor: un presupuesto se anula (cambia de
            // estado) y sus renglones no se quitan, así que no se ejercita.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dp => dp.Articulo)
            .WithMany(a => a.DetallesPresupuestos)
            .HasForeignKey(dp => dp.IdArticulo)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
