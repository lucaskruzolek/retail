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
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        // Único entre las activas (D-4): una marca dada de baja libera su nombre.
        builder.HasIndex(m => m.NombreMarca)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(m => m.IsDeleted);

        // Diccionarios de catalogación (Track 6.6): entidades internas del agregado, que EF Core escribe a través de
        // las listas privadas (_alias, _prefijos) porque las colecciones públicas son de solo lectura. Cascade como
        // los detalles de una venta: nunca se ejecuta, porque el borrado es lógico (Ley 3).
        builder.HasMany(m => m.Alias)
            .WithOne(a => a.Marca)
            .HasForeignKey(a => a.IdMarca)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Alias)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Prefijos)
            .WithOne(p => p.Marca)
            .HasForeignKey(p => p.IdMarca)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Prefijos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
