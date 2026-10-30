using System.IO;
using FluentAssertions;
using Retail.App.Services;
using Xunit;

namespace Retail.App.UnitTests.Services;

public sealed class ConfirmacionesAvisoRestauracionEnArchivoTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "retail-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _ruta;

    public ConfirmacionesAvisoRestauracionEnArchivoTests()
    {
        _ruta = Path.Combine(_carpeta, "avisos-restauracion.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact]
    public async Task EstaConfirmadaAsync_SinArchivo_DevuelveFalse()
    {
        // Arrange
        var almacen = new ConfirmacionesAvisoRestauracionEnArchivo(_ruta);

        // Act
        var confirmada = await almacen.EstaConfirmadaAsync(5, Guid.NewGuid());

        // Assert
        confirmada.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmarAsync_LuegoEstaConfirmada_DevuelveTrue()
    {
        // Arrange
        var almacen = new ConfirmacionesAvisoRestauracionEnArchivo(_ruta);
        var referencia = Guid.NewGuid();

        // Act
        await almacen.ConfirmarAsync(5, referencia);

        // Assert
        (await almacen.EstaConfirmadaAsync(5, referencia)).Should().BeTrue();
    }

    [Fact]
    public async Task EstaConfirmadaAsync_OtroOperador_DevuelveFalse()
    {
        // Arrange
        var almacen = new ConfirmacionesAvisoRestauracionEnArchivo(_ruta);
        var referencia = Guid.NewGuid();
        await almacen.ConfirmarAsync(5, referencia);

        // Act
        var confirmada = await almacen.EstaConfirmadaAsync(6, referencia);

        // Assert
        confirmada.Should().BeFalse();
    }

    [Fact]
    public async Task EstaConfirmadaAsync_OtraRestauracion_DevuelveFalse()
    {
        // Arrange
        var almacen = new ConfirmacionesAvisoRestauracionEnArchivo(_ruta);
        var anterior = Guid.NewGuid();
        var nueva = Guid.NewGuid();
        await almacen.ConfirmarAsync(5, anterior);

        // Act
        var nuevaConfirmadaAntes = await almacen.EstaConfirmadaAsync(5, nueva);
        await almacen.ConfirmarAsync(5, nueva);

        // Assert
        nuevaConfirmadaAntes.Should().BeFalse();
        (await almacen.EstaConfirmadaAsync(5, nueva)).Should().BeTrue();
        (await almacen.EstaConfirmadaAsync(5, anterior)).Should().BeFalse();
    }

    [Fact]
    public async Task EstaConfirmadaAsync_ArchivoCorrupto_DevuelveFalse()
    {
        // Arrange
        Directory.CreateDirectory(_carpeta);
        await File.WriteAllTextAsync(_ruta, "{ esto no es json");
        var almacen = new ConfirmacionesAvisoRestauracionEnArchivo(_ruta);

        // Act
        var confirmada = await almacen.EstaConfirmadaAsync(5, Guid.NewGuid());

        // Assert
        confirmada.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmarAsync_OtraInstancia_PersisteEntreInstancias()
    {
        // Arrange
        var referencia = Guid.NewGuid();
        await new ConfirmacionesAvisoRestauracionEnArchivo(_ruta).ConfirmarAsync(5, referencia);

        // Act
        var otraInstancia = new ConfirmacionesAvisoRestauracionEnArchivo(_ruta);

        // Assert
        (await otraInstancia.EstaConfirmadaAsync(5, referencia)).Should().BeTrue();
    }
}
