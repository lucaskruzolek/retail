using Microsoft.EntityFrameworkCore;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Contadores de la instancia de SQL Server para las mediciones de rendimiento: se leen antes y después de una operación
/// y se informa la diferencia.
/// </summary>
internal static class EstadisticasSqlServer
{
    /// <summary>
    /// Compilaciones de SQL acumuladas por la instancia (contador "SQL Compilations/sec", que pese al nombre es un
    /// total). Incluye la poca actividad ajena que haya en el servidor.
    /// </summary>
    public static async Task<long> CompilacionesAsync(DbContext context)
    {
        return await context.Database
            .SqlQuery<long>($"""
                SELECT cntr_value AS [Value]
                FROM sys.dm_os_performance_counters
                WHERE counter_name = 'SQL Compilations/sec' AND object_name LIKE '%SQL Statistics%'
                """)
            .SingleAsync();
    }
}
