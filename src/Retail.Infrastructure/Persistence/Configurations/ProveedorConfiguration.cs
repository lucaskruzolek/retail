using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        builder.ToTable("PROVEEDORES");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id_proveedor");

        builder.Property(p => p.RazonSocial)
            .HasColumnName("razon_social")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.Cuit)
            .HasColumnName("cuit")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(p => p.Cuit)
            .IsUnique();

        builder.Property(p => p.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(50);

        builder.Property(p => p.Email)
            .HasColumnName("email")
            .HasMaxLength(100);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(p => p.IsDeleted);
    }
}
