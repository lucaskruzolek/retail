using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class TerminoClasificacionConfiguration : IEntityTypeConfiguration<TerminoClasificacion>
{
    public void Configure(EntityTypeBuilder<TerminoClasificacion> builder)
    {
        builder.ToTable("TERMINOS_CLASIFICACION");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id_termino");

        // Se guarda ya en su forma de comparación (mayúsculas, sin tildes; ReglasClasificacion), así que no necesita
        // una collation insensible a acentos: la igualdad es exacta.
        builder.Property(t => t.Termino)
            .HasColumnName("termino")
            .HasMaxLength(ReglasClasificacion.LongitudMaximaTermino)
            .IsRequired();

        // Un término sugiere una sola categoría en todo el catálogo (M-27); uno dado de baja libera el término.
        builder.HasIndex(t => t.Termino)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        // Nula = término ambiguo. EsAmbiguo se deriva de ella y no se persiste.
        builder.Property(t => t.IdCategoria)
            .HasColumnName("id_categoria")
            .IsRequired(false);

        builder.Ignore(t => t.EsAmbiguo);

        // La categoría es otra raíz de agregado: se relaciona por Id, sin navegación en el Dominio (como D-13).
        builder.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(t => t.IdCategoria)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(t => t.IsDeleted);
    }
}
