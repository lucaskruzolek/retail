using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;
using Retail.Domain.Enums;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="ConfiguracionComercial"/> a CONFIGURACION_COMERCIAL (D-38): una única fila, con id fijo
/// (<see cref="ConfiguracionComercial.IdUnico"/>, sin IDENTITY), que siembra la migración como dato de referencia del
/// esquema, igual que los roles.
/// </summary>
public class ConfiguracionComercialConfiguration : IEntityTypeConfiguration<ConfiguracionComercial>
{
    // Fecha fija de la fila sembrada: HasData necesita valores constantes para no generar una migración en cada cambio.
    private static readonly DateTime FechaSiembra = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ConfiguracionComercial> builder)
    {
        builder.ToTable("CONFIGURACION_COMERCIAL", tabla =>
        {
            // Defensa en profundidad: solo las políticas que conoce el Dominio (RedondeoPrecioEnum).
            tabla.HasCheckConstraint(
                "CK_CONFIGURACION_COMERCIAL_RedondeoPrecio",
                "[redondeo_precio] IN ('EnteroMasCercano', 'MultiploDe5Superior', 'MultiploDe5Inferior', " +
                "'MultiploDe10Superior', 'MultiploDe10Inferior', 'MultiploDe100Superior', 'MultiploDe100Inferior')");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id_configuracion_comercial")
            .ValueGeneratedNever();

        builder.Property(c => c.RedondeoPrecio)
            .HasColumnName("redondeo_precio")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasData(new
        {
            Id = ConfiguracionComercial.IdUnico,
            RedondeoPrecio = RedondeoPrecioEnum.EnteroMasCercano,
            CreatedAt = FechaSiembra,
            DeletedAt = (DateTime?)null
        });
    }
}
