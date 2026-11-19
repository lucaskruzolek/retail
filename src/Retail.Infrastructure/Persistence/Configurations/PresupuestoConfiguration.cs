using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="Presupuesto"/> a PRESUPUESTOS (RF-11, RF-12).
/// </summary>
public class PresupuestoConfiguration : IEntityTypeConfiguration<Presupuesto>
{
    public void Configure(EntityTypeBuilder<Presupuesto> builder)
    {
        builder.ToTable("PRESUPUESTOS", t =>
        {
            t.HasCheckConstraint("CK_PRESUPUESTOS_Totales", "[subtotal] >= 0 AND [descuento] >= 0 AND [total] >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id_presupuesto");

        builder.Property(p => p.IdUsuario)
            .HasColumnName("id_usuario")
            .IsRequired();

        // Nullable: NULL es Consumidor Final, igual que en VENTAS.
        builder.Property(p => p.IdCliente)
            .HasColumnName("id_cliente");

        // Nota interna (PS-20). La collation insensible a acentos permite buscarla en el listado ("lapiz" encuentra
        // "Lápiz"), igual que las demás columnas de texto que el usuario busca.
        builder.Property(p => p.Titulo)
            .HasColumnName("titulo")
            .HasMaxLength(Presupuesto.LongitudMaximaTitulo)
            .UseCollation(Cotejamientos.BusquedaEnCastellano);

        builder.Property(p => p.FechaEmision)
            .HasColumnName("fecha_emision")
            .IsRequired();

        // Fecha calendario local, sin hora (PS-09): "válido hasta el 02/12" es todo ese día, sin depender de la zona horaria.
        builder.Property(p => p.FechaVencimiento)
            .HasColumnName("fecha_vencimiento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(p => p.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Descuento)
            .HasColumnName("descuento")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.FechaCierre)
            .HasColumnName("fecha_cierre");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        // Control de concurrencia optimista (PS-13): si una terminal anula o convierte un presupuesto que otra ya
        // modificó, EF Core recibe 0 filas afectadas y lanza DbUpdateConcurrencyException en lugar de pisar el cambio.
        // Es una shadow property, igual que en Pedido y Articulo: un detalle de persistencia que el Dominio no conoce.
        builder.Property<byte[]>("RowVersion")
            .HasColumnName("row_version")
            .IsRowVersion();

        // Sostiene el listado por estado y vigencia (Vigentes = Pendiente con vencimiento >= hoy).
        builder.HasIndex(p => new { p.Estado, p.FechaVencimiento })
            .HasDatabaseName("IX_PRESUPUESTOS_Estado_FechaVencimiento");

        builder.Ignore(p => p.IsDeleted);

        builder.HasOne(p => p.Usuario)
            .WithMany(u => u.Presupuestos)
            .HasForeignKey(p => p.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Cliente)
            .WithMany(c => c.Presupuestos)
            .HasForeignKey(p => p.IdCliente)
            .OnDelete(DeleteBehavior.Restrict);

        // Los renglones viven dentro de la raíz: EF los puebla por el campo privado, no por la vista de solo lectura.
        builder.Navigation(p => p.Detalles)
            .HasField("_detalles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
