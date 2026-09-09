using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class MovimientoCajaConfiguration : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(EntityTypeBuilder<MovimientoCaja> builder)
    {
        builder.ToTable("MOVIMIENTOS_CAJA");

        builder.HasKey(mc => mc.Id);

        builder.Property(mc => mc.Id)
            .HasColumnName("id_movimiento");

        builder.Property(mc => mc.IdTurno)
            .HasColumnName("id_turno")
            .IsRequired();

        builder.Property(mc => mc.TipoMovimiento)
            .HasColumnName("tipo_movimiento")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(mc => mc.Monto)
            .HasColumnName("monto")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(mc => mc.Concepto)
            .HasColumnName("concepto")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(mc => mc.FechaHora)
            .HasColumnName("fecha_hora")
            .IsRequired();

        builder.Property(mc => mc.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(mc => mc.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(mc => mc.IsDeleted);

        builder.HasOne(mc => mc.TurnoCaja)
            .WithMany(tc => tc.Movimientos)
            .HasForeignKey(mc => mc.IdTurno)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
