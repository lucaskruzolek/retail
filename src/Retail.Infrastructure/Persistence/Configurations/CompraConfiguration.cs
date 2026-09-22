using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class CompraConfiguration : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> builder)
    {
        builder.ToTable("COMPRAS", t =>
        {
            t.HasCheckConstraint("CK_COMPRAS_Totales", "[subtotal] >= 0 AND [total] >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id_compra");

        builder.Property(c => c.IdProveedor)
            .HasColumnName("id_proveedor")
            .IsRequired();

        builder.Property(c => c.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        builder.Property(c => c.TipoComprobante)
            .HasColumnName("tipo_comprobante")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.NumeroComprobante)
            .HasColumnName("numero_comprobante")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.FechaEmision)
            .HasColumnName("fecha_emision")
            .IsRequired();

        builder.Property(c => c.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(c => c.IsDeleted);

        builder.HasOne(c => c.Proveedor)
            .WithMany(p => p.Compras)
            .HasForeignKey(c => c.IdProveedor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Usuario)
            .WithMany(u => u.Compras)
            .HasForeignKey(c => c.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
