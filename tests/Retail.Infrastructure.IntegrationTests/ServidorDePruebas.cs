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
    private const string ServidorPorDefecto = @".\SQLEXPRESS";

    /// <summary>
    /// Arma la cadena de conexión (autenticación de Windows) hacia la base de pruebas indicada.
    /// <c>TrustServerCertificate</c> es necesario porque Microsoft.Data.SqlClient cifra por defecto y el
    /// certificado de una instancia local es autofirmado.
    /// </summary>
    /// <param name="nombreBase">Nombre único de la base; el test que la crea debe borrarla al terminar.</param>
    public static string CadenaDeConexion(string nombreBase)
    {
        var servidor = Environment.GetEnvironmentVariable(VariableDeEntorno);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(servidor) ? ServidorPorDefecto : servidor,
            InitialCatalog = nombreBase,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true
        };

        return builder.ConnectionString;
    }
}
