using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class PagoVentaConfiguration : IEntityTypeConfiguration<PagoVenta>
{
    public void Configure(EntityTypeBuilder<PagoVenta> builder)
    {
        builder.ToTable("PAGOS_VENTA", t =>
        {
            t.HasCheckConstraint("CK_PAGOS_VENTA_Monto", "[monto] > 0");
        });

        builder.HasKey(pv => pv.Id);

        builder.Property(pv => pv.Id)
            .HasColumnName("id_pago");

        builder.Property(pv => pv.IdVenta)
            .HasColumnName("id_venta")
            .IsRequired();

        builder.Property(pv => pv.MedioPago)
            .HasColumnName("medio_pago")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(pv => pv.Monto)
            .HasColumnName("monto")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(pv => pv.ReferenciaPago)
            .HasColumnName("referencia_pago")
            .HasMaxLength(100);

        builder.Property(pv => pv.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(pv => pv.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(pv => pv.IsDeleted);

        builder.HasOne(pv => pv.Venta)
            .WithMany(v => v.Pagos)
            .HasForeignKey(pv => pv.IdVenta)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
