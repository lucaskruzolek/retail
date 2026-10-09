using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra LocalDB que la migración SepararNombreApellidoUsuarios conserva las filas existentes:
/// el scaffold original de EF borraba nombre_completo antes de completar las columnas nuevas.
/// </summary>
public class MigracionSepararNombreApellidoTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261009054743_BusquedaInsensibleAAcentos";

    private readonly string _dbName = $"RetailDb_Test_MigNombre_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);

        // La base queda con el esquema anterior: USUARIOS todavía tiene nombre_completo.
        await _context.GetService<IMigrator>().MigrateAsync(MigracionAnterior);
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
    public async Task Up_UsuariosConNombreCompleto_SeparaEnElUltimoEspacioYPasaLoginAMinusculas()
    {
        // Arrange: filas con el esquema viejo, escritas con SQL porque la entidad actual ya no tiene nombre_completo.
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ROLES (nombre_rol, descripcion, created_at) VALUES ('Gerente', 'Prueba', SYSUTCDATETIME());
            DECLARE @rol int = SCOPE_IDENTITY();
            INSERT INTO USUARIOS (nombre_usuario, password_hash, nombre_completo, id_rol, created_at) VALUES
                ('Admin', 'hash', 'Administrador General', @rol, SYSUTCDATETIME()),
                ('jcperez', 'hash', '  Juan Carlos Pérez ', @rol, SYSUTCDATETIME()),
                ('cajero', 'hash', 'Cajero', @rol, SYSUTCDATETIME());
            """);

        // Act
        await _context.GetService<IMigrator>().MigrateAsync();

        // Assert
        var usuarios = await _context.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new { u.NombreUsuario, u.Nombre, u.Apellido })
            .ToListAsync();

        usuarios.Should().Equal(
            new { NombreUsuario = "admin", Nombre = "Administrador", Apellido = "General" },
            new { NombreUsuario = "jcperez", Nombre = "Juan Carlos", Apellido = "Pérez" },
            new { NombreUsuario = "cajero", Nombre = "Cajero", Apellido = "Cajero" });
    }
}
