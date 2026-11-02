using System.IO;
using FluentAssertions;
using Retail.App.Services;
using Xunit;

namespace Retail.App.UnitTests.Services;

public sealed class PreferenciasUsuarioTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "retail-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _ruta;

    public PreferenciasUsuarioTests()
    {
        _ruta = Path.Combine(_carpeta, "preferencias.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact]
    public void Cargar_ArchivoInexistente_UsaTemaClaro()
    {
        // Arrange
        var preferencias = new PreferenciasUsuario(_ruta) { Tema = TemaVisual.Oscuro };

        // Act
        preferencias.Cargar();

        // Assert
        preferencias.Tema.Should().Be(TemaVisual.Claro);
    }

    [Fact]
    public void Guardar_LuegoCargarEnOtraInstancia_RecuperaElTema()
    {
        // Arrange
        new PreferenciasUsuario(_ruta) { Tema = TemaVisual.Oscuro }.Guardar();
        var otra = new PreferenciasUsuario(_ruta);

        // Act
        otra.Cargar();

        // Assert
        otra.Tema.Should().Be(TemaVisual.Oscuro);
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("{ \"Tema\": \"Fucsia\" }")]
    [InlineData("{ \"Tema\": 7 }")]
    [InlineData("")]
    public void Cargar_ArchivoCorruptoOValorInvalido_UsaTemaClaroSinLanzar(string contenido)
    {
        // Arrange
        Directory.CreateDirectory(_carpeta);
        File.WriteAllText(_ruta, contenido);
        var preferencias = new PreferenciasUsuario(_ruta) { Tema = TemaVisual.Oscuro };

        // Act
        var cargar = preferencias.Cargar;

        // Assert
        cargar.Should().NotThrow();
        preferencias.Tema.Should().Be(TemaVisual.Claro);
    }

    [Fact]
    public void Constructor_RutaVacia_Lanza()
    {
        // Act
        var crear = () => new PreferenciasUsuario(" ");

        // Assert
        crear.Should().Throw<ArgumentException>();
    }
}
