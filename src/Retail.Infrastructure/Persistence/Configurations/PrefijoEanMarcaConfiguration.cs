using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class PrefijoEanMarcaConfiguration : IEntityTypeConfiguration<PrefijoEanMarca>
{
    public void Configure(EntityTypeBuilder<PrefijoEanMarca> builder)
    {
        // Forma canónica del prefijo (M-28), como CK_PROVEEDORES_Cuit: el largo y los dígitos son límites físicos
        // del dato; que no empiece con 2 también, porque esos códigos nunca se normalizan. Defensa en profundidad
        // (Ley 7 de persistencia): la regla vive en ReglasCodigoBarras.
        builder.ToTable("PREFIJOS_EAN_MARCAS", t =>
        {
            t.HasCheckConstraint(
                "CK_PREFIJOS_EAN_MARCAS_Prefijo",
                "LEN([prefijo]) = 7 AND [prefijo] NOT LIKE '%[^0-9]%' AND [prefijo] NOT LIKE '2%'");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id_prefijo");

        builder.Property(p => p.IdMarca)
            .HasColumnName("id_marca")
            .IsRequired();

        builder.Property(p => p.Prefijo)
            .HasColumnName("prefijo")
            .HasMaxLength(ReglasCodigoBarras.LongitudPrefijoTitular)
            .IsUnicode(false)
            .IsRequired();

        // Un prefijo pertenece a una sola marca (M-28): los repartidos entre varias se descartan al consolidar (M-25).
        builder.HasIndex(p => p.Prefijo)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(p => p.IsDeleted);
    }
}
