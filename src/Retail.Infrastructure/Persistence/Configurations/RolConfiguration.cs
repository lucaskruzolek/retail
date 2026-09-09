using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("ROLES");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id_rol");

        builder.Property(r => r.NombreRol)
            .HasColumnName("nombre_rol")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(r => r.IsDeleted);
    }
}
