using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("CATEGORIAS");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id_categoria");

        builder.Property(c => c.NombreCategoria)
            .HasColumnName("nombre_categoria")
            .HasMaxLength(100)
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        // Único entre las activas (D-4): una categoría dada de baja libera su nombre.
        builder.HasIndex(c => c.NombreCategoria)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(c => c.IdRubro)
            .HasColumnName("id_rubro")
            .IsRequired();

        // El rubro es otra raíz de agregado: se relaciona por Id, sin navegación en el Dominio (D-13). Restrict
        // porque los rubros solo se dan de baja de forma lógica y nunca con categorías activas.
        builder.HasOne<Rubro>()
            .WithMany()
            .HasForeignKey(c => c.IdRubro)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(c => c.IsDeleted);
    }
}
