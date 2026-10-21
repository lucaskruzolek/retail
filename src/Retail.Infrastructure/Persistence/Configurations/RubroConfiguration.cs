using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class RubroConfiguration : IEntityTypeConfiguration<Rubro>
{
    public void Configure(EntityTypeBuilder<Rubro> builder)
    {
        builder.ToTable("RUBROS");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id_rubro");

        // Colación insensible a mayúsculas y acentos (D-22, R-3): "Libreria" y "Librería" son el mismo rubro.
        builder.Property(r => r.NombreRubro)
            .HasColumnName("nombre_rubro")
            .HasMaxLength(100)
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        // Único entre los activos (D-4): un rubro dado de baja libera su nombre.
        builder.HasIndex(r => r.NombreRubro)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(r => r.IsDeleted);
    }
}
