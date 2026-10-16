using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Exceptions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express el mapeo de REGISTROS_BACKUP y las restricciones que la base garantiza además del
/// agregado: un único backup en curso, referencia única y coherencia entre tipo y operador.
/// </summary>
public class RegistroBackupPersistenciaTests : IAsyncLifetime, IDisposable
{
    private const string Migracion = "20261013071257_AgregarRolAdministradorYCuentasDeUsuario";

    private readonly string _dbName = $"RetailDb_Test_Backup_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        _context = CrearContexto();
        await _context.Database.MigrateAsync();
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

    private RetailDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(_connectionString).Options);

    private static RegistroBackup Automatico(string archivo = "a.bak") =>
        RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, $@"C:\RetailData\Backups\{archivo}", Migracion, "1.0.0");

    [Fact]
    public async Task Guardar_CicloCompleto_SeRecuperaConTodosSusDatos()
    {
        // Arrange
        var registro = RegistroBackup.Iniciar(TipoBackupEnum.Manual, 3, "gerente", @"C:\RetailData\Backups\m.bak", Migracion, "1.0.0");
        registro.RegistrarExito(4096, new string('b', 64));
        registro.MarcarVerificado();
        _context.RegistrosBackup.Add(registro);
        await _context.SaveChangesAsync();

        // Act: otro contexto, para leer lo que realmente quedó en la base.
        await using var lectura = CrearContexto();
        var guardado = await lectura.RegistrosBackup.SingleAsync();

        // Assert
        guardado.Referencia.Should().Be(registro.Referencia);
        guardado.Tipo.Should().Be(TipoBackupEnum.Manual);
        guardado.Estado.Should().Be(EstadoBackupEnum.Verificado);
        guardado.IdUsuario.Should().Be(3);
        guardado.NombreUsuario.Should().Be("gerente");
        guardado.TamanoBytes.Should().Be(4096);
        guardado.HashSha256.Should().Be(new string('b', 64));
        guardado.UltimaMigracion.Should().Be(Migracion);
        guardado.EsRestaurable.Should().BeTrue();
    }

    [Fact]
    public async Task Guardar_OperadorInexistenteEnUsuarios_SeAceptaPorqueNoHayClaveForanea()
    {
        // El historial se reinserta en bases restauradas donde el usuario puede no existir (R-1).
        _context.RegistrosBackup.Add(
            RegistroBackup.Iniciar(TipoBackupEnum.Manual, 999, "usuario.posterior", @"C:\b.bak", Migracion, "1.0.0"));

        var act = () => _context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Guardar_DosBackupsEnCurso_LaBaseRechazaElSegundo()
    {
        // Arrange
        _context.RegistrosBackup.Add(Automatico("primero.bak"));
        await _context.SaveChangesAsync();

        // Act: otra instancia (por ejemplo, el servicio automático y un Gerente al mismo tiempo).
        await using var otroProceso = CrearContexto();
        otroProceso.RegistrosBackup.Add(Automatico("segundo.bak"));
        var act = () => otroProceso.SaveChangesAsync();

        // Assert
        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<Microsoft.Data.SqlClient.SqlException>()
            .WithMessage("*IX_REGISTROS_BACKUP_EnCurso*");
    }

    [Fact]
    public async Task SaveChangesAsync_DosBackupsEnCursoSimultaneos_ElSegundoLanzaRestriccionDeUnicidadYDescartaSusCambios()
    {
        // Arrange: dos procesos pasaron a la vez el control de "copia en curso"; el primero guarda.
        _context.RegistrosBackup.Add(Automatico("primero.bak"));
        await new UnitOfWork(_context).SaveChangesAsync();

        await using var otroProceso = CrearContexto();
        var unidadDelOtroProceso = new UnitOfWork(otroProceso);
        otroProceso.RegistrosBackup.Add(Automatico("segundo.bak"));

        // Act
        var act = () => unidadDelOtroProceso.SaveChangesAsync();

        // Assert: la Aplicación recibe una excepción propia, sin el tipo de EF Core, y el contexto queda limpio.
        (await act.Should().ThrowAsync<RestriccionDeUnicidadException>())
            .WithInnerException<DbUpdateException>();
        otroProceso.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Guardar_BackupTerminadoYOtroEnCurso_SeAceptan()
    {
        // El índice solo restringe los registros EnCurso: el historial puede crecer sin límite.
        var terminado = Automatico("viejo.bak");
        terminado.RegistrarFallo("Disco lleno");
        _context.RegistrosBackup.AddRange(terminado, Automatico("nuevo.bak"));

        var act = () => _context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InsertManual_CopiaManualSinOperador_LaRechazaElCheck()
    {
        // Un script de soporte que saltea el agregado tampoco puede registrar una copia manual anónima.
        var act = () => _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO REGISTROS_BACKUP (referencia, tipo, estado, fecha_inicio, ruta_archivo, ultima_migracion, version_app, created_at)
            VALUES (NEWID(), 'Manual', 'Fallido', SYSUTCDATETIME(), 'x.bak', 'm', '1.0.0', SYSUTCDATETIME());
            """);

        await act.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>()
            .WithMessage("*CK_REGISTROS_BACKUP_Operador*");
    }
}
