namespace Retail.App.UnitTests.Helpers;

/// <summary>
/// Reloj fijo con la zona de Argentina (UTC-3, sin horario de verano), para que las fechas locales y las
/// antigüedades no dependan de la máquina ni del momento en que corre el test.
/// </summary>
public sealed class RelojDePrueba : TimeProvider
{
    private static readonly TimeZoneInfo s_argentina =
        TimeZoneInfo.CreateCustomTimeZone("AR-Test", TimeSpan.FromHours(-3), "AR", "AR");

    private readonly DateTimeOffset _ahoraUtc;

    public RelojDePrueba(DateTimeOffset ahoraUtc)
    {
        _ahoraUtc = ahoraUtc;
    }

    public override TimeZoneInfo LocalTimeZone => s_argentina;

    public override DateTimeOffset GetUtcNow() => _ahoraUtc;
}
