using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Fase 7 (D-29): la tarea programada corre <c>Retail.Cli backup</c> como <c>NT AUTHORITY\SYSTEM</c> con los permisos
/// mínimos que le da <c>scripts/registrar-tarea-backup.ps1</c>. Cada prueba suplanta (<c>EXECUTE AS LOGIN</c>) una cuenta
/// con exactamente esos permisos y ejecuta lo mismo que hace la herramienta.
/// </summary>
/// <remarks>
/// Límites: (1) los archivos <c>.bak</c> los escribe la cuenta del servicio del motor, no la cuenta suplantada, así que
/// los permisos de carpeta de la tarea (Modify para SYSTEM) no se prueban acá; (2) la cuenta es un login de SQL Server de
/// prueba, no el login real <c>NT AUTHORITY\SYSTEM</c>, que solo se puede ejercitar desde el Programador de tareas.
/// Las sentencias de copia, verificación y bloqueo son las mismas que emiten <c>SqlServerEjecutorBackup</c> y
/// <c>SqlServerBloqueoRestauracion</c>.
/// </remarks>
[Collection(PermisosTareaProgramadaColeccion.Nombre)]
public class PermisosTareaProgramadaIntegracionTests
{
    private const string Migracion = "20261013190351_AgregarRegistrosRestauracion";

    private readonly PermisosTareaProgramadaFixture _fixture;

    public PermisosTareaProgramadaIntegracionTests(PermisosTareaProgramadaFixture fixture)
    {
        _fixture = fixture;
    }

    private static RetailDbContext ContextoSobre(SqlConnection conexion) =>
        new(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(conexion).Options);

    private string RutaNueva() => Path.Combine(_fixture.Directorio, $"permisos_{Guid.NewGuid():N}.bak");

    private static async Task<object?> EscalarAsync(SqlConnection conexion, string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        return await comando.ExecuteScalarAsync();
    }

