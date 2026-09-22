using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class CatalogoProveedorConfiguration : IEntityTypeConfiguration<CatalogoProveedor>
{
    public void Configure(EntityTypeBuilder<CatalogoProveedor> builder)
    {
        builder.ToTable("CATALOGOS_PROVEEDORES", t =>
        {
            t.HasCheckConstraint("CK_CATALOGOS_PROVEEDORES_PrecioCosto", "[precio_costo] >= 0");
        });

        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.Id)
            .HasColumnName("id_catalogo");

        builder.Property(cp => cp.IdProveedor)
            .HasColumnName("id_proveedor")
            .IsRequired();

        builder.Property(cp => cp.CodigoProveedor)
            .HasColumnName("codigo_proveedor")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(cp => cp.CodigoBarras)
            .HasColumnName("codigo_barras")
            .HasMaxLength(50);

        builder.HasIndex(cp => new { cp.IdProveedor, cp.CodigoProveedor })
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(cp => cp.DescripcionProveedor)
            .HasColumnName("descripcion_proveedor")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(cp => cp.CostoReposicion)
            .HasColumnName("precio_costo")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(cp => cp.FechaActualizacion)
            .HasColumnName("fecha_actualizacion")
            .IsRequired();

        builder.Property(cp => cp.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(cp => cp.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(cp => cp.IsDeleted);

        builder.HasOne(cp => cp.Proveedor)
            .WithMany(p => p.Catalogos)
            .HasForeignKey(cp => cp.IdProveedor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
