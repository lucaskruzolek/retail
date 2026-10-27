using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class ParClasificacionConfiguration : IEntityTypeConfiguration<ParClasificacion>
{
    public void Configure(EntityTypeBuilder<ParClasificacion> builder)
    {
        // Forma física de la segunda palabra (Track 6.6, Fase 5): una sola palabra de al menos 2 caracteres. Que tenga
        // 2 letras y no sea de relleno es regla del Dominio (ReglasClasificacion), y que el término sea ambiguo
        // relaciona dos filas: ninguna de las dos se expresa en un CHECK (Ley 6 y Ley 7 de persistencia). La columna
        // es NOT NULL, así que la condición nunca da UNKNOWN.
        builder.ToTable("PARES_CLASIFICACION", t =>
        {
            t.HasCheckConstraint(
                "CK_PARES_CLASIFICACION_SegundaPalabra",
                "LEN([segunda_palabra]) >= 2 AND CHARINDEX(' ', [segunda_palabra]) = 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id_par");

        builder.Property(p => p.IdTermino)
            .HasColumnName("id_termino")
            .IsRequired();

        // Se guarda ya en su forma de comparación, como el término: la igualdad es exacta.
        builder.Property(p => p.SegundaPalabra)
            .HasColumnName("segunda_palabra")
            .HasMaxLength(ReglasClasificacion.LongitudMaximaTermino)
            .IsRequired();

        // Un par vigente por término y segunda palabra. Lo garantiza la raíz TerminoClasificacion; el índice es la
        // defensa en profundidad, y uno dado de baja libera el par.
        builder.HasIndex(p => new { p.IdTermino, p.SegundaPalabra })
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        // Nula = par ignorado (P-4). EsIgnorado se deriva de ella y no se persiste.
        builder.Property(p => p.IdCategoria)
            .HasColumnName("id_categoria")
            .IsRequired(false);

        builder.Ignore(p => p.EsIgnorado);

        // La categoría es otra raíz de agregado: se relaciona por Id, sin navegación en el Dominio (como D-13).
        builder.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(p => p.IdCategoria)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(p => p.IsDeleted);
    }
}
