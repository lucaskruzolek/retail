using FluentAssertions;
using Retail.Infrastructure.Logging;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Logging;

/// <summary>
/// Regla única de ubicación de los registros (aplicación y Retail.Cli): <c>%LOCALAPPDATA%\Retail POS\logs</c>.
/// Se inyecta la carpeta base, así las pruebas no dependen del perfil de la cuenta que las ejecuta ni tocan el disco.
/// </summary>
public class UbicacionDeLogsTests
{
    [Fact]
    public void Directorio_ConCarpetaBase_DevuelveRetailPosLogsDentroDeLaBase()
    {
        // Arrange
        var carpetaBase = Path.Combine("C:", "Users", "cajero", "AppData", "Local");

        // Act
        var directorio = UbicacionDeLogs.Directorio(carpetaBase);

        // Assert
        directorio.Should().Be(Path.Combine(carpetaBase, "Retail POS", "logs"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Directorio_ConCarpetaBaseVacia_UsaLaCarpetaTemporalDelSistema(string? carpetaBase)
    {
        // Act
        var directorio = UbicacionDeLogs.Directorio(carpetaBase);

        // Assert
        directorio.Should().Be(Path.Combine(Path.GetTempPath(), "Retail POS", "logs"));
    }

    [Fact]
    public void Directorio_SinParametros_UsaLaCarpetaLocalApplicationDataDeLaCuentaActual()
    {
        // Arrange
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Act
        var directorio = UbicacionDeLogs.Directorio();

        // Assert
        directorio.Should().Be(UbicacionDeLogs.Directorio(local));
    }
}
