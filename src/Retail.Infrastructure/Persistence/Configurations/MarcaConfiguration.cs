using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class MarcaConfiguration : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> builder)
    {
        builder.ToTable("MARCAS");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id_marca");

        builder.Property(m => m.NombreMarca)
            .HasColumnName("nombre_marca")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(m => m.IsDeleted);
    }
}
