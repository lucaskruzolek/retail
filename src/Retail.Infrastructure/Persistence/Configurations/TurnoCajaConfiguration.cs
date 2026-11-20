using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class TurnoCajaConfiguration : IEntityTypeConfiguration<TurnoCaja>
{
    public void Configure(EntityTypeBuilder<TurnoCaja> builder)
    {
        builder.ToTable("TURNOS_CAJA", t =>
        {
            t.HasCheckConstraint("CK_TURNOS_CAJA_CantidadVentas", "[cantidad_ventas] >= 0");
            t.HasCheckConstraint("CK_TURNOS_CAJA_Saldos", "[saldo_inicial] >= 0 AND [total_ventas_efectivo] >= 0 AND [total_ingresos_efectivo] >= 0 AND [total_egresos_efectivo] >= 0 AND [total_ventas_electronicas] >= 0 AND [monto_retenido_en_caja] >= 0");
        });

        builder.HasKey(tc => tc.Id);

        builder.Property(tc => tc.Id)
            .HasColumnName("id_turno");

        builder.Property(tc => tc.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        builder.Property(tc => tc.IdTerminal)
            .HasColumnName("id_terminal")
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

        builder.Property(tc => tc.CantidadVentas)
            .HasColumnName("cantidad_ventas")
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

        // Control de concurrencia optimista (MT-11, H3): un encargado puede cerrar el turno desde otra terminal
        // mientras el cajero está cobrando una venta. SQL Server cambia el rowversion en cada UPDATE y EF Core lo
        // agrega al WHERE, así que el segundo guardado falla con DbUpdateConcurrencyException en lugar de imputar
        // la venta a un turno ya cerrado. Shadow property: el Dominio no necesita conocer este detalle.
        builder.Property<byte[]>("RowVersion")
            .HasColumnName("row_version")
            .IsRowVersion();

        builder.HasOne(tc => tc.Usuario)
            .WithMany(u => u.TurnosCaja)
            .HasForeignKey(tc => tc.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        // El turno pertenece a una terminal (MT-1). Restrict: una terminal con historial no se elimina; se da de baja
        // lógica (Ley 3).
        builder.HasOne(tc => tc.Terminal)
            .WithMany()
            .HasForeignKey(tc => tc.IdTerminal)
            .OnDelete(DeleteBehavior.Restrict);

        // Un turno abierto por terminal (MT-1) Y uno por cajero (MT-9). Son dos índices únicos filtrados distintos
        // porque cada uno protege una regla diferente, y son la garantía final cuando dos PC abren a la vez: las
        // comprobaciones previas de CajaService solo mejoran el mensaje. Un turno cerrado o dado de baja libera
        // la terminal y al cajero.
        builder.HasIndex(tc => tc.IdTerminal)
            .IsUnique()
            .HasFilter("[estado] = 'Abierto' AND [deleted_at] IS NULL")
            .HasDatabaseName("UX_TURNOS_CAJA_AbiertoPorTerminal");

        builder.HasIndex(tc => tc.IdUsuario)
            .IsUnique()
            .HasFilter("[estado] = 'Abierto' AND [deleted_at] IS NULL")
            .HasDatabaseName("UX_TURNOS_CAJA_AbiertoPorUsuario");
    }
}
