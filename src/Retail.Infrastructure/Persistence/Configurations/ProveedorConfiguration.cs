using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> builder)
    {
        // Defensa en profundidad (Ley 7 de persistencia): aunque el agregado ya normaliza el CUIT, la base rechaza
        // por su cuenta cualquier valor que no sean 11 dígitos, por ejemplo un INSERT manual con guiones. El
        // dígito verificador no se controla acá: sería lógica de negocio en la base, prohibida por la Ley 6.
        builder.ToTable("PROVEEDORES", t =>
        {
            t.HasCheckConstraint("CK_PROVEEDORES_Cuit", "LEN([cuit]) = 11 AND [cuit] NOT LIKE '%[^0-9]%'");
        });

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
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

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
