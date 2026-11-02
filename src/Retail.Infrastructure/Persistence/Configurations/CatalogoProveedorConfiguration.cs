using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Common;
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
            .UseCollation(Cotejamientos.BusquedaEnCastellano)
            .IsRequired();

        // Índice de paginación: entrega las filas del proveedor ya ordenadas por descripción, de modo que la página del
        // catálogo no ordena en memoria (sin Sort ni memory grant). Respeta el borrado lógico (Ley 8, RNF-02).
        builder.HasIndex(cp => new { cp.IdProveedor, cp.DescripcionProveedor })
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(cp => cp.CostoReposicion)
            .HasColumnName("precio_costo")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(cp => cp.FechaActualizacion)
            .HasColumnName("fecha_actualizacion")
            .IsRequired();

        // Clasificación del proveedor tal como la escribe (Track 6.6, M-12): texto para mostrar, no clave foránea.
        builder.Property(cp => cp.RubroProveedor)
            .HasColumnName("rubro_proveedor")
            .HasMaxLength(ReglasClasificacion.LongitudMaximaTextoDeProveedor);

        builder.Property(cp => cp.CategoriaProveedor)
            .HasColumnName("categoria_proveedor")
            .HasMaxLength(ReglasClasificacion.LongitudMaximaTextoDeProveedor);

        builder.Property(cp => cp.MarcaProveedor)
            .HasColumnName("marca_proveedor")
            .HasMaxLength(ReglasClasificacion.LongitudMaximaTextoDeProveedor);

        // Clasificación sugerida con las reglas del comercio (Track 6.6, M-72, M-74). Las FK son opcionales y sin
        // navegación: el dominio solo guarda ids y la consulta del explorador hace el cruce. Restrict porque el borrado
        // físico está prohibido (Ley 3); una categoría o marca dada de baja se lee como "sin categoría" (M-74).
        builder.Property(cp => cp.IdCategoriaSugerida)
            .HasColumnName("id_categoria_sugerida");

        builder.Property(cp => cp.IdMarcaSugerida)
            .HasColumnName("id_marca_sugerida");

        // Nula = pendiente de clasificar (M-74).
        builder.Property(cp => cp.FechaClasificacion)
            .HasColumnName("fecha_clasificacion");

        builder.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(cp => cp.IdCategoriaSugerida)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Marca>()
            .WithMany()
            .HasForeignKey(cp => cp.IdMarcaSugerida)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Índices de los filtros del explorador por categoría y por marca sugeridas: el proveedor primero, como el de
        // paginación, y solo las filas vigentes (Ley 8, RNF-02).
        builder.HasIndex(cp => new { cp.IdProveedor, cp.IdCategoriaSugerida })
            .HasFilter("[deleted_at] IS NULL");

        builder.HasIndex(cp => new { cp.IdProveedor, cp.IdMarcaSugerida })
            .HasFilter("[deleted_at] IS NULL");

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

        // Fuente del artículo, principal o adicional (Track 4.3, A-1 y A-16). ARTICULOS.id_catalogo_proveedor guarda
        // solo la principal; que esta sea una de estas fuentes lo garantiza el dominio (J1). Se declara explícita
        // porque hay dos relaciones entre las mismas tablas, y Restrict porque el borrado físico está prohibido.
        builder.Property(cp => cp.IdArticulo)
            .HasColumnName("id_articulo");

        builder.HasOne(cp => cp.Articulo)
            .WithMany()
            .HasForeignKey(cp => cp.IdArticulo)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
