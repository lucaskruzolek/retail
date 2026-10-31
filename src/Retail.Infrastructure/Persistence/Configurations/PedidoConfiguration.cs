using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="Pedido"/> a PEDIDOS (RF-28).
/// </summary>
public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("PEDIDOS");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id_pedido");

        builder.Property(p => p.IdProveedor)
            .HasColumnName("id_proveedor")
            .IsRequired();

        builder.Property(p => p.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        // El estado se guarda como texto legible, igual que en presupuestos y turnos de caja (F1): una fila
        // inspeccionada a mano dice 'Emitido' y no un número, y el índice filtrado de abajo se lee solo.
        builder.Property(p => p.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // A-7: un solo borrador vigente por proveedor. Es la última barrera si dos terminales crean el borrador a la
        // vez; el servicio la traduce en RestriccionDeUnicidadException. Un pedido dado de baja libera el lugar.
        builder.HasIndex(p => p.IdProveedor)
            .IsUnique()
            .HasFilter("[estado] = N'Borrador' AND [deleted_at] IS NULL")
            .HasDatabaseName("UX_PEDIDOS_borrador_por_proveedor");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.FechaEmision)
            .HasColumnName("fecha_emision");

        builder.Property(p => p.FechaCierre)
            .HasColumnName("fecha_cierre");

        builder.Property(p => p.MotivoCancelacion)
            .HasColumnName("motivo_cancelacion")
            .HasMaxLength(Pedido.LongitudMaximaMotivoCancelacion);

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        // Control de concurrencia optimista (A-13 / D1): SQL Server cambia el rowversion en cada UPDATE de la fila y
        // EF Core lo agrega al WHERE. Si dos terminales emiten, reciben o cancelan el mismo pedido, la segunda recibe
        // DbUpdateConcurrencyException en lugar de pisar el cambio. Es una shadow property: un detalle de
        // persistencia que el Dominio no necesita conocer (igual que en Articulo).
        builder.Property<byte[]>("RowVersion")
            .HasColumnName("row_version")
            .IsRowVersion();

        builder.Ignore(p => p.IsDeleted);
        builder.Ignore(p => p.TotalEstimado);

        builder.HasOne(p => p.Proveedor)
            .WithMany()
            .HasForeignKey(p => p.IdProveedor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Usuario)
            .WithMany()
            .HasForeignKey(p => p.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        // Los renglones viven dentro de la raíz: EF los puebla por el campo privado, no por la vista de solo lectura.
        builder.HasMany(p => p.Detalles)
            .WithOne(d => d.Pedido)
            .HasForeignKey(d => d.IdPedido)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Detalles)
            .HasField("_detalles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
