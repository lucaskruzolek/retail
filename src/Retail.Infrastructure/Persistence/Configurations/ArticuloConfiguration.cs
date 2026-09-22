using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class ArticuloConfiguration : IEntityTypeConfiguration<Articulo>
{
    public void Configure(EntityTypeBuilder<Articulo> builder)
    {
        builder.ToTable("ARTICULOS", t =>
        {
            t.HasCheckConstraint("CK_ARTICULOS_Precios", "[precio_venta] >= 0 AND [costo_reposicion] >= 0 AND [porcentaje_ganancia] >= 0");
            t.HasCheckConstraint("CK_ARTICULOS_StockMinimo", "[stock_minimo] >= 0");
            t.HasCheckConstraint("CK_ARTICULOS_StockActual", "([stock_actual] >= 0) OR ([es_servicio] = 1)");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id_articulo");

        builder.Property(a => a.CodigoBarras)
            .HasColumnName("codigo_barras")
            .HasMaxLength(50)
            .IsRequired(false);

        // Filtered Unique Index: permite múltiples valores NULL para productos artesanales (RF-04) y reutilización ante soft-delete (RF-06)
        builder.HasIndex(a => a.CodigoBarras)
            .IsUnique()
            .HasFilter("[codigo_barras] IS NOT NULL AND [deleted_at] IS NULL");

        builder.Property(a => a.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.IdCategoria)
            .HasColumnName("id_categoria")
            .IsRequired(false);

        builder.Property(a => a.IdMarca)
            .HasColumnName("id_marca")
            .IsRequired(false);

        builder.Property(a => a.IdCatalogoProveedor)
            .HasColumnName("id_catalogo_proveedor");

        builder.Property(a => a.CostoReposicion)
            .HasColumnName("costo_reposicion")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.PorcentajeGanancia)
            .HasColumnName("porcentaje_ganancia")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.PrecioVenta)
            .HasColumnName("precio_venta")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.StockActual)
            .HasColumnName("stock_actual")
            .IsRequired();

        builder.Property(a => a.StockMinimo)
            .HasColumnName("stock_minimo")
            .IsRequired();

        builder.Property(a => a.EsServicio)
            .HasColumnName("es_servicio")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(a => a.IsDeleted);

        builder.HasOne(a => a.Categoria)
            .WithMany(c => c.Articulos)
            .HasForeignKey(a => a.IdCategoria)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Marca)
            .WithMany(m => m.Articulos)
            .HasForeignKey(a => a.IdMarca)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CatalogoProveedor)
            .WithMany(cp => cp.Articulos)
            .HasForeignKey(a => a.IdCatalogoProveedor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