    [Fact]
    public async Task EstadoDelEsquema_ConLosPermisosDelScript_LeeMigracionesSinTocarUsuarios()
    {
        // Arrange: lo primero que hace "Retail.Cli backup" (ComandoBackup → IEsquemaBaseDatos). Ya no consulta el estado de
        // los usuarios (IEstadoSistemaService leía el Gerente completo, con su hash de contraseña): la cuenta de la tarea
        // no tiene ningún permiso sobre USUARIOS.
        await using var conexion = await _fixture.AbrirComoAsync(_fixture.LoginCompleto);
        await using var contexto = ContextoSobre(conexion);
        var esquema = new EsquemaBaseDatos(contexto);

        // Act
        var estado = await esquema.ObtenerEstadoAsync();
        var existeHistorial = await esquema.ExisteHistorialDeBackupsAsync();
        var ultimaMigracion = await esquema.ObtenerUltimaMigracionAplicadaAsync();

        // Assert
        estado.PuedeConectar.Should().BeTrue();
        estado.EstaAlDia.Should().BeTrue();
        existeHistorial.Should().BeTrue("la existencia de la tabla se detecta con OBJECT_ID, que exige algún permiso sobre ella");
        ultimaMigracion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ConsultasDeUsuarios_ConLosPermisosDelScript_NoPuedeLeerNiElHashDeLasContrasenas()
    {
        // Arrange: mínimo privilegio. Si la copia necesitara USUARIOS habría que dar un permiso por columnas; como no la
        // necesita, no hay ninguno, y por lo tanto tampoco password_hash (ni nada más de esa tabla).
        await using var conexion = await _fixture.AbrirComoAsync(_fixture.LoginCompleto);
        await using var contexto = ContextoSobre(conexion);

        // Act
        var hash = async () => await EscalarAsync(conexion, "SELECT TOP (1) password_hash FROM USUARIOS");
        var existencia = async () => await contexto.Usuarios.AnyAsync();
        var gerentes = async () => await contexto.Usuarios.Where(u => u.IdRol == 1).ToListAsync();

        // Assert
        (await hash.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("denied");
        (await existencia.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("denied");
        (await gerentes.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("denied");
    }

    [Fact]
    public void PermisosDelScript_NoIncluyenNingunaTablaDeNegocioNiDeUsuarios()
    {
        // Act
        var permisos = PermisosTareaProgramadaFixture.LeerPermisosDelScript("[cuenta]");

        // Assert: la lista completa y exacta de lo que se concede (un permiso nuevo exige actualizar esta prueba a propósito).
        permisos.Select(p => p.Sql).Should().BeEquivalentTo(
        [
            "GRANT CREATE ANY DATABASE TO [cuenta];",
            "ALTER ROLE db_backupoperator ADD MEMBER [cuenta];",
            "GRANT SELECT ON OBJECT::dbo.[__EFMigrationsHistory] TO [cuenta];",
            "GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.REGISTROS_BACKUP TO [cuenta];"
        ]);
    }

    [Fact]
    public async Task HistorialDeCopias_ConLosPermisosDelScript_AltaCambiosDeEstadoYRetencion()
    {
        // Arrange
        await using var conexion = await _fixture.AbrirComoAsync(_fixture.LoginCompleto);
        await using var contexto = ContextoSobre(conexion);
        var registro = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, RutaNueva(), Migracion, "1.0.0");

        // Act: el ciclo de vida completo de una copia automática, más la baja de su archivo por retención.
        contexto.RegistrosBackup.Add(registro);
        await contexto.SaveChangesAsync();

        var enCurso = await contexto.RegistrosBackup.AnyAsync(r => r.Estado == EstadoBackupEnum.EnCurso);

        registro.RegistrarExito(4096, new string('a', 64));
        await contexto.SaveChangesAsync();
        registro.MarcarVerificado();
        await contexto.SaveChangesAsync();
        registro.MarcarArchivoPurgado();
        await contexto.SaveChangesAsync();

        // Assert
        enCurso.Should().BeTrue();
        registro.Estado.Should().Be(EstadoBackupEnum.ArchivoPurgado);
    }

    [Fact]
    public async Task CopiaYVerificacion_ConLosPermisosDelScript_LaCopiaSeCreaYSeVerifica()
    {
        // Arrange
        await using var conexion = await _fixture.AbrirComoAsync(_fixture.LoginCompleto);
        var ruta = RutaNueva();

        // Act: mismas sentencias que SqlServerEjecutorBackup.
        await using (var copia = conexion.CreateCommand())
        {
            copia.CommandText =
                $"BACKUP DATABASE [{_fixture.BaseDeDatos}] TO DISK = @ruta WITH FORMAT, INIT, CHECKSUM, STATS = 10, NAME = @nombre;";
            copia.Parameters.AddWithValue("@ruta", ruta);
            copia.Parameters.AddWithValue("@nombre", "Retail POS - permisos");
            await copia.ExecuteNonQueryAsync();
        }

        await using (var verificacion = conexion.CreateCommand())
        {
            verificacion.CommandText = "RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM;";
            verificacion.Parameters.AddWithValue("@ruta", ruta);
            await verificacion.ExecuteNonQueryAsync();
        }

        // Assert
        File.Exists(ruta).Should().BeTrue();
    }

    [Fact]
    public async Task Verificacion_SinElPermisoDeServidor_FallaPorFaltaDeCreateDatabase()
    {
        // Arrange: la misma cuenta sin "GRANT CREATE ANY DATABASE": prueba que ese permiso hace falta.
        var ruta = RutaNueva();
        await using var conexion = await _fixture.AbrirComoAsync(_fixture.LoginSinPermisoDeServidor);
        await using (var copia = conexion.CreateCommand())
        {
            copia.CommandText = $"BACKUP DATABASE [{_fixture.BaseDeDatos}] TO DISK = @ruta WITH FORMAT, INIT, CHECKSUM;";
            copia.Parameters.AddWithValue("@ruta", ruta);
            await copia.ExecuteNonQueryAsync();
        }

        // Act
        await using var verificacion = conexion.CreateCommand();
        verificacion.CommandText = "RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM;";
        verificacion.Parameters.AddWithValue("@ruta", ruta);
        var act = () => verificacion.ExecuteNonQueryAsync();

        // Assert: BACKUP funcionó (db_backupoperator); VERIFYONLY no.
        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("CREATE DATABASE");
    }

    [Fact]
    public async Task BloqueoDeRestauracion_ConLosPermisosDelScript_ElBloqueoSobreMasterSeObtieneYSeSuelta()
    {
        // Arrange: SqlServerBloqueoRestauracion se conecta a master; la cuenta no tiene usuario ahí (entra como guest).
        await using var conexion = await _fixture.AbrirComoAsync(_fixture.LoginCompleto, "master");

        // Act: mismo pedido que la copia automática (bloqueo compartido).
        var resultado = await EscalarAsync(
            conexion,
            "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = 'Shared', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r;",
            ("@recurso", $"RetailPermisos_{Guid.NewGuid():N}"));

        // Assert: 0 = concedido inmediatamente.
        Convert.ToInt32(resultado, System.Globalization.CultureInfo.InvariantCulture).Should().Be(0);
    }

    [Theory]
    [InlineData("SELECT TOP (1) 1 FROM VENTAS")]
    [InlineData("UPDATE USUARIOS SET nombre_usuario = nombre_usuario WHERE 1 = 0")]
    [InlineData("DELETE FROM REGISTROS_BACKUP WHERE 1 = 0")]
    [InlineData("CREATE TABLE dbo.PRUEBA_PERMISOS (id int)")]
    public async Task CuentaDeLaTarea_FueraDeLoNecesario_NoTienePermiso(string sql)
    {
        // Arrange: mínimo privilegio. La tarea no lee ventas ni clientes, no modifica usuarios, no borra el historial
        // (el borrado es lógico) y no cambia el esquema.
        await using var conexion = await _fixture.AbrirComoAsync(_fixture.LoginCompleto);
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;

        // Act
        var act = () => comando.ExecuteNonQueryAsync();

        // Assert
        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("denied");
    }
}
