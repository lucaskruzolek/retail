using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express la migración ConcurrenciaCajaYClientes (Track 6.7, Paquete 1): los turnos que
/// ya existían reciben <c>cantidad_ventas</c> recalculada con sus ventas vigentes y no con un 0 que mentiría.
/// </summary>
public class MigracionConcurrenciaCajaYClientesTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261119135959_PresupuestosCorreccion";

    private readonly string _dbName = $"RetailDb_Test_MigConc_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
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
    public async Task Up_TurnoExistenteConVentas_RecalculaCantidadVentasSinContarLasDadasDeBaja()
    {
        // Arrange: base en el esquema anterior, con un turno que tuvo 2 ventas vigentes y 1 dada de baja.
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(MigracionAnterior);
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ROLES (nombre_rol, descripcion, created_at) VALUES ('Cajero', 'Mostrador', SYSUTCDATETIME());

            INSERT INTO USUARIOS (nombre_usuario, password_hash, nombre, apellido, id_rol, created_at)
            VALUES ('cajero1', 'hash', 'Cajero', 'Uno', (SELECT TOP 1 id_rol FROM ROLES), SYSUTCDATETIME());

            INSERT INTO TURNOS_CAJA (id_usuario, fecha_apertura, saldo_inicial, total_ventas_efectivo, total_ingresos_efectivo,
                total_egresos_efectivo, saldo_teorico_efectivo, total_ventas_electronicas, monto_retenido_en_caja, estado, created_at)
            VALUES ((SELECT TOP 1 id_usuario FROM USUARIOS), SYSUTCDATETIME(), 0, 0, 0, 0, 0, 0, 0, 'Abierto', SYSUTCDATETIME());
            """);

        var idTurno = await _context.Database
            .SqlQueryRaw<int>("SELECT id_turno AS Value FROM TURNOS_CAJA")
            .SingleAsync();
        var idUsuario = await _context.Database
            .SqlQueryRaw<int>("SELECT id_usuario AS Value FROM USUARIOS")
            .SingleAsync();

        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO VENTAS (id_turno, id_usuario, fecha_hora, subtotal, descuento, total, estado_fiscal, created_at, deleted_at)
            VALUES ({0}, {1}, SYSUTCDATETIME(), 100, 0, 100, 'Pendiente', SYSUTCDATETIME(), NULL),
                   ({0}, {1}, SYSUTCDATETIME(), 200, 0, 200, 'Pendiente', SYSUTCDATETIME(), NULL),
                   ({0}, {1}, SYSUTCDATETIME(), 300, 0, 300, 'Pendiente', SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            idTurno,
            idUsuario);

        // Act
        await migrator.MigrateAsync();

        // Assert
        var turno = await _context.TurnosCaja.AsNoTracking().SingleAsync();
        turno.CantidadVentas.Should().Be(2);
    }
}
