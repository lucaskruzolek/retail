using FluentAssertions;
using Retail.Infrastructure.Terminales;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Pruebas del archivo local que identifica a esta PC como terminal. Usan una carpeta temporal propia: nunca tocan
/// ProgramData.
/// </summary>
public sealed class TerminalLocalArchivoTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), $"RetailTerminal_{Guid.NewGuid():N}");

    private string Ruta => Path.Combine(_carpeta, "sub", "terminal.json");

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact]
    public async Task GuardarAsync_CarpetaInexistente_CreaElArchivoYSeLeeEnOtraInstancia()
    {
        // Arrange
        var primera = new TerminalLocalArchivo(Ruta);

        // Act
        await primera.GuardarAsync(3);

        // Assert: la propia instancia y una nueva (como al reiniciar la aplicación) ven el mismo valor.
        primera.IdTerminal.Should().Be(3);
        new TerminalLocalArchivo(Ruta).IdTerminal.Should().Be(3);
        File.Exists(Ruta).Should().BeTrue();
        File.Exists(Ruta + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task GuardarAsync_ArchivoExistente_LoReemplaza()
    {
        // Arrange
        await new TerminalLocalArchivo(Ruta).GuardarAsync(3);
        var sut = new TerminalLocalArchivo(Ruta);

        // Act
        await sut.GuardarAsync(8);

        // Assert
        sut.IdTerminal.Should().Be(8);
        new TerminalLocalArchivo(Ruta).IdTerminal.Should().Be(8);
    }

    [Fact]
    public void IdTerminal_ArchivoInexistente_EsNulo()
    {
        new TerminalLocalArchivo(Ruta).IdTerminal.Should().BeNull();
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("{ \"IdTerminal\": \"abc\" }")]
    [InlineData("{ \"IdTerminal\": 0 }")]
    [InlineData("null")]
    [InlineData("")]
    public void IdTerminal_ArchivoCorrupto_EsNulo(string contenido)
    {
        // Arrange
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
        File.WriteAllText(Ruta, contenido);

        // Act / Assert
        new TerminalLocalArchivo(Ruta).IdTerminal.Should().BeNull();
    }

    [Fact]
    public async Task GuardarAsync_IdInvalido_LanzaArgumentOutOfRange()
    {
        var sut = new TerminalLocalArchivo(Ruta);

        var act = async () => await sut.GuardarAsync(0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NombreEquipo_SiempreEsElDeLaPC()
    {
        new TerminalLocalArchivo(Ruta).NombreEquipo.Should().Be(Environment.MachineName);
    }
}
