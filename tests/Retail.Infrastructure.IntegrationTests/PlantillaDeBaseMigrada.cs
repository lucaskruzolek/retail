using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Crea las bases de las pruebas de integración a partir de una plantilla ya migrada, en lugar de aplicar todas las
/// migraciones en cada test.
/// </summary>
/// <remarks>
/// xUnit ejecuta <c>InitializeAsync</c> una vez por test (no una por clase), así que <c>MigrateAsync</c> repetía las
/// mismas migraciones cientos de veces: unos 9 s por test entre crear la base y migrarla. Acá se migra una sola vez por
/// corrida, se guarda una copia (<c>BACKUP</c>) y cada test la restaura con su propio nombre (unos 2 a 4 s).
/// <para>
/// El aislamiento no cambia: cada test sigue teniendo su propia base, idéntica a una recién migrada. La plantilla se
/// arma de nuevo en cada corrida (no se reutiliza entre corridas) para que nunca quede desactualizada respecto de las
/// migraciones del código.
/// </para>
/// </remarks>
internal static class PlantillaDeBaseMigrada
{
    // Lazy con ExecutionAndPublication (el modo por defecto): aunque varias clases arranquen en paralelo, la plantilla se
    // crea una sola vez y todas esperan la misma tarea.
    private static readonly Lazy<Task<Plantilla>> _plantilla = new(CrearPlantillaAsync);

    /// <summary>
    /// Reemplaza a <c>MigrateAsync()</c> en el arranque de un test: crea la base del contexto con todas las migraciones
    /// aplicadas, restaurándola desde la plantilla. La base no debe existir (los tests usan nombres con un GUID).
    /// </summary>
    public static async Task CrearDesdePlantillaAsync(this DatabaseFacade database)
    {
        var nombre = new SqlConnectionStringBuilder(database.GetConnectionString()).InitialCatalog;
        var plantilla = await _plantilla.Value;

        await using var conexion = new SqlConnection(ServidorDePruebas.CadenaDeConexion("master"));
        await conexion.OpenAsync();

        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            $"RESTORE DATABASE {ComandosSqlServer.Delimitar(nombre)} FROM DISK = @ruta " +
            "WITH MOVE @logicoDatos TO @fisicoDatos, MOVE @logicoLog TO @fisicoLog;";
        comando.Parameters.AddWithValue("@ruta", plantilla.RutaCopia);
        comando.Parameters.AddWithValue("@logicoDatos", plantilla.LogicoDatos);
        comando.Parameters.AddWithValue("@fisicoDatos", Path.Combine(plantilla.DirectorioDatos, $"{nombre}.mdf"));
        comando.Parameters.AddWithValue("@logicoLog", plantilla.LogicoLog);
        comando.Parameters.AddWithValue("@fisicoLog", Path.Combine(plantilla.DirectorioLog, $"{nombre}_log.ldf"));

        // Sin límite de tiempo: con muchas clases en paralelo, la instancia Express puede encolar las restauraciones.
        comando.CommandTimeout = 0;
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<Plantilla> CrearPlantillaAsync()
    {
        var nombre = $"RetailDb_Test_Plantilla_{Guid.NewGuid():N}";
        var cadenaMaster = ServidorDePruebas.CadenaDeConexion("master");

        // xUnit 2 no tiene un "fixture de ensamblado" con cierre: la carpeta con el .bak se borra al terminar el proceso.
        var carpeta = ServidorDePruebas.CrearCarpetaAccesibleParaElMotor("RetailPlantillaTests");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => BorrarCarpeta(carpeta);
        var rutaCopia = Path.Combine(carpeta, "plantilla.bak");

        await using (var contexto = new RetailDbContext(
            new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(ServidorDePruebas.CadenaDeConexion(nombre)).Options))
        {
            await contexto.Database.MigrateAsync();
        }

        await using var conexion = new SqlConnection(cadenaMaster);
        await conexion.OpenAsync();

        // Los nombres lógicos de los archivos se necesitan para el MOVE de cada restauración; las clones se guardan en
        // las mismas carpetas que la plantilla.
        string? logicoDatos = null;
        string? logicoLog = null;
        string? directorioDatos = null;
        string? directorioLog = null;

        await using (var consulta = conexion.CreateCommand())
        {
            consulta.CommandText = "SELECT name, type_desc, physical_name FROM sys.master_files WHERE database_id = DB_ID(@nombre);";
            consulta.Parameters.AddWithValue("@nombre", nombre);

            await using var lector = await consulta.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                if (lector.GetString(1) == "ROWS")
                {
                    logicoDatos = lector.GetString(0);
                    directorioDatos = Path.GetDirectoryName(lector.GetString(2));
                }
                else
                {
                    logicoLog = lector.GetString(0);
                    directorioLog = Path.GetDirectoryName(lector.GetString(2));
                }
            }
        }

        if (logicoDatos is null || logicoLog is null || directorioDatos is null || directorioLog is null)
        {
            throw new InvalidOperationException($"No se encontraron los archivos de datos y de log de la plantilla {nombre}.");
        }

        await using (var copia = conexion.CreateCommand())
        {
            copia.CommandText = $"BACKUP DATABASE {ComandosSqlServer.Delimitar(nombre)} TO DISK = @ruta WITH INIT, COPY_ONLY;";
            copia.Parameters.AddWithValue("@ruta", rutaCopia);
            copia.CommandTimeout = 0;
            await copia.ExecuteNonQueryAsync();
        }

        // Con la copia hecha, la base plantilla ya no hace falta.
        await ComandosSqlServer.EliminarBaseAsync(cadenaMaster, nombre, NullLogger.Instance, CancellationToken.None);

        return new Plantilla(rutaCopia, logicoDatos, logicoLog, directorioDatos, directorioLog);
    }

    private static void BorrarCarpeta(string carpeta)
    {
        try
        {
            Directory.Delete(carpeta, recursive: true);
        }
        catch (IOException)
        {
            // Si el archivo quedó tomado, la carpeta queda en %TEMP% con un prefijo reconocible; no debe romper la corrida.
        }
        catch (UnauthorizedAccessException)
        {
            // Ídem.
        }
    }

    private sealed record Plantilla(
        string RutaCopia,
        string LogicoDatos,
        string LogicoLog,
        string DirectorioDatos,
        string DirectorioLog);
}
