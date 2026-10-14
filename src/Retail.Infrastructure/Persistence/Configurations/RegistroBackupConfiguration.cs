using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="RegistroBackup"/> a REGISTROS_BACKUP (RF-22).
/// </summary>
/// <remarks>
/// Sin clave foránea a USUARIOS: el operador es una foto (id + nombre) para que el historial pueda reinsertarse en
/// una base restaurada donde ese usuario quizás no exista (R-1).
/// </remarks>
public class RegistroBackupConfiguration : IEntityTypeConfiguration<RegistroBackup>
{
    public void Configure(EntityTypeBuilder<RegistroBackup> builder)
    {
        builder.ToTable("REGISTROS_BACKUP", tabla =>
        {
            // Defensa en profundidad de las invariantes del agregado.
            tabla.HasCheckConstraint("CK_REGISTROS_BACKUP_Tamano", "[tamano_bytes] IS NULL OR [tamano_bytes] > 0");
            tabla.HasCheckConstraint(
                "CK_REGISTROS_BACKUP_Operador",
                "([tipo] IN ('Manual', 'PreRestauracion') AND [id_usuario] IS NOT NULL AND [nombre_usuario] IS NOT NULL) " +
                "OR ([tipo] IN ('Automatico', 'PreMigracion') AND [id_usuario] IS NULL AND [nombre_usuario] IS NULL)");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id_registro_backup");

        builder.Property(r => r.Referencia)
            .HasColumnName("referencia")
            .IsRequired();

        // Identidad estable entre bases: es la clave para reinsertar el historial sin duplicarlo tras un restore.
        builder.HasIndex(r => r.Referencia)
            .IsUnique();

        builder.Property(r => r.Tipo)
            .HasColumnName("tipo")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Un único backup en curso a la vez (R-12): mismo patrón que IX_TURNOS_CAJA_TurnoAbierto.
        builder.HasIndex(r => r.Estado)
            .IsUnique()
            .HasFilter("[estado] = 'EnCurso' AND [deleted_at] IS NULL")
            .HasDatabaseName("IX_REGISTROS_BACKUP_EnCurso");

        builder.Property(r => r.IdUsuario)
            .HasColumnName("id_usuario");

        builder.Property(r => r.NombreUsuario)
            .HasColumnName("nombre_usuario")
            .HasMaxLength(Retail.Domain.Common.ReglasTexto.LongitudMaximaNombreUsuario);

        builder.Property(r => r.FechaInicio)
            .HasColumnName("fecha_inicio")
            .IsRequired();

        // El historial se lista del más reciente al más antiguo.
        builder.HasIndex(r => r.FechaInicio)
            .IsDescending();

        builder.Property(r => r.FechaFin)
            .HasColumnName("fecha_fin");

        builder.Property(r => r.RutaArchivo)
            .HasColumnName("ruta_archivo")
            .HasMaxLength(RegistroBackup.LongitudMaximaRuta)
            .IsRequired();

        builder.Property(r => r.TamanoBytes)
            .HasColumnName("tamano_bytes");

        builder.Property(r => r.HashSha256)
            .HasColumnName("hash_sha256")
            .HasMaxLength(RegistroBackup.LongitudHashSha256)
            .IsFixedLength()
            .IsUnicode(false);

        builder.Property(r => r.UltimaMigracion)
            .HasColumnName("ultima_migracion")
            .HasMaxLength(RegistroBackup.LongitudMaximaMigracion)
            .IsRequired();

        builder.Property(r => r.VersionApp)
            .HasColumnName("version_app")
            .HasMaxLength(RegistroBackup.LongitudMaximaVersion)
            .IsRequired();

        builder.Property(r => r.MensajeError)
            .HasColumnName("mensaje_error")
            .HasMaxLength(RegistroBackup.LongitudMaximaMensajeError);

        builder.Ignore(r => r.EsRestaurable);
        builder.Ignore(r => r.EsDelSistema);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(r => r.IsDeleted);
    }
}
