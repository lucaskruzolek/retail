namespace Retail.App.Services;

/// <summary>
/// Conversión de las horas UTC que informa el servidor a la hora local de esta PC para mostrarlas.
/// </summary>
public static class ConversionHora
{
    /// <summary>Convierte una hora UTC (aunque llegue con <c>Kind</c> sin especificar, como la lee SQL Server) a hora local.</summary>
    public static DateTime AHoraLocal(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
    }
}
