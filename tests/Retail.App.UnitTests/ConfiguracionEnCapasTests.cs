using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Retail.Infrastructure.Configuracion;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// Precedencia real de la configuración (MT-7) con el mismo host que usa la aplicación: appsettings.json &lt;
/// appsettings.local.json &lt; variables de entorno.
/// </summary>
public sealed class ConfiguracionEnCapasTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "RetailCapas_" + Guid.NewGuid().ToString("N"));
    private readonly string _prefijo = "RETAILCAPAS" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    public ConfiguracionEnCapasTests()
    {
        Directory.CreateDirectory(Path.Combine(_raiz, "app"));
        Directory.CreateDirectory(Path.Combine(_raiz, "pc"));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_prefijo + "__C", null);
        Directory.Delete(_raiz, recursive: true);
    }

    [Fact]
    public void AgregarConfiguracionLocal_ConElHostDeLaAplicacion_RespetaBaseMenorQueLocalMenorQueEntorno()
    {
        // Arrange: tres claves, cada una definida en una capa más.
        var clave = _prefijo;
        File.WriteAllText(
            Path.Combine(_raiz, "app", "appsettings.json"),
            $$"""{ "{{clave}}": { "A": "base", "B": "base", "C": "base" } }""");
        File.WriteAllText(
            Path.Combine(_raiz, "pc", ConfiguracionLocal.NombreArchivo),
            $$"""{ "{{clave}}": { "B": "local", "C": "local" } }""");
        Environment.SetEnvironmentVariable(_prefijo + "__C", "entorno");

        // Act: mismo armado que App.xaml.cs (CreateDefaultBuilder + AgregarConfiguracionLocal).
        using var host = Host.CreateDefaultBuilder()
            .UseContentRoot(Path.Combine(_raiz, "app"))
            .ConfigureAppConfiguration((_, configuracion) => configuracion.AgregarConfiguracionLocal(Path.Combine(_raiz, "pc")))
            .Build();
        var config = host.Services.GetRequiredService<IConfiguration>();

        // Assert
        config[$"{clave}:A"].Should().Be("base");
        config[$"{clave}:B"].Should().Be("local");
        config[$"{clave}:C"].Should().Be("entorno");
    }
}
