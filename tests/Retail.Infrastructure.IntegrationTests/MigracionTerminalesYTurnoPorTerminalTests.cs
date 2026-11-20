using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express la migración TerminalesYTurnoPorTerminal (Track 6.7, Paquete 3): los turnos que ya
/// existían quedan atribuidos a una terminal histórica "Terminal 1", y una base sin turnos no recibe terminales.
/// </summary>
public class MigracionTerminalesYTurnoPorTerminalTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261119211248_ConcurrenciaCajaYClientes";

    private readonly string _dbName = $"RetailDb_Test_MigTerm_{Guid.NewGuid():N}";
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
    public async Task Up_TurnosExistentes_QuedanAsignadosATerminal1()
    {
        // Arrange: base en el esquema anterior, con un turno cerrado y otro abierto.
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(MigracionAnterior);
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ROLES (nombre_rol, descripcion, created_at) VALUES ('Cajero', 'Mostrador', SYSUTCDATETIME());

            INSERT INTO USUARIOS (nombre_usuario, password_hash, nombre, apellido, id_rol, created_at)
            VALUES ('cajero1', 'hash', 'Cajero', 'Uno', (SELECT TOP 1 id_rol FROM ROLES), SYSUTCDATETIME());

            INSERT INTO TURNOS_CAJA (id_usuario, fecha_apertura, saldo_inicial, total_ventas_efectivo, total_ingresos_efectivo,
                total_egresos_efectivo, saldo_teorico_efectivo, total_ventas_electronicas, monto_retenido_en_caja, estado,
                cantidad_ventas, created_at)
            VALUES ((SELECT TOP 1 id_usuario FROM USUARIOS), SYSUTCDATETIME(), 0, 0, 0, 0, 0, 0, 0, 'Cerrado', 0, SYSUTCDATETIME()),
                   ((SELECT TOP 1 id_usuario FROM USUARIOS), SYSUTCDATETIME(), 0, 0, 0, 0, 0, 0, 0, 'Abierto', 0, SYSUTCDATETIME());
            """);

        // Act
        await migrator.MigrateAsync();

        // Assert
        var terminales = await _context.Terminales.AsNoTracking().ToListAsync();
        terminales.Should().ContainSingle();
        terminales[0].Nombre.Should().Be("Terminal 1");
        terminales[0].NombreEquipo.Should().BeEmpty();

        var turnos = await _context.TurnosCaja.AsNoTracking().ToListAsync();
        turnos.Should().HaveCount(2);
        turnos.Should().OnlyContain(t => t.IdTerminal == terminales[0].Id);
    }

    [Fact]
    public async Task Up_BaseSinTurnos_NoCreaTerminales()
    {
        // Arrange
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(MigracionAnterior);

        // Act
        await migrator.MigrateAsync();

        // Assert
        (await _context.Terminales.AsNoTracking().CountAsync()).Should().Be(0);
    }
}
