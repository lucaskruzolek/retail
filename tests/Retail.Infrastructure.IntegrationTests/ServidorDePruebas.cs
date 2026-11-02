using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.SqlClient;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Punto único de elección del servidor SQL Server de las pruebas de integración.
/// Por defecto usa la instancia local <c>.\SQLEXPRESS</c>; se puede apuntar a otro servidor con la variable de
/// entorno <c>RETAIL_TEST_SQLSERVER</c> (por ejemplo, en CI o en una máquina con otro nombre de instancia).
/// </summary>
internal static class ServidorDePruebas
{
    private const string VariableDeEntorno = "RETAIL_TEST_SQLSERVER";
    private const string InstanciaExpress = @".\SQLEXPRESS";
    private const string InstanciaPredeterminada = ".";

    private static readonly Lazy<string> ServidorResuelto = new(ResolverServidor);

    /// <summary>
    /// Determina la instancia de SQL Server a utilizar. Si no se especifica mediante la variable de
    /// entorno <c>RETAIL_TEST_SQLSERVER</c>, intenta conectarse a <c>.\SQLEXPRESS</c> y, de no responder,
    /// realiza fallback automático a la instancia local predeterminada (<c>.</c>).
    /// </summary>
    private static string ResolverServidor()
    {
        var servidor = Environment.GetEnvironmentVariable(VariableDeEntorno);
        if (!string.IsNullOrWhiteSpace(servidor))
        {
            return servidor;
        }

        if (ProbarConexion(InstanciaExpress))
        {
            return InstanciaExpress;
        }

        if (ProbarConexion(InstanciaPredeterminada))
        {
            return InstanciaPredeterminada;
        }

        return InstanciaExpress;
    }

    private static bool ProbarConexion(string servidor)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = servidor,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 1
            };

            using var conexion = new SqlConnection(builder.ConnectionString);
            conexion.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Arma la cadena de conexión (autenticación de Windows) hacia la base de pruebas indicada.
    /// <c>TrustServerCertificate</c> es necesario porque Microsoft.Data.SqlClient cifra por defecto y el
    /// certificado de una instancia local es autofirmado.
    /// </summary>
    /// <param name="nombreBase">Nombre único de la base; el test que la crea debe borrarla al terminar.</param>
    public static string CadenaDeConexion(string nombreBase)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = ServidorResuelto.Value,
            InitialCatalog = nombreBase,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Crea una carpeta temporal única donde el motor puede escribir y leer archivos <c>.bak</c>, y el proceso de pruebas
    /// también. En SQL Server Express, <c>BACKUP</c> y <c>RESTORE</c> los ejecuta la cuenta del servicio
    /// (por ejemplo, <c>NT Service\MSSQL$SQLEXPRESS</c>), que no tiene permiso sobre el <c>%TEMP%</c> del usuario
    /// (error 5, acceso denegado). Se le otorga Modify con herencia sobre la carpeta; las carpetas padre no necesitan
    /// permisos porque Windows omite la verificación de recorrido por defecto.
    /// </summary>
    /// <param name="prefijo">Prefijo del nombre de la carpeta, para reconocerla si una prueba la deja huérfana.</param>
    /// <returns>Ruta de la carpeta creada. Quien la pide debe borrarla al terminar.</returns>
    public static string CrearCarpetaAccesibleParaElMotor(string prefijo)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Los permisos de la carpeta de pruebas se otorgan con ACL de Windows.");
        }

        var cuentaDelMotor = ObtenerCuentaDelServicioDelMotor();

        var ruta = Path.Combine(Path.GetTempPath(), $"{prefijo}_{Guid.NewGuid():N}");
        var carpeta = Directory.CreateDirectory(ruta);

        var seguridad = carpeta.GetAccessControl();
        seguridad.AddAccessRule(new FileSystemAccessRule(
            new NTAccount(cuentaDelMotor),
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        carpeta.SetAccessControl(seguridad);

        return ruta;
    }

    /// <summary>
    /// Lee de <c>sys.dm_server_services</c> la cuenta con la que corre el servicio del motor. Si no se puede obtener,
    /// falla con un mensaje claro: seguir sin permisos produciría errores de acceso denegado difíciles de rastrear.
    /// </summary>
    internal static string ObtenerCuentaDelServicioDelMotor()
    {
        try
        {
            using var conexion = new SqlConnection(CadenaDeConexion("master"));
            conexion.Open();

            using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT TOP (1) service_account FROM sys.dm_server_services WHERE servicename LIKE 'SQL Server (%';";

            var cuenta = comando.ExecuteScalar() as string;
            return string.IsNullOrWhiteSpace(cuenta)
                ? throw new InvalidOperationException("sys.dm_server_services no devolvió la cuenta del servicio de SQL Server.")
                : cuenta;
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                "No se pudo leer la cuenta del servicio de SQL Server (sys.dm_server_services requiere VIEW SERVER STATE). " +
                "Sin ella no se pueden dar permisos de escritura a las pruebas de copias de seguridad.", ex);
        }
    }
}
