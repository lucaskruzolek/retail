using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express el mapeo de REGISTROS_RESTAURACION y las restricciones que la base garantiza además del
/// agregado: referencia única y operador identificado (D-22).
/// </summary>
public class RegistroRestauracionPersistenciaTests : IAsyncLifetime, IDisposable
{
    private const string Migracion = "20261013083426_AgregarRegistrosBackup";

    private readonly string _dbName = $"RetailDb_Test_Restauracion_{Guid.NewGuid():N}";
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

    private static RegistroRestauracion Registro(Guid? previa, string? baseAnterior) =>
        RegistroRestauracion.Registrar(
            OrigenRestauracionEnum.Aplicacion,
            3,
            "gerente",
            Guid.NewGuid(),
            @"C:\RetailData\Backups\copia.bak",
            Migracion,
            previa,
            baseAnterior);

    [Fact]
    public async Task Guardar_ConTodosLosCampos_SeRecuperaConTodosSusDatos()
    {
        // Arrange
        var previa = Guid.NewGuid();
        var registro = Registro(previa, "RetailDb_Anterior_20261013_190000");
        _context.RegistrosRestauracion.Add(registro);
        await _context.SaveChangesAsync();

        // Act: otro contexto, para leer lo que realmente quedó en la base.
        await using var lectura = CrearContexto();
        var guardado = await lectura.RegistrosRestauracion.SingleAsync();

        // Assert
        guardado.Referencia.Should().Be(registro.Referencia);
        guardado.Origen.Should().Be(OrigenRestauracionEnum.Aplicacion);
        guardado.IdUsuario.Should().Be(3);
        guardado.NombreUsuario.Should().Be("gerente");
        guardado.Fecha.Should().BeCloseTo(registro.Fecha, TimeSpan.FromMilliseconds(1));
        guardado.ReferenciaCopia.Should().Be(registro.ReferenciaCopia);
        guardado.RutaArchivo.Should().Be(@"C:\RetailData\Backups\copia.bak");
        guardado.MigracionCopia.Should().Be(Migracion);
        guardado.ReferenciaCopiaPrevia.Should().Be(previa);
        guardado.BaseAnterior.Should().Be("RetailDb_Anterior_20261013_190000");
    }

    [Fact]
    public async Task Guardar_SinCopiaPreviaNiBaseAnterior_SeRecuperanComoNulos()
    {
        // Arrange: una PC nueva no tiene base vigente que reemplazar (D-20).
        _context.RegistrosRestauracion.Add(Registro(previa: null, baseAnterior: null));
        await _context.SaveChangesAsync();

        // Act
        await using var lectura = CrearContexto();
        var guardado = await lectura.RegistrosRestauracion.SingleAsync();

        // Assert
        guardado.ReferenciaCopiaPrevia.Should().BeNull();
        guardado.BaseAnterior.Should().BeNull();
    }

    [Fact]
    public async Task Guardar_ConOperador_PersisteOperadorYAutorizante()
    {
        // Arrange: control dual (D-30): el Administrador opera y el Gerente autoriza.
        var registro = RegistroRestauracion.Registrar(
            OrigenRestauracionEnum.Aplicacion,
            3,
            "gerente",
            Guid.NewGuid(),
            @"C:\RetailData\Backups\copia.bak",
            Migracion,
            null,
            null,
            idOperador: 5,
            nombreOperador: "admin");
        _context.RegistrosRestauracion.Add(registro);
        await _context.SaveChangesAsync();

        // Act
        await using var lectura = CrearContexto();
        var guardado = await lectura.RegistrosRestauracion.AsNoTracking().SingleAsync();

        // Assert
        guardado.IdUsuario.Should().Be(3);
        guardado.NombreUsuario.Should().Be("gerente");
        guardado.IdOperador.Should().Be(5);
        guardado.NombreOperador.Should().Be("admin");
    }

    [Fact]
    public async Task Guardar_SinOperador_PersisteOperadorNulo()
    {
        // Arrange
        _context.RegistrosRestauracion.Add(Registro(null, null));
        await _context.SaveChangesAsync();

        // Act
        await using var lectura = CrearContexto();
        var guardado = await lectura.RegistrosRestauracion.AsNoTracking().SingleAsync();

        // Assert
        guardado.IdOperador.Should().BeNull();
        guardado.NombreOperador.Should().BeNull();
    }

    [Fact]
    public async Task InsertManual_OperadorIgualAlAutorizante_LoRechazaElCheck()
    {
        var act = () => _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO REGISTROS_RESTAURACION (referencia, origen, id_usuario, nombre_usuario, id_operador, nombre_operador, fecha, referencia_copia, ruta_archivo, migracion_copia, created_at)
            VALUES (NEWID(), 'Consola', 3, 'x', 3, 'x', SYSUTCDATETIME(), NEWID(), 'x.bak', 'm', SYSUTCDATETIME());
            """);

        await act.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>()
            .WithMessage("*CK_REGISTROS_RESTAURACION_Operador*");
    }

    [Fact]
    public async Task Guardar_ReferenciaDuplicada_LaBaseRechazaElSegundo()
    {
        // Arrange: se inserta el mismo registro dos veces, como haría una rehidratación mal hecha.
        _context.RegistrosRestauracion.Add(Registro(null, null));
        await _context.SaveChangesAsync();

        // Act
        await using var otroContexto = CrearContexto();
        var act = () => otroContexto.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO REGISTROS_RESTAURACION (referencia, origen, id_usuario, nombre_usuario, fecha, referencia_copia, ruta_archivo, migracion_copia, created_at)
            SELECT referencia, origen, id_usuario, nombre_usuario, fecha, referencia_copia, ruta_archivo, migracion_copia, created_at
            FROM REGISTROS_RESTAURACION;
            """);

        // Assert
        await act.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>()
            .WithMessage("*IX_REGISTROS_RESTAURACION_referencia*");
    }

    [Fact]
    public async Task InsertManual_UsuarioNoPositivo_LoRechazaElCheck()
    {
        // Un script de soporte que saltea el agregado tampoco puede registrar una restauración anónima.
        var act = () => _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO REGISTROS_RESTAURACION (referencia, origen, id_usuario, nombre_usuario, fecha, referencia_copia, ruta_archivo, migracion_copia, created_at)
            VALUES (NEWID(), 'Consola', 0, 'x', SYSUTCDATETIME(), NEWID(), 'x.bak', 'm', SYSUTCDATETIME());
            """);

        await act.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>()
            .WithMessage("*CK_REGISTROS_RESTAURACION_IdUsuario*");
    }
}
