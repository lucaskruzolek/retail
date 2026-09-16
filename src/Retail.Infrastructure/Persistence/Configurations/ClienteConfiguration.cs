using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("CLIENTES");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id_cliente");

        builder.Property(c => c.RazonSocialONombre)
            .HasColumnName("razon_social_o_nombre")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.TipoDocumento)
            .HasColumnName("tipo_documento")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.NumeroDocumento)
            .HasColumnName("numero_documento")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(c => c.NumeroDocumento)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(c => c.CondicionIva)
            .HasColumnName("condicion_iva")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.DomicilioFiscal)
            .HasColumnName("domicilio_fiscal")
            .HasMaxLength(200);

        builder.Property(c => c.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(50);

        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasMaxLength(100);

        builder.Property(c => c.TieneCuentaCorriente)
            .HasColumnName("tiene_cuenta_corriente")
            .IsRequired();

        builder.Property(c => c.LimiteCredito)
            .HasColumnName("limite_credito")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.SaldoCuentaCorriente)
            .HasColumnName("saldo_cuenta_corriente")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(c => c.IsDeleted);
    }
}
