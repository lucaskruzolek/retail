using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="ListaArticulos"/> a LISTAS_ARTICULOS (RF-30).
/// </summary>
public class ListaArticulosConfiguration : IEntityTypeConfiguration<ListaArticulos>
{
    public void Configure(EntityTypeBuilder<ListaArticulos> builder)
    {
        builder.ToTable("LISTAS_ARTICULOS");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id_lista");

        // Collation insensible a acentos y mayúsculas (igual que la descripción del artículo): el índice único de abajo
        // compara con ella, así "Lista Música" y "LISTA MUSICA" son el mismo nombre (L-10).
        builder.Property(l => l.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(ListaArticulos.LongitudMaximaNombre)
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        // L-10: dos listas activas no pueden llamarse igual. Una lista dada de baja libera el nombre.
        builder.HasIndex(l => l.Nombre)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL")
            .HasDatabaseName("UX_LISTAS_ARTICULOS_nombre");

        builder.Property(l => l.IdUsuarioCreador)
            .HasColumnName("id_usuario_creador")
            .IsRequired();

        // Precisión completa de datetime2: ListaService la compara por igualdad exacta en el control optimista.
        builder.Property(l => l.FechaModificacion)
            .HasColumnName("fecha_modificacion")
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(l => l.DeletedAt)
            .HasColumnName("deleted_at");

        // Concurrencia optimista: si dos terminales editan la misma lista, la segunda recibe
        // DbUpdateConcurrencyException en lugar de pisar el cambio. Propiedad sombra, igual que en Pedido.
        builder.Property<byte[]>("RowVersion")
            .HasColumnName("row_version")
            .IsRowVersion();

        builder.Ignore(l => l.IsDeleted);

        builder.HasOne(l => l.UsuarioCreador)
            .WithMany()
            .HasForeignKey(l => l.IdUsuarioCreador)
            .OnDelete(DeleteBehavior.Restrict);

        // Los renglones viven dentro de la raíz: EF los puebla por el campo privado, no por la vista de solo lectura.
        // La relación es requerida: al quitar un renglón de la colección EF lo marca Deleted y RetailDbContext lo
        // convierte en baja lógica (Ley 3).
        builder.HasMany(l => l.Renglones)
            .WithOne(r => r.ListaArticulos)
            .HasForeignKey(r => r.IdListaArticulos)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(l => l.Renglones)
            .HasField("_renglones")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
