using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="RenglonLista"/> a RENGLONES_LISTA (RF-30). Entidad interna del agregado <see cref="ListaArticulos"/>.
/// </summary>
public class RenglonListaConfiguration : IEntityTypeConfiguration<RenglonLista>
{
    public void Configure(EntityTypeBuilder<RenglonLista> builder)
    {
        builder.ToTable("RENGLONES_LISTA", t =>
        {
            // Defensa en profundidad de la invariante del agregado (el dominio ya la exige).
            t.HasCheckConstraint("CK_RENGLONES_LISTA_Cantidad", "[cantidad] >= 1");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id_renglon_lista");

        builder.Property(r => r.IdListaArticulos)
            .HasColumnName("id_lista")
            .IsRequired();

        builder.Property(r => r.IdArticulo)
            .HasColumnName("id_articulo")
            .IsRequired();

        builder.Property(r => r.Cantidad)
            .HasColumnName("cantidad")
            .IsRequired();

        // Un artículo figura una sola vez por lista (agregarlo de nuevo suma la cantidad, en el dominio). El filtro
        // excluye los renglones quitados (baja lógica): el mismo artículo se puede volver a agregar.
        builder.HasIndex(r => new { r.IdListaArticulos, r.IdArticulo })
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL")
            .HasDatabaseName("UX_RENGLONES_LISTA_lista_articulo");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(r => r.IsDeleted);

        builder.HasOne(r => r.Articulo)
            .WithMany()
            .HasForeignKey(r => r.IdArticulo)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
