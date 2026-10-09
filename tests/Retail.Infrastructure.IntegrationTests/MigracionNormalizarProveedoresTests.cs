using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra LocalDB la migración NormalizarCuitProveedoresYCheck: normaliza los CUIT existentes sin
/// violar el índice único y, a partir de ahí, el CHECK rechaza cualquier CUIT que no sean 11 dígitos.
/// </summary>
public class MigracionNormalizarProveedoresTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261009192638_NormalizarDocumentosYContactoClientes";

    private readonly string _dbName = $"RetailDb_Test_MigProv_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
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
    public async Task Up_CuitConGuiones_LoNormalizaSalvoQueChoqueConOtroProveedorActivo()
    {
        // Arrange: el proveedor 2 ya tiene guardado "20123456786", el mismo CUIT que el 1 escrito con guiones.
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO PROVEEDORES (razon_social, cuit, telefono, email, created_at) VALUES
                ('Distribuidora Norte', '30-71234567-1', '(011) 4555-1234', ' Ventas@Norte.COM ', SYSUTCDATETIME()),
                ('Papelera Sur', '20-12345678-6', NULL, NULL, SYSUTCDATETIME()),
                ('Papelera Sur Duplicada', '20123456786', NULL, NULL, SYSUTCDATETIME());
            """);

        // Act
        await _context.GetService<IMigrator>().MigrateAsync();

        // Assert
        var proveedores = await _context.Proveedores
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Cuit, p.Telefono, p.Email })
            .ToListAsync();

        proveedores.Should().Equal(
            new { Cuit = "30712345671", Telefono = (string?)"01145551234", Email = (string?)"ventas@norte.com" },
            new { Cuit = "20-12345678-6", Telefono = (string?)null, Email = (string?)null },
            new { Cuit = "20123456786", Telefono = (string?)null, Email = (string?)null });
    }

    [Fact]
    public async Task CheckConstraint_InsertConCuitConGuionesFueraDeLaAplicacion_LoRechazaLaBase()
    {
        // Arrange
        await _context.GetService<IMigrator>().MigrateAsync();

        // Act: un INSERT manual (script de soporte, herramienta ETL) que saltea el agregado.
        var act = () => _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO PROVEEDORES (razon_social, cuit, created_at) VALUES ('Manual', '30-71234567-1', SYSUTCDATETIME());");

        // Assert
        (await act.Should().ThrowAsync<SqlException>()).WithMessage("*CK_PROVEEDORES_Cuit*");
    }
}
