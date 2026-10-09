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

            // Presentación derivada (RF-21): origen y unidades van juntos; un derivado no tiene catálogo propio,
            // no es un servicio ni deriva de sí mismo. Las reglas que miran otra fila (un solo nivel) quedan en el Dominio.
            // El IS NOT NULL explícito es necesario: un CHECK solo rechaza FALSE, y NULL >= 1 da UNKNOWN.
            t.HasCheckConstraint(
                "CK_ARTICULOS_Presentacion",
                "([id_articulo_origen] IS NULL AND [unidades_por_origen] IS NULL) OR " +
                "([id_articulo_origen] IS NOT NULL AND [unidades_por_origen] IS NOT NULL AND [unidades_por_origen] >= 1 " +
                "AND [id_catalogo_proveedor] IS NULL AND [es_servicio] = 0 AND [id_articulo_origen] <> [id_articulo])");
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
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        builder.Property(a => a.IdCategoria)
            .HasColumnName("id_categoria")
            .IsRequired(false);

        builder.Property(a => a.IdMarca)
            .HasColumnName("id_marca")
            .IsRequired(false);

        builder.Property(a => a.IdCatalogoProveedor)
            .HasColumnName("id_catalogo_proveedor");

        // Filtered Unique Index: cada ítem del proveedor se vincula, como máximo, a un artículo de compra activo (RF-05).
        // Las presentaciones derivadas no tienen catálogo propio, así que la regla vale con fraccionamiento (RF-21).
        builder.HasIndex(a => a.IdCatalogoProveedor)
            .IsUnique()
            .HasFilter("[id_catalogo_proveedor] IS NOT NULL AND [deleted_at] IS NULL");

        builder.Property(a => a.IdArticuloOrigen)
            .HasColumnName("id_articulo_origen")
            .IsRequired(false);

        builder.Property(a => a.UnidadesPorOrigen)
            .HasColumnName("unidades_por_origen")
            .IsRequired(false);

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

        // Control de concurrencia optimista (D-12): SQL Server cambia el rowversion en cada UPDATE y EF Core lo
        // agrega al WHERE. Si otra terminal modificó el artículo (por ejemplo, vendió el último), el UPDATE no
        // afecta filas y SaveChanges lanza DbUpdateConcurrencyException en lugar de pisar el stock.
        // Es una shadow property: un detalle de persistencia que el Dominio no necesita conocer.
        builder.Property<byte[]>("RowVersion")
            .HasColumnName("row_version")
            .IsRowVersion();

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

        // Autorrelación origen → presentaciones (RF-21). Restrict: el borrado físico está prohibido y SQL Server
        // no admite cascadas en una FK autorreferencial.
        builder.HasOne(a => a.ArticuloOrigen)
            .WithMany(a => a.Presentaciones)
            .HasForeignKey(a => a.IdArticuloOrigen)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
