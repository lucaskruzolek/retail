using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("USUARIOS", tabla =>
        {
            // Defensa en profundidad: el contador de D-31 nunca es negativo.
            tabla.HasCheckConstraint(
                "CK_USUARIOS_IntentosAutorizacionFallidos",
                "[intentos_autorizacion_fallidos] >= 0");
        });

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id_usuario");

        builder.Property(u => u.NombreUsuario)
            .HasColumnName("nombre_usuario")
            .HasMaxLength(ReglasTexto.LongitudMaximaNombreUsuario)
            .IsRequired();

        builder.HasIndex(u => u.NombreUsuario)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(ReglasTexto.LongitudMaximaNombrePersona)
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        builder.Property(u => u.Apellido)
            .HasColumnName("apellido")
            .HasMaxLength(ReglasTexto.LongitudMaximaNombrePersona)
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        // Derivado de Nombre y Apellido: persistirlo duplicaría datos (3FN).
        builder.Ignore(u => u.NombreCompleto);

        // Sin índice único: el flujo de recuperación pide el nombre de usuario, así que dos cuentas pueden
        // compartir el mismo email (por ejemplo, el del local).
        builder.Property(u => u.EmailRecuperacion)
            .HasColumnName("email_recuperacion")
            .HasMaxLength(ReglasContacto.LongitudMaximaEmail);

        builder.Property(u => u.DebeCambiarPassword)
            .HasColumnName("debe_cambiar_password")
            .IsRequired();

        // Persistido en la base y no en la sesión: cerrar la aplicación no reinicia el contador (D-31).
        builder.Property(u => u.IntentosAutorizacionFallidos)
            .HasColumnName("intentos_autorizacion_fallidos")
            .IsRequired();

        // Derivada del contador: no tiene columna propia.
        builder.Ignore(u => u.EstaBloqueada);

        builder.Property(u => u.IdRol)
            .HasColumnName("id_rol")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(u => u.IsDeleted);

        builder.HasOne(u => u.Rol)
            .WithMany(r => r.Usuarios)
            .HasForeignKey(u => u.IdRol)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
