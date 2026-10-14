using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Infrastructure.Backups;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Lectura del manifiesto y huella de una copia elegida por el operador para restaurar (RF-22): archivos reales en una
/// carpeta temporal, y rutas que el sistema debe rechazar antes de dárselas al motor.
/// </summary>
public class ArchivosBackupRestauracionTests : IDisposable
{
    private readonly string _directorio = Path.Combine(Path.GetTempPath(), $"RetailArchivosTests_{Guid.NewGuid():N}");
    private readonly ArchivosBackup _sut;

    public ArchivosBackupRestauracionTests()
    {
        Directory.CreateDirectory(_directorio);
        _sut = new ArchivosBackup(Options.Create(new BackupOptions { Directorio = _directorio }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static ManifiestoBackupDto Manifiesto(string archivoBak) => new()
    {
        Referencia = Guid.NewGuid(),
        Tipo = "Manual",
        FechaInicioUtc = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
        Operador = "gerente",
        ArchivoBak = archivoBak,
        TamanoBytes = 10,
        HashSha256 = new string('a', 64),
        UltimaMigracion = "20261013190351_AgregarRegistrosRestauracion",
        VersionApp = "1.0.0",
        RegistradaEnHistorial = true
    };

    private async Task<string> CrearCopiaAsync(string nombre = "copia.bak", byte[]? contenido = null)
    {
        var ruta = Path.Combine(_directorio, nombre);
        await File.WriteAllBytesAsync(ruta, contenido ?? [1, 2, 3]);
        return ruta;
    }

    // ───────────────────── LeerManifiestoAsync ─────────────────────

    [Fact]
    public async Task LeerManifiestoAsync_CopiaConManifiesto_DevuelveLoQueSeEscribio()
    {
        // Arrange
        var ruta = await CrearCopiaAsync();
        var escrito = Manifiesto("copia.bak");
        await _sut.EscribirManifiestoAsync(ruta, escrito);

        // Act
        var leido = await _sut.LeerManifiestoAsync(ruta);

        // Assert
        leido.Should().BeEquivalentTo(escrito);
    }

    [Fact]
    public async Task LeerManifiestoAsync_NombreDeArchivoConOtraCapitalizacion_LoAcepta()
    {
        // Arrange: Windows no distingue mayúsculas en los nombres de archivo.
        var ruta = await CrearCopiaAsync();
        await _sut.EscribirManifiestoAsync(ruta, Manifiesto("COPIA.BAK"));

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LeerManifiestoAsync_ArchivoBakInexistente_LanzaRestauracionFallida()
    {
        // Arrange
        var ruta = Path.Combine(_directorio, "no-existe.bak");

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*no se encontró*");
    }

    [Fact]
    public async Task LeerManifiestoAsync_SinManifiestoJson_LanzaRestauracionFallida()
    {
        // Arrange
        var ruta = await CrearCopiaAsync();

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*manifiesto .json*");
    }

    [Fact]
    public async Task LeerManifiestoAsync_JsonCorrupto_LanzaRestauracionFallidaConLaCausaInterna()
    {
        // Arrange
        var ruta = await CrearCopiaAsync();
        await File.WriteAllTextAsync(ArchivosBackup.RutaManifiesto(ruta), "{ esto no es json");

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        (await act.Should().ThrowAsync<RestauracionFallidaException>()).Which.InnerException.Should().NotBeNull();
    }

    [Fact]
    public async Task LeerManifiestoAsync_JsonNull_LanzaRestauracionFallida()
    {
        // Arrange
        var ruta = await CrearCopiaAsync();
        await File.WriteAllTextAsync(ArchivosBackup.RutaManifiesto(ruta), "null");

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>();
    }

    [Fact]
    public async Task LeerManifiestoAsync_ManifiestoDeOtraCopia_LanzaRestauracionFallida()
    {
        // Arrange: el .json describe "otra.bak", no el archivo que se quiere restaurar.
        var ruta = await CrearCopiaAsync();
        await _sut.EscribirManifiestoAsync(ruta, Manifiesto("otra.bak"));

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*otra copia*");
    }

    [Fact]
    public async Task LeerManifiestoAsync_ReferenciaVacia_LanzaRestauracionFallida()
    {
        await ProbarManifiestoInvalidoAsync(m => m with { Referencia = Guid.Empty }, "*referencia*");
    }

    [Fact]
    public async Task LeerManifiestoAsync_ArchivoBakEnBlanco_LanzaRestauracionFallida()
    {
        await ProbarManifiestoInvalidoAsync(m => m with { ArchivoBak = "  " }, "*nombre del archivo*");
    }

    [Fact]
    public async Task LeerManifiestoAsync_UltimaMigracionNula_LanzaRestauracionFallida()
    {
        // 'required' acepta null en el JSON: se valida de forma explícita.
        await ProbarManifiestoInvalidoAsync(m => m with { UltimaMigracion = null! }, "*incompleto*");
    }

    [Fact]
    public async Task LeerManifiestoAsync_OperadorTipoOVersionEnBlanco_LanzaRestauracionFallida()
    {
        await ProbarManifiestoInvalidoAsync(m => m with { Operador = "" }, "*incompleto*");
        await ProbarManifiestoInvalidoAsync(m => m with { Tipo = " " }, "*incompleto*");
        await ProbarManifiestoInvalidoAsync(m => m with { VersionApp = null! }, "*incompleto*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task LeerManifiestoAsync_TamanoNoPositivo_LanzaRestauracionFallida(long tamano)
    {
        await ProbarManifiestoInvalidoAsync(m => m with { TamanoBytes = tamano }, "*tamaño*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task LeerManifiestoAsync_HashQueNoEsSha256_LanzaRestauracionFallida(string hash)
    {
        await ProbarManifiestoInvalidoAsync(m => m with { HashSha256 = hash }, "*SHA-256*");
    }

    [Fact]
    public async Task LeerManifiestoAsync_JsonConUltimaMigracionNull_LanzaRestauracionFallida()
    {
        // Arrange
        var ruta = await CrearCopiaAsync();
        await _sut.EscribirManifiestoAsync(ruta, Manifiesto("copia.bak"));
        var json = (await File.ReadAllTextAsync(ArchivosBackup.RutaManifiesto(ruta)))
            .Replace("\"20261013190351_AgregarRegistrosRestauracion\"", "null", StringComparison.Ordinal);
        await File.WriteAllTextAsync(ArchivosBackup.RutaManifiesto(ruta), json);

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*incompleto*");
    }

    private async Task ProbarManifiestoInvalidoAsync(Func<ManifiestoBackupDto, ManifiestoBackupDto> corromper, string mensaje)
    {
        var ruta = await CrearCopiaAsync();
        await _sut.EscribirManifiestoAsync(ruta, corromper(Manifiesto("copia.bak")));

        var act = () => _sut.LeerManifiestoAsync(ruta);

        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage(mensaje);
    }

    [Theory]
    [InlineData(@"\\servidor\recurso\x.bak")]
    [InlineData(@"\\?\C:\x.bak")]
    [InlineData(@"\\.\C:\x.bak")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LeerManifiestoAsync_RutaNoAceptable_LanzaRestauracionFallida(string ruta)
    {
        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>();
    }

    [Fact]
    public async Task LeerManifiestoAsync_ExtensionDistintaDeBak_LanzaRestauracionFallida()
    {
        // Arrange
        var ruta = Path.Combine(_directorio, "copia.txt");
        await File.WriteAllTextAsync(ruta, "x");

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*.bak*");
    }

    [Fact]
    public async Task LeerManifiestoAsync_ExtensionBakEnMayusculas_LaAcepta()
    {
        // Arrange
        var ruta = await CrearCopiaAsync("COPIA.BAK");
        await _sut.EscribirManifiestoAsync(ruta, Manifiesto("COPIA.BAK"));

        // Act
        var act = () => _sut.LeerManifiestoAsync(ruta);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ───────────────────── CalcularHuellaAsync ─────────────────────

    [Fact]
    public async Task CalcularHuellaAsync_ArchivoExistente_DevuelveTamanoYSha256Correctos()
    {
        // Arrange
        var contenido = new byte[300_000];
        Random.Shared.NextBytes(contenido);
        var ruta = await CrearCopiaAsync(contenido: contenido);

        // Act
        var huella = await _sut.CalcularHuellaAsync(ruta);

        // Assert: el hash esperado se calcula por separado, sin pasar por la implementación.
        huella.TamanoBytes.Should().Be(contenido.Length);
        huella.HashSha256.Should().Be(Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant());
    }

    [Fact]
    public async Task CalcularHuellaAsync_ArchivoInexistente_PropagaFileNotFound()
    {
        // Act
        var act = () => _sut.CalcularHuellaAsync(Path.Combine(_directorio, "no-existe.bak"));

        // Assert: la conversión de errores de E/S la hace el servicio de Aplicación.
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task CalcularHuellaAsync_RutaUnc_LanzaRestauracionFallida()
    {
        // Act
        var act = () => _sut.CalcularHuellaAsync(@"\\servidor\recurso\x.bak");

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>();
    }
}
