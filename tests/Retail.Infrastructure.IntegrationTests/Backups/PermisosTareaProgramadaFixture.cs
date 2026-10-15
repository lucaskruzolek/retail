using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Base migrada y dos cuentas de prueba para <see cref="PermisosTareaProgramadaIntegracionTests"/>: una con EXACTAMENTE
/// los permisos que <c>scripts/registrar-tarea-backup.ps1</c> le da a <c>NT AUTHORITY\SYSTEM</c>, y otra con los mismos
/// menos el permiso de servidor (para probar que ese permiso hace falta).
/// </summary>
/// <remarks>
/// Los permisos no se repiten acá: se leen del script, de modo que la prueba verifica lo que el script realmente otorga.
/// Las cuentas son logins de SQL Server sin acceso a la red (la instancia usa solo autenticación de Windows) y se usan por
/// suplantación (<c>EXECUTE AS LOGIN</c>) desde la conexión del usuario de pruebas.
/// </remarks>
public sealed partial class PermisosTareaProgramadaFixture : IAsyncLifetime
{
    private readonly List<string> _logins = [];

    public string BaseDeDatos { get; } = $"RetailDb_Test_Permisos_{Guid.NewGuid():N}";

    public string LoginCompleto { get; } = $"RetailTestTarea_{Guid.NewGuid():N}";

    public string LoginSinPermisoDeServidor { get; } = $"RetailTestTareaSinServidor_{Guid.NewGuid():N}";

    /// <summary>
    /// Carpeta donde el motor puede escribir los <c>.bak</c> (el motor, no la cuenta suplantada, escribe los archivos).
    /// </summary>
    public string Directorio { get; } = ServidorDePruebas.CrearCarpetaAccesibleParaElMotor("RetailPermisosTests");

    public static string CadenaDeConexion(string baseDeDatos, bool pooling = false)
    {
        // Sin pool: una conexión que suplantó a otra cuenta no debe volver al pool con esa identidad puesta.
        return new SqlConnectionStringBuilder(ServidorDePruebas.CadenaDeConexion(baseDeDatos)) { Pooling = pooling }.ConnectionString;
    }

    /// <summary>
    /// Abre una conexión a la base de pruebas (o a otra) que ejecuta todo como la cuenta de prueba indicada.
    /// </summary>
    public async Task<SqlConnection> AbrirComoAsync(string login, string? baseDeDatos = null)
    {
        var conexion = new SqlConnection(CadenaDeConexion(baseDeDatos ?? BaseDeDatos));
        await conexion.OpenAsync();
        await EjecutarAsync(conexion, $"EXECUTE AS LOGIN = N'{login}';");
        return conexion;
    }

    /// <summary>
    /// Los permisos de <c>$Permisos</c> del script, con <c>{0}</c> ya reemplazado por la cuenta indicada.
    /// </summary>
    public static IReadOnlyList<(string Ambito, string Sql)> LeerPermisosDelScript(string cuentaDelimitada)
    {
        var script = File.ReadAllText(RutaDelScript());
        var permisos = PermisoDelScript().Matches(script)
            .Select(m => (m.Groups["ambito"].Value, m.Groups["sql"].Value.Replace("{0}", cuentaDelimitada, StringComparison.Ordinal)))
            .ToList();

        if (permisos.Count == 0)
        {
            throw new InvalidOperationException("No se encontraron permisos en $Permisos de registrar-tarea-backup.ps1: ¿cambió el formato?");
        }

        return permisos;
    }

    public async Task InitializeAsync()
    {
        await using (var contexto = new RetailDbContext(
            new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(CadenaDeConexion(BaseDeDatos, pooling: true)).Options))
        {
            await contexto.Database.MigrateAsync();
        }

        await CrearCuentaAsync(LoginCompleto, incluirPermisosDeServidor: true);
        await CrearCuentaAsync(LoginSinPermisoDeServidor, incluirPermisosDeServidor: false);
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var master = new SqlConnection(CadenaDeConexion("master"));
        await master.OpenAsync();

        var delimitada = $"[{BaseDeDatos}]";
        await EjecutarAsync(master,
            $"IF DB_ID(N'{BaseDeDatos}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE {delimitada} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {delimitada}; END");

        foreach (var login in _logins)
        {
            await EjecutarAsync(master, $"IF SUSER_ID(N'{login}') IS NOT NULL DROP LOGIN [{login}];");
        }

        SqlConnection.ClearAllPools();

        if (Directory.Exists(Directorio))
        {
            Directory.Delete(Directorio, recursive: true);
        }
    }

    private async Task CrearCuentaAsync(string login, bool incluirPermisosDeServidor)
    {
        await using var master = new SqlConnection(CadenaDeConexion("master"));
        await master.OpenAsync();

        // Contraseña descartable: nadie puede usarla (la instancia no acepta autenticación de SQL Server).
        await EjecutarAsync(master, $"CREATE LOGIN [{login}] WITH PASSWORD = N'{Guid.NewGuid():N}Aa1!', CHECK_POLICY = OFF;");
        _logins.Add(login);

        await using var enLaBase = new SqlConnection(CadenaDeConexion(BaseDeDatos));
        await enLaBase.OpenAsync();
        await EjecutarAsync(enLaBase, $"CREATE USER [{login}] FOR LOGIN [{login}];");

        foreach (var (ambito, sql) in LeerPermisosDelScript($"[{login}]"))
        {
            if (ambito == "Servidor")
            {
                if (incluirPermisosDeServidor)
                {
                    await EjecutarAsync(master, sql);
                }

                continue;
            }

            await EjecutarAsync(enLaBase, sql);
        }
    }

    private static async Task EjecutarAsync(SqlConnection conexion, string sql)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    private static string RutaDelScript()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            var candidata = Path.Combine(carpeta.FullName, "scripts", "registrar-tarea-backup.ps1");
            if (File.Exists(candidata))
            {
                return candidata;
            }
        }

        throw new FileNotFoundException("No se encontró scripts/registrar-tarea-backup.ps1 subiendo desde la carpeta de las pruebas.");
    }

    // Una línea de la lista: @{ Ambito = 'Servidor'; Sql = '...'; Motivo = '...' }
    [GeneratedRegex(@"Ambito\s*=\s*'(?<ambito>Servidor|BaseDeDatos)'\s*;\s*Sql\s*=\s*'(?<sql>[^']+)'")]
    private static partial Regex PermisoDelScript();
}
