using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="Terminal"/> a TERMINALES (MT-1): los puestos de trabajo del local.
/// </summary>
public class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.ToTable("TERMINALES");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id_terminal");

        // Collation insensible a acentos y mayúsculas: "Mostrador 1" y "MOSTRADOR 1" son la misma terminal.
        builder.Property(t => t.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(Terminal.LongitudMaximaNombre)
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        // Dos terminales activas no pueden llamarse igual; una dada de baja libera el nombre.
        builder.HasIndex(t => t.Nombre)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL")
            .HasDatabaseName("UX_TERMINALES_nombre");

        // Nombre de Windows de la PC: solo sirve de control (no es único: un equipo reemplazado puede reutilizarse).
        builder.Property(t => t.NombreEquipo)
            .HasColumnName("nombre_equipo")
            .HasMaxLength(Terminal.LongitudMaximaNombreEquipo)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(t => t.IsDeleted);
    }
}
