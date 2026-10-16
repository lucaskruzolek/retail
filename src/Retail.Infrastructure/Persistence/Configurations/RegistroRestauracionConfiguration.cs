using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="RegistroRestauracion"/> a REGISTROS_RESTAURACION (RF-22, D-22).
/// </summary>
/// <remarks>
/// Sin claves foráneas, ni a USUARIOS ni a REGISTROS_BACKUP: el registro se rehidrata entre bases (se escribe en la
/// base restaurada y se reinserta en las siguientes), donde el Gerente o la copia referida pueden no existir. El
/// operador es una foto (id + nombre), igual que en <see cref="RegistroBackup"/>, y las referencias a copias son
/// <see cref="Guid"/> estables, no claves de tabla.
/// </remarks>
public class RegistroRestauracionConfiguration : IEntityTypeConfiguration<RegistroRestauracion>
{
    public void Configure(EntityTypeBuilder<RegistroRestauracion> builder)
    {
        builder.ToTable("REGISTROS_RESTAURACION", tabla =>
        {
            // Defensa en profundidad: toda restauración la autoriza un Gerente identificado (D-18).
            tabla.HasCheckConstraint("CK_REGISTROS_RESTAURACION_IdUsuario", "[id_usuario] > 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id_registro_restauracion");

        builder.Property(r => r.Referencia)
            .HasColumnName("referencia")
            .IsRequired();

        // Identidad estable entre bases: permite reinsertar el registro sin duplicarlo tras un restore.
        builder.HasIndex(r => r.Referencia)
            .IsUnique();

        builder.Property(r => r.Origen)
            .HasColumnName("origen")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        builder.Property(r => r.NombreUsuario)
            .HasColumnName("nombre_usuario")
            .HasMaxLength(ReglasTexto.LongitudMaximaNombreUsuario)
            .IsRequired();

        builder.Property(r => r.Fecha)
            .HasColumnName("fecha")
            .IsRequired();

        // Se lista del más reciente al más antiguo.
        builder.HasIndex(r => r.Fecha)
            .IsDescending();

        builder.Property(r => r.ReferenciaCopia)
            .HasColumnName("referencia_copia")
            .IsRequired();

        builder.Property(r => r.RutaArchivo)
            .HasColumnName("ruta_archivo")
            .HasMaxLength(RegistroBackup.LongitudMaximaRuta)
            .IsRequired();

        builder.Property(r => r.MigracionCopia)
            .HasColumnName("migracion_copia")
            .HasMaxLength(RegistroBackup.LongitudMaximaMigracion)
            .IsRequired();

        builder.Property(r => r.ReferenciaCopiaPrevia)
            .HasColumnName("referencia_copia_previa");

        builder.Property(r => r.BaseAnterior)
            .HasColumnName("base_anterior")
            .HasMaxLength(RegistroRestauracion.LongitudMaximaNombreBase);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(r => r.IsDeleted);
    }
}
