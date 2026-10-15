using FluentAssertions;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Infrastructure.Backups;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// B-3: una copia elegida desde cualquier carpeta (por ejemplo, Descargas) se trae al directorio de respaldos, donde la
/// cuenta del motor sí puede leerla. Archivos reales en carpetas temporales; no interviene el motor.
/// </summary>
public class ArchivosBackupPrepararParaRestaurarTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"RetailPrepararTests_{Guid.NewGuid():N}");
    private readonly string _directorio;
    private readonly string _externa;
    private readonly ArchivosBackup _sut;

    public ArchivosBackupPrepararParaRestaurarTests()
    {
        _directorio = Path.Combine(_raiz, "Backups");
        _externa = Path.Combine(_raiz, "Descargas");
        Directory.CreateDirectory(_directorio);
        Directory.CreateDirectory(_externa);
        _sut = new ArchivosBackup(Options.Create(new BackupOptions { Directorio = _directorio }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private async Task<string> CrearCopiaAsync(string carpeta, string nombre, byte[] contenido, Guid? referencia = null)
    {
        var ruta = Path.Combine(carpeta, nombre);
        await File.WriteAllBytesAsync(ruta, contenido);
        await _sut.EscribirManifiestoAsync(ruta, new ManifiestoBackupDto
        {
            Referencia = referencia ?? Guid.NewGuid(),
            Tipo = "Manual",
            FechaInicioUtc = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
            Operador = "gerente",
            ArchivoBak = nombre,
            TamanoBytes = contenido.Length,
            HashSha256 = new string('a', 64),
            UltimaMigracion = "20261013190351_AgregarRegistrosRestauracion",
            VersionApp = "1.0.0",
            RegistradaEnHistorial = true
        });
        return ruta;
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_ArchivoYaEnElDirectorio_DevuelveLaMismaRutaSinCopiar()
    {
        // Arrange
        var ruta = await CrearCopiaAsync(_directorio, "copia.bak", [1, 2, 3]);

        // Act
        var resultado = await _sut.PrepararParaRestaurarAsync(ruta);

        // Assert
        resultado.Should().Be(ruta);
        Directory.GetFiles(_directorio).Should().HaveCount(2, "solo el .bak y su .json");
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_ArchivoYaEnElDirectorioConOtraCapitalizacion_NoLoCopia()
    {
        // Arrange
        await CrearCopiaAsync(_directorio, "copia.bak", [1, 2, 3]);
        var rutaEnMayusculas = Path.Combine(_directorio.ToUpperInvariant(), "copia.bak");

        // Act
        var resultado = await _sut.PrepararParaRestaurarAsync(rutaEnMayusculas);

        // Assert: Windows no distingue mayúsculas en las rutas.
        resultado.Should().Be(rutaEnMayusculas);
        Directory.GetFiles(_directorio).Should().HaveCount(2);
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_ArchivoFueraDelDirectorio_CopiaElBakYSuManifiesto()
    {
        // Arrange
        var referencia = Guid.NewGuid();
        var origen = await CrearCopiaAsync(_externa, "copia.bak", [1, 2, 3, 4], referencia);

        // Act
        var resultado = await _sut.PrepararParaRestaurarAsync(origen);

        // Assert
        resultado.Should().Be(Path.Combine(_directorio, "copia.bak"));
        (await File.ReadAllBytesAsync(resultado)).Should().Equal(1, 2, 3, 4);
        var manifiesto = await _sut.LeerManifiestoAsync(resultado);
        manifiesto.Referencia.Should().Be(referencia);
        File.Exists(origen).Should().BeTrue("el original no se toca");
        Directory.GetFiles(_directorio, "*.copiando").Should().BeEmpty("no quedan temporales");
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_MismoNombreYMismoContenido_ReutilizaLaCopiaExistente()
    {
        // Arrange
        var origen = await CrearCopiaAsync(_externa, "copia.bak", [1, 2, 3]);
        var primera = await _sut.PrepararParaRestaurarAsync(origen);

        // Act
        var segunda = await _sut.PrepararParaRestaurarAsync(origen);

        // Assert
        segunda.Should().Be(primera);
        Directory.GetFiles(_directorio).Should().HaveCount(2);
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_MismoNombreYContenidoDistinto_AgregaSufijoAlBakYAlManifiesto()
    {
        // Arrange: ya hay una "copia.bak" distinta en el directorio.
        await CrearCopiaAsync(_directorio, "copia.bak", [9, 9, 9]);
        var origen = await CrearCopiaAsync(_externa, "copia.bak", [1, 2, 3]);

        // Act
        var resultado = await _sut.PrepararParaRestaurarAsync(origen);

        // Assert
        resultado.Should().Be(Path.Combine(_directorio, "copia_2.bak"));
        (await File.ReadAllBytesAsync(Path.Combine(_directorio, "copia.bak"))).Should().Equal(9, 9, 9);
        (await File.ReadAllBytesAsync(resultado)).Should().Equal(1, 2, 3);
        File.Exists(Path.Combine(_directorio, "copia_2.json")).Should().BeTrue();

        // El manifiesto copiado nombra al archivo nuevo: si no, LeerManifiestoAsync lo rechazaría.
        (await _sut.LeerManifiestoAsync(resultado)).ArchivoBak.Should().Be("copia_2.bak");
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_SufijoOcupadoPorOtroContenido_ElijeElSiguiente()
    {
        // Arrange
        await CrearCopiaAsync(_directorio, "copia.bak", [9, 9, 9]);
        await CrearCopiaAsync(_directorio, "copia_2.bak", [8, 8, 8]);
        var origen = await CrearCopiaAsync(_externa, "copia.bak", [1, 2, 3]);

        // Act
        var resultado = await _sut.PrepararParaRestaurarAsync(origen);

        // Assert
        Path.GetFileName(resultado).Should().Be("copia_3.bak");
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_SinManifiesto_LanzaRestauracionFallidaYNoCopiaNada()
    {
        // Arrange
        var origen = Path.Combine(_externa, "copia.bak");
        await File.WriteAllBytesAsync(origen, [1, 2, 3]);

        // Act
        var act = () => _sut.PrepararParaRestaurarAsync(origen);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*manifiesto*");
        Directory.GetFiles(_directorio).Should().BeEmpty();
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_ArchivoInexistente_LanzaRestauracionFallida()
    {
        // Act
        var act = () => _sut.PrepararParaRestaurarAsync(Path.Combine(_externa, "no-existe.bak"));

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*No se encontró*");
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_SinEspacioLibreSuficiente_LanzaRestauracionFallidaYNoCopiaNada()
    {
        // Arrange: el margen exigido (1.000.000.000 MB) supera cualquier disco.
        var origen = await CrearCopiaAsync(_externa, "copia.bak", [1, 2, 3]);
        var sinEspacio = new ArchivosBackup(Options.Create(new BackupOptions { Directorio = _directorio, EspacioLibreMinimoMb = 1_000_000_000 }));

        // Act
        var act = () => sinEspacio.PrepararParaRestaurarAsync(origen);

        // Assert: el aviso es claro y se produce antes de escribir nada en el disco.
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*Espacio insuficiente*");
        Directory.GetFiles(_directorio).Should().BeEmpty();
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_ElDestinoYaEstaOcupadoPorOtraCosa_ConservaLaCausaOriginalYNoBorraLoAjeno()
    {
        // Arrange: "copia.bak" no existe como archivo, pero el nombre está tomado por una carpeta (algo ajeno). El Move falla
        // con una IOException; la limpieza no debe intentar borrar lo ajeno ni reemplazar esa causa por otro error.
        var origen = await CrearCopiaAsync(_externa, "copia.bak", [1, 2, 3]);
        var ajeno = Path.Combine(_directorio, "copia.bak");
        Directory.CreateDirectory(ajeno);

        // Act
        var act = () => _sut.PrepararParaRestaurarAsync(origen);

        // Assert
        var excepcion = (await act.Should().ThrowAsync<RestauracionFallidaException>()).Which;
        excepcion.InnerException.Should().BeAssignableTo<IOException>("la causa original es la falla del Move, no un error de la limpieza");
        Directory.Exists(ajeno).Should().BeTrue("lo ajeno no se toca");
        Directory.GetFiles(_directorio, "*.copiando").Should().BeEmpty();
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_FallaElManifiestoDelDestino_BorraLoQueCreoYDejaLoAjeno()
    {
        // Arrange: el .bak se copia bien, pero su manifiesto no se puede escribir (el nombre está tomado por una carpeta).
        var origen = await CrearCopiaAsync(_externa, "copia.bak", [1, 2, 3]);
        var ajeno = Path.Combine(_directorio, "copia.json");
        Directory.CreateDirectory(ajeno);

        // Act
        var act = () => _sut.PrepararParaRestaurarAsync(origen);

        // Assert: el .bak que se acababa de crear se retira (el próximo intento empieza limpio).
        await act.Should().ThrowAsync<RestauracionFallidaException>();
        File.Exists(Path.Combine(_directorio, "copia.bak")).Should().BeFalse("el .bak lo creó esta operación y quedó sin manifiesto");
        Directory.GetFiles(_directorio, "*.copiando").Should().BeEmpty();
        Directory.Exists(ajeno).Should().BeTrue("lo ajeno no se toca");
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_CanceladaMientrasCopia_NoDejaTemporalesNiElDestino()
    {
        // Arrange: un archivo grande (la copia tarda lo suficiente) que se cancela apenas aparece el temporal.
        var origen = Path.Combine(_externa, "grande.bak");
        await using (var archivo = new FileStream(origen, FileMode.Create))
        {
            archivo.SetLength(300L * 1024 * 1024);
        }

        await _sut.EscribirManifiestoAsync(origen, new ManifiestoBackupDto
        {
            Referencia = Guid.NewGuid(),
            Tipo = "Manual",
            FechaInicioUtc = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
            Operador = "gerente",
            ArchivoBak = "grande.bak",
            TamanoBytes = 300L * 1024 * 1024,
            HashSha256 = new string('a', 64),
            UltimaMigracion = "20261013190351_AgregarRegistrosRestauracion",
            VersionApp = "1.0.0",
            RegistradaEnHistorial = true
        });

        using var cancelacion = new CancellationTokenSource();
        var temporal = Path.Combine(_directorio, "grande.bak.copiando");
        var vigilante = Task.Run(async () =>
        {
            while (!File.Exists(temporal))
            {
                await Task.Delay(1);
            }

            await cancelacion.CancelAsync();
        });

        // Act
        var act = () => _sut.PrepararParaRestaurarAsync(origen, cancelacion.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await vigilante;
        File.Exists(temporal).Should().BeFalse();
        File.Exists(Path.Combine(_directorio, "grande.bak")).Should().BeFalse();
        File.Exists(Path.Combine(_directorio, "grande.json")).Should().BeFalse();
        File.Exists(origen).Should().BeTrue("el original no se toca");
    }

    [Fact]
    public async Task PrepararParaRestaurarAsync_RutaDeRed_LanzaRestauracionFallida()
    {
        // Act
        var act = () => _sut.PrepararParaRestaurarAsync(@"\\servidor\recurso\copia.bak");

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>();
    }
}
