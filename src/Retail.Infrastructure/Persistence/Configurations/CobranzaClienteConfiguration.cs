using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class CobranzaClienteConfiguration : IEntityTypeConfiguration<CobranzaCliente>
{
    public void Configure(EntityTypeBuilder<CobranzaCliente> builder)
    {
        builder.ToTable("COBRANZAS_CLIENTES");

        builder.HasKey(cc => cc.Id);

        builder.Property(cc => cc.Id)
            .HasColumnName("id_cobranza");

        builder.Property(cc => cc.IdCliente)
            .HasColumnName("id_cliente")
            .IsRequired();

        builder.Property(cc => cc.IdTurno)
            .HasColumnName("id_turno")
            .IsRequired();

        builder.Property(cc => cc.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        builder.Property(cc => cc.FechaHora)
            .HasColumnName("fecha_hora")
            .IsRequired();

        builder.Property(cc => cc.MedioPago)
            .HasColumnName("medio_pago")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(cc => cc.Monto)
            .HasColumnName("monto")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(cc => cc.Referencia)
            .HasColumnName("referencia")
            .HasMaxLength(100);

        builder.Property(cc => cc.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(cc => cc.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(cc => cc.IsDeleted);

        builder.HasOne(cc => cc.Cliente)
            .WithMany(c => c.Cobranzas)
            .HasForeignKey(cc => cc.IdCliente)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cc => cc.TurnoCaja)
            .WithMany(tc => tc.Cobranzas)
            .HasForeignKey(cc => cc.IdTurno)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cc => cc.Usuario)
            .WithMany(u => u.CobranzasClientes)
            .HasForeignKey(cc => cc.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
