using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class CompraConfiguration : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> builder)
    {
        builder.ToTable("COMPRAS", t =>
        {
            t.HasCheckConstraint("CK_COMPRAS_Totales", "[subtotal] >= 0 AND [total] >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id_compra");

        builder.Property(c => c.IdProveedor)
            .HasColumnName("id_proveedor")
            .IsRequired();

        builder.Property(c => c.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        builder.Property(c => c.TipoComprobante)
            .HasColumnName("tipo_comprobante")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.NumeroComprobante)
            .HasColumnName("numero_comprobante")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.FechaEmision)
            .HasColumnName("fecha_emision")
            .IsRequired();

        builder.Property(c => c.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(c => c.IsDeleted);

        // La colección de renglones vive en el backing field privado _detalles del agregado.
        builder.Navigation(c => c.Detalles)
            .HasField("_detalles");

        builder.HasOne(c => c.Proveedor)
            .WithMany(p => p.Compras)
            .HasForeignKey(c => c.IdProveedor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Usuario)
            .WithMany(u => u.Compras)
            .HasForeignKey(c => c.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        // C-1: Un mismo comprobante (proveedor + tipo + número) no puede registrarse dos veces (defensa en profundidad).
        // El índice es filtrado: solo aplica a registros no eliminados lógicamente.
        builder.HasIndex(c => new { c.IdProveedor, c.TipoComprobante, c.NumeroComprobante })
            .HasFilter("[deleted_at] IS NULL")
            .IsUnique()
            .HasDatabaseName("UX_COMPRAS_Comprobante");

        // P2 / §5.3: FK nullable hacia PEDIDOS. La columna se mapea explícitamente.
        builder.Property(c => c.IdPedidoOrigen)
            .HasColumnName("id_pedido_origen");

        builder.HasOne(c => c.PedidoOrigen)
            .WithMany()
            .HasForeignKey(c => c.IdPedidoOrigen)
            .OnDelete(DeleteBehavior.Restrict);

        // A-11 / §5.3: defensa en profundidad — un pedido no puede cerrarse con dos compras distintas.
        // El índice filtrado excluye NULLs (compras sin pedido) y registros borrados lógicamente.
        builder.HasIndex(c => c.IdPedidoOrigen)
            .HasFilter("[id_pedido_origen] IS NOT NULL AND [deleted_at] IS NULL")
            .IsUnique()
            .HasDatabaseName("UX_COMPRAS_PedidoOrigen");
    }
}
