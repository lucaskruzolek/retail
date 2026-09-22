using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class ComprobanteFiscalConfiguration : IEntityTypeConfiguration<ComprobanteFiscal>
{
    public void Configure(EntityTypeBuilder<ComprobanteFiscal> builder)
    {
        builder.ToTable("COMPROBANTES_FISCALES", t =>
        {
            t.HasCheckConstraint("CK_COMPROBANTES_FISCALES_Numeracion", "[punto_venta] > 0 AND [numero_comprobante] >= 0");
        });

        builder.HasKey(cf => cf.Id);

        builder.Property(cf => cf.Id)
            .HasColumnName("id_comprobante");

        builder.Property(cf => cf.IdVenta)
            .HasColumnName("id_venta")
            .IsRequired();

        builder.Property(cf => cf.TipoComprobante)
            .HasColumnName("tipo_comprobante")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(cf => cf.PuntoVenta)
            .HasColumnName("punto_venta")
            .IsRequired();

        builder.Property(cf => cf.NumeroComprobante)
            .HasColumnName("numero_comprobante")
            .IsRequired();

        builder.Property(cf => cf.Cae)
            .HasColumnName("cae")
            .HasMaxLength(20);

        builder.Property(cf => cf.FechaVtoCae)
            .HasColumnName("fecha_vto_cae");

        builder.Property(cf => cf.ResultadoArca)
            .HasColumnName("resultado_arca")
            .HasMaxLength(50);

        builder.Property(cf => cf.MotivoError)
            .HasColumnName("motivo_error")
            .HasMaxLength(500);

        builder.Property(cf => cf.FechaEmision)
            .HasColumnName("fecha_emision")
            .IsRequired();

        builder.Property(cf => cf.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(cf => cf.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(cf => cf.IsDeleted);

        builder.HasOne(cf => cf.Venta)
            .WithOne(v => v.ComprobanteFiscal)
            .HasForeignKey<ComprobanteFiscal>(cf => cf.IdVenta)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
