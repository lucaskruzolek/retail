namespace Retail.Application.UnitTests.TestData;

/// <summary>
/// Reloj fijo para probar los reportes: devuelve siempre el mismo instante UTC y una zona local fija, para no
/// depender de la zona de la máquina que corre las pruebas.
/// </summary>
internal sealed class RelojDePrueba : TimeProvider
{
    private readonly DateTimeOffset _ahoraUtc;
    private readonly TimeZoneInfo _zona;

    public RelojDePrueba(DateTimeOffset ahoraUtc, TimeZoneInfo? zona = null)
    {
        _ahoraUtc = ahoraUtc;
        _zona = zona ?? Argentina;
    }

    /// <summary>Argentina: UTC-3 todo el año (sin horario de verano).</summary>
    public static TimeZoneInfo Argentina { get; } =
        TimeZoneInfo.CreateCustomTimeZone("AR-Test", TimeSpan.FromHours(-3), "AR", "AR");

    /// <summary>Reloj fijado en el mediodía local del 15/10/2026 (15:00 UTC).</summary>
    public static RelojDePrueba EnQuinceDeOctubre() => new(new DateTimeOffset(2026, 10, 15, 15, 0, 0, TimeSpan.Zero));

    public override TimeZoneInfo LocalTimeZone => _zona;

    public override DateTimeOffset GetUtcNow() => _ahoraUtc;
}
