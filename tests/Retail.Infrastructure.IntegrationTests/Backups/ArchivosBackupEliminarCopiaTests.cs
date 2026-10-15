using FluentAssertions;
using Microsoft.Extensions.Options;
using Retail.Infrastructure.Backups;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Borrado de copias (RF-22): lo ejecuta la cuenta SYSTEM de la tarea programada con una ruta que sale de
/// <c>REGISTROS_BACKUP.ruta_archivo</c>, así que solo debe poder borrar un <c>.bak</c> dentro del directorio de respaldos.
/// Cada caso deja un archivo "señuelo" fuera de lo permitido y verifica que sigue en pie.
/// </summary>
public class ArchivosBackupEliminarCopiaTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"RetailEliminarTests_{Guid.NewGuid():N}");
    private readonly string _directorio;
    private readonly string _ajena;
    private readonly ArchivosBackup _sut;

    public ArchivosBackupEliminarCopiaTests()
    {
        _directorio = Path.Combine(_raiz, "Backups");
        _ajena = Path.Combine(_raiz, "Ajena");
        Directory.CreateDirectory(_directorio);
        Directory.CreateDirectory(_ajena);
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

    private static string CrearArchivo(string carpeta, string nombre)
    {
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, nombre);
        File.WriteAllText(ruta, "contenido");
        return ruta;
    }

    [Fact]
    public void EliminarCopia_BakYManifiestoDentroDelDirectorio_BorraAmbos()
    {
        // Arrange
        var bak = CrearArchivo(_directorio, "Retail_20261013_210000_Automatico.bak");
        var manifiesto = CrearArchivo(_directorio, "Retail_20261013_210000_Automatico.json");

        // Act
        _sut.EliminarCopia(bak);

        // Assert
        File.Exists(bak).Should().BeFalse();
        File.Exists(manifiesto).Should().BeFalse();
    }

    [Fact]
    public void EliminarCopia_ArchivoInexistenteDentroDelDirectorio_NoLanza()
    {
        // Una copia que falló antes de crearse se "elimina" igual.
        var act = () => _sut.EliminarCopia(Path.Combine(_directorio, "nunca-existio.bak"));

        act.Should().NotThrow();
    }

    [Fact]
    public void EliminarCopia_ExtensionEnMayusculas_BorraLaCopia()
    {
        var bak = CrearArchivo(_directorio, "COPIA.BAK");

        _sut.EliminarCopia(bak);

        File.Exists(bak).Should().BeFalse();
    }

    [Fact]
    public void EliminarCopia_ArchivoFueraDelDirectorio_LanzaYNoLoBorra()
    {
        // Arrange: el señuelo imita un archivo del sistema al que apunta un registro adulterado.
        var senuelo = CrearArchivo(_ajena, "sistema.bak");
        var manifiestoSenuelo = CrearArchivo(_ajena, "sistema.json");

        // Act
        var act = () => _sut.EliminarCopia(senuelo);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>().WithMessage("*directorio de respaldos*");
        File.Exists(senuelo).Should().BeTrue();
        File.Exists(manifiestoSenuelo).Should().BeTrue();
    }

    [Fact]
    public void EliminarCopia_ArchivoEnUnaSubcarpetaDelDirectorio_LanzaYNoLoBorra()
    {
        var senuelo = CrearArchivo(Path.Combine(_directorio, "sub"), "anidada.bak");

        var act = () => _sut.EliminarCopia(senuelo);

        act.Should().Throw<UnauthorizedAccessException>();
        File.Exists(senuelo).Should().BeTrue();
    }

    [Fact]
    public void EliminarCopia_ArchivoQueNoEsBak_LanzaYNoLoBorra()
    {
        // Arrange: dentro del directorio, pero no es una copia (por ejemplo, una configuración o un manifiesto).
        var senuelo = CrearArchivo(_directorio, "configuracion.ini");

        // Act
        var act = () => _sut.EliminarCopia(senuelo);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>().WithMessage("*.bak*");
        File.Exists(senuelo).Should().BeTrue();
    }

    [Fact]
    public void EliminarCopia_RecorridoConPuntosPuntoHaciaAfuera_LanzaYNoBorra()
    {
        // Arrange: "<directorio>\..\Ajena\sistema.bak" se normaliza a un archivo fuera del directorio.
        var senuelo = CrearArchivo(_ajena, "sistema.bak");
        var recorrido = Path.Combine(_directorio, "..", "Ajena", "sistema.bak");

        // Act
        var act = () => _sut.EliminarCopia(recorrido);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>();
        File.Exists(senuelo).Should().BeTrue();
    }

    [Fact]
    public void EliminarCopia_RecorridoConPuntosPuntoQueVuelveAlDirectorio_BorraLaCopia()
    {
        // Arrange: la normalización también acepta lo legítimo: "<directorio>\sub\..\copia.bak" es "<directorio>\copia.bak".
        var bak = CrearArchivo(_directorio, "copia.bak");
        var rodeo = Path.Combine(_directorio, "sub", "..", "copia.bak");

        // Act
        _sut.EliminarCopia(rodeo);

        // Assert
        File.Exists(bak).Should().BeFalse();
    }

    [Fact]
    public void EliminarCopia_RutaVacia_LanzaSinBorrarNada()
    {
        var act = () => _sut.EliminarCopia(string.Empty);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void EliminarCopia_EnlaceSimbolicoDentroDelDirectorio_LanzaYNoTocaElDestino()
    {
        // Arrange: un enlace "copia.bak" dentro del directorio que apunta a un archivo ajeno.
        var destino = CrearArchivo(_ajena, "objetivo.bak");
        var enlace = Path.Combine(_directorio, "enlace.bak");
        try
        {
            File.CreateSymbolicLink(enlace, destino);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Crear enlaces simbólicos exige privilegio (administrador o modo desarrollador): sin él no se puede armar el
            // caso y la prueba no aplica en esta máquina. xUnit 2.9 no ofrece "omitir" en tiempo de ejecución.
            return;
        }

        // Act
        var act = () => _sut.EliminarCopia(enlace);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>().WithMessage("*enlace simbólico*");
        File.Exists(destino).Should().BeTrue();
        new FileInfo(enlace).Attributes.Should().HaveFlag(FileAttributes.ReparsePoint);
    }
}
