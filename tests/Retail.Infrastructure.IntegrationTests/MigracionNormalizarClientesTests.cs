using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra LocalDB que la migración NormalizarDocumentosYContactoClientes lleva los datos existentes a la
/// forma canónica sin violar el índice único de documentos de clientes activos.
/// </summary>
public class MigracionNormalizarClientesTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261009184557_SepararNombreApellidoUsuarios";

    private readonly string _dbName = $"RetailDb_Test_MigClientes_{Guid.NewGuid():N}";
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
    public async Task Up_DocumentosYContactosConFormato_LosNormalizaSinViolarElIndiceUnico()
    {
        // Arrange: el cliente 3 normalizaría al mismo DNI que el 2, que ya está activo y normalizado.
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO CLIENTES (razon_social_o_nombre, tipo_documento, numero_documento, condicion_iva, email, telefono,
                                  tiene_cuenta_corriente, limite_credito, saldo_cuenta_corriente, created_at)
            VALUES
                ('Ana Gómez', 'Dni', '12.345.678', 'ConsumidorFinal', ' Ana@Mail.COM ', '(011) 4555-1234', 0, 0, 0, SYSUTCDATETIME()),
                ('Beto Ruiz', 'Dni', '23456789', 'ConsumidorFinal', NULL, NULL, 0, 0, 0, SYSUTCDATETIME()),
                ('Carla Ruiz', 'Dni', '23.456.789', 'ConsumidorFinal', '   ', NULL, 0, 0, 0, SYSUTCDATETIME()),
                ('John Smith', 'Pasaporte', 'aaa 123456', 'ConsumidorFinal', NULL, NULL, 0, 0, 0, SYSUTCDATETIME());
            """);

        // Act
        await _context.GetService<IMigrator>().MigrateAsync();

        // Assert
        var clientes = await _context.Clientes
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new { c.NumeroDocumento, c.Email, c.Telefono })
            .ToListAsync();

        clientes.Should().Equal(
            new { NumeroDocumento = "12345678", Email = (string?)"ana@mail.com", Telefono = (string?)"01145551234" },
            new { NumeroDocumento = "23456789", Email = (string?)null, Telefono = (string?)null },
            new { NumeroDocumento = "23.456.789", Email = (string?)null, Telefono = (string?)null },
            new { NumeroDocumento = "AAA123456", Email = (string?)null, Telefono = (string?)null });
    }
}
