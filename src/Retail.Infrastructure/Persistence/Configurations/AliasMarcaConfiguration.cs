using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class AliasMarcaConfiguration : IEntityTypeConfiguration<AliasMarca>
{
    public void Configure(EntityTypeBuilder<AliasMarca> builder)
    {
        builder.ToTable("ALIAS_MARCAS");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id_alias");

        builder.Property(a => a.IdMarca)
            .HasColumnName("id_marca")
            .IsRequired();

        // Forma de comparación (ReglasClasificacion): la igualdad es exacta, sin collation propia.
        builder.Property(a => a.Alias)
            .HasColumnName("alias")
            .HasMaxLength(ReglasClasificacion.LongitudMaximaAlias)
            .IsRequired();

        // Un alias pertenece a una sola marca (sección 8.1 del Track 6.6): lo verifica Aplicación y lo garantiza
        // este índice, que cruza agregados. Uno dado de baja libera el alias.
        builder.HasIndex(a => a.Alias)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(a => a.IsDeleted);
    }
}
