using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class TurnoCajaConfiguration : IEntityTypeConfiguration<TurnoCaja>
{
    public void Configure(EntityTypeBuilder<TurnoCaja> builder)
    {
        builder.ToTable("TURNOS_CAJA");

        builder.HasKey(tc => tc.Id);

        builder.Property(tc => tc.Id)
            .HasColumnName("id_turno");

        builder.Property(tc => tc.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        builder.Property(tc => tc.FechaApertura)
            .HasColumnName("fecha_apertura")
            .IsRequired();

        builder.Property(tc => tc.SaldoInicial)
            .HasColumnName("saldo_inicial")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tc => tc.FechaCierre)
            .HasColumnName("fecha_cierre");

        builder.Property(tc => tc.TotalVentasEfectivo)
            .HasColumnName("total_ventas_efectivo")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tc => tc.TotalIngresosEfectivo)
            .HasColumnName("total_ingresos_efectivo")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tc => tc.TotalEgresosEfectivo)
            .HasColumnName("total_egresos_efectivo")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tc => tc.SaldoTeoricoEfectivo)
            .HasColumnName("saldo_teorico_efectivo")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tc => tc.SaldoDeclaradoEfectivo)
            .HasColumnName("saldo_declarado_efectivo")
            .HasPrecision(18, 2);

        builder.Property(tc => tc.DiferenciaEfectivo)
            .HasColumnName("diferencia_efectivo")
            .HasPrecision(18, 2);

        builder.Property(tc => tc.TotalVentasElectronicas)
            .HasColumnName("total_ventas_electronicas")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tc => tc.MontoRetenidoEnCaja)
            .HasColumnName("monto_retenido_en_caja")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(tc => tc.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(tc => tc.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(tc => tc.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(tc => tc.IsDeleted);

        builder.HasOne(tc => tc.Usuario)
            .WithMany(u => u.TurnosCaja)
            .HasForeignKey(tc => tc.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
