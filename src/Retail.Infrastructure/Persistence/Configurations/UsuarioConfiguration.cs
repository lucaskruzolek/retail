using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retail.Domain.Entities;

namespace Retail.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("USUARIOS");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id_usuario");

        builder.Property(u => u.NombreUsuario)
            .HasColumnName("nombre_usuario")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(u => u.NombreUsuario)
            .IsUnique()
            .HasFilter("[deleted_at] IS NULL");

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.NombreCompleto)
            .HasColumnName("nombre_completo")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.IdRol)
            .HasColumnName("id_rol")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(u => u.IsDeleted);

        builder.HasOne(u => u.Rol)
            .WithMany(r => r.Usuarios)
            .HasForeignKey(u => u.IdRol)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
