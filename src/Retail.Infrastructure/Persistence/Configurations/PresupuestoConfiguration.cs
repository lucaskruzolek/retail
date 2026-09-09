using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class PresupuestoConfiguration : IEntityTypeConfiguration<Presupuesto>
{
    public void Configure(EntityTypeBuilder<Presupuesto> builder)
    {
        builder.ToTable("PRESUPUESTOS");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id_presupuesto");

        builder.Property(p => p.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        builder.Property(p => p.IdCliente)
            .HasColumnName("id_cliente")
            .IsRequired();

        builder.Property(p => p.FechaEmision)
            .HasColumnName("fecha_emision")
            .IsRequired();

        builder.Property(p => p.FechaVencimiento)
            .HasColumnName("fecha_vencimiento")
            .IsRequired();

        builder.Property(p => p.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Descuento)
            .HasColumnName("descuento")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(p => p.IsDeleted);

        builder.HasOne(p => p.Usuario)
            .WithMany(u => u.Presupuestos)
            .HasForeignKey(p => p.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Cliente)
            .WithMany(c => c.Presupuestos)
            .HasForeignKey(p => p.IdCliente)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
