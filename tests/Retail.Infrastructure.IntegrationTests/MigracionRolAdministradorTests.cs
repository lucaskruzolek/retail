using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra LocalDB la migración AgregarRolAdministradorYCuentasDeUsuario en los dos escenarios que debe
/// cubrir: una base existente (sembrada por el seeder anterior con Cajero, Encargado y Gerente) y una base vacía.
/// </summary>
public class MigracionRolAdministradorTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261009195202_NormalizarCuitProveedoresYCheck";

    private readonly string _dbName = $"RetailDb_Test_MigRol_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Up_BaseConLosTresRolesAnteriores_AgregaSoloAdministradorYConservaLasCuentas()
    {
        // Arrange: el estado que dejaba el seeder anterior (IDENTITY asignó 1, 2 y 3) y un Gerente existente.
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(MigracionAnterior);
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ROLES (nombre_rol, descripcion, created_at) VALUES
                ('Cajero', 'Operador de punto de venta, mostrador y cobranzas', SYSUTCDATETIME()),
                ('Encargado', 'Encargado de inventario, compras a distribuidores y control de caja', SYSUTCDATETIME()),
                ('Gerente', 'Administrador general con acceso total, consola fiscal y configuración', SYSUTCDATETIME());

            INSERT INTO USUARIOS (nombre_usuario, password_hash, nombre, apellido, id_rol, created_at)
            VALUES ('admin', 'hash', 'Administrador', 'General', 3, SYSUTCDATETIME());
            """);

        // Act
        await migrator.MigrateAsync();

        // Assert
        var roles = await _context.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        roles.Select(r => (r.Id, r.NombreRol)).Should().Equal(
            (1, "Cajero"), (2, "Encargado"), (3, "Gerente"), ((int)RolUsuarioEnum.Administrador, "Administrador"));
        roles.Single(r => r.Id == 3).Descripcion.Should().NotContain("Administrador");

        // Las cuentas existentes no quedan obligadas a cambiar la contraseña ni con email inventado.
        var admin = await _context.Usuarios.AsNoTracking().SingleAsync();
        admin.DebeCambiarPassword.Should().BeFalse();
        admin.EmailRecuperacion.Should().BeNull();
    }

    [Fact]
    public async Task Up_BaseVacia_SiembraLosCuatroRolesConElIdentificadorDelEnum()
    {
        // Act
        await _context.GetService<IMigrator>().MigrateAsync();

        // Assert
        var roles = await _context.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        roles.Select(r => (r.Id, r.NombreRol)).Should().Equal(
            Enum.GetValues<RolUsuarioEnum>().Select(rol => ((int)rol, rol.ToString())));
    }
}
