using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class VentaConfiguration : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> builder)
    {
        builder.ToTable("VENTAS");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id_venta");

        builder.Property(v => v.IdTurno)
            .HasColumnName("id_turno")
            .IsRequired();

        builder.Property(v => v.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        builder.Property(v => v.IdCliente)
            .HasColumnName("id_cliente")
            .IsRequired();

        builder.Property(v => v.IdPresupuestoOrigen)
            .HasColumnName("id_presupuesto_origen");

        builder.Property(v => v.FechaHora)
            .HasColumnName("fecha_hora")
            .IsRequired();

        builder.Property(v => v.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(v => v.Descuento)
            .HasColumnName("descuento")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(v => v.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(v => v.EstadoFiscal)
            .HasColumnName("estado_fiscal")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(v => v.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(v => v.IsDeleted);

        builder.HasOne(v => v.TurnoCaja)
            .WithMany(tc => tc.Ventas)
            .HasForeignKey(v => v.IdTurno)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Usuario)
            .WithMany(u => u.Ventas)
            .HasForeignKey(v => v.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Cliente)
            .WithMany(c => c.Ventas)
            .HasForeignKey(v => v.IdCliente)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.PresupuestoOrigen)
            .WithMany(p => p.VentasOriginadas)
            .HasForeignKey(v => v.IdPresupuestoOrigen)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
