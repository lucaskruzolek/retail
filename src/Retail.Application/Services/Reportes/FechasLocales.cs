namespace Retail.Application.Services.Reportes;

/// <summary>
/// Hora local del comercio a partir de un <see cref="TimeProvider"/>. Los reportes trabajan con días calendario
/// locales (D-9); inyectar el reloj permite probarlos sin depender de la zona de la máquina.
/// </summary>
internal static class FechasLocales
{
    /// <summary>Fecha y hora actuales en la zona local del reloj.</summary>
    public static DateTime AhoraLocal(this TimeProvider reloj)
    {
        return TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), reloj.LocalTimeZone).DateTime;
    }

    /// <summary>Día calendario actual en la zona local del reloj.</summary>
    public static DateOnly HoyLocal(this TimeProvider reloj)
    {
        return DateOnly.FromDateTime(reloj.AhoraLocal());
    }
}
