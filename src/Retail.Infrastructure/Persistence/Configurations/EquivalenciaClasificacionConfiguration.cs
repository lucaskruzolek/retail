using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class EquivalenciaClasificacionConfiguration : IEntityTypeConfiguration<EquivalenciaClasificacion>
{
    public void Configure(EntityTypeBuilder<EquivalenciaClasificacion> builder)
    {
        // El destino corresponde al tipo (Track 6.6, M-31): una equivalencia de categoría nunca apunta a una marca, y
        // una de marca no tiene rubro ni categoría. Sin destino es "ignorada" (M-32). Los IS NULL son explícitos y el
        // tipo es NOT NULL, así que ninguna rama queda en UNKNOWN (Ley 7 de persistencia).
        builder.ToTable("EQUIVALENCIAS_CLASIFICACION", t =>
        {
            t.HasCheckConstraint(
                "CK_EQUIVALENCIAS_CLASIFICACION_Destino",
                "([tipo] = 'Categoria' AND [id_marca] IS NULL) OR " +
                "([tipo] = 'Marca' AND [id_categoria] IS NULL AND [texto_rubro] IS NULL)");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id_equivalencia");

        builder.Property(e => e.IdProveedor)
            .HasColumnName("id_proveedor")
            .IsRequired();

        builder.Property(e => e.Tipo)
            .HasColumnName("tipo")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Forma de comparación (ReglasClasificacion.ExigirTextoDeProveedor): la igualdad es exacta.
        builder.Property(e => e.TextoRubro)
            .HasColumnName("texto_rubro")
            .HasMaxLength(ReglasClasificacion.LongitudMaximaTextoDeProveedor)
            .IsRequired(false);

        builder.Property(e => e.Texto)
            .HasColumnName("texto")
            .HasMaxLength(ReglasClasificacion.LongitudMaximaTextoDeProveedor)
            .IsRequired();

        // Una equivalencia vigente por valor de cada proveedor. Lo garantiza la raíz Proveedor; el índice es la
        // defensa en profundidad. SQL Server considera iguales dos NULL en un índice único, así que dos equivalencias
        // sin rubro con el mismo texto también chocan, que es lo buscado.
        builder.HasIndex(e => new { e.IdProveedor, e.Tipo, e.TextoRubro, e.Texto })
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(e => e.IdCategoria)
            .HasColumnName("id_categoria")
            .IsRequired(false);

        builder.Property(e => e.IdMarca)
            .HasColumnName("id_marca")
            .IsRequired(false);

        builder.Ignore(e => e.EsIgnorada);

        // Categoría y marca son otras raíces de agregado: se relacionan por Id, sin navegación en el Dominio.
        builder.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(e => e.IdCategoria)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Marca>()
            .WithMany()
            .HasForeignKey(e => e.IdMarca)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(e => e.IsDeleted);
    }
}
