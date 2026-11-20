using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Retail.Infrastructure.Configuracion;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Configuracion;

/// <summary>
/// Configuración en capas (MT-7): appsettings.json &lt; appsettings.local.json &lt; variables de entorno.
/// </summary>
[Collection("VariablesDeEntorno")]
public sealed class ConfiguracionLocalTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "RetailCfg_" + Guid.NewGuid().ToString("N"));

    public ConfiguracionLocalTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    public void Dispose()
    {
        Directory.Delete(_carpeta, recursive: true);
    }

    private string EscribirBase(string json)
    {
        var ruta = Path.Combine(_carpeta, "appsettings.json");
        File.WriteAllText(ruta, json);
        return ruta;
    }

    private void EscribirLocal(string json)
    {
        File.WriteAllText(Path.Combine(_carpeta, ConfiguracionLocal.NombreArchivo), json);
    }

    private IConfigurationRoot Armar(string rutaBase, Dictionary<string, string?>? entorno = null)
    {
        // Mismo orden que los hosts: JSON de la aplicación, después la fuente más prioritaria (acá, en memoria, en lugar de
        // las variables de entorno: la prueba con el host real está en Retail.App.UnitTests); la local se inserta en su lugar.
        return new ConfigurationBuilder()
            .AddJsonFile(rutaBase, optional: false)
            .AddInMemoryCollection(entorno)
            .AgregarConfiguracionLocal(_carpeta)
            .Build();
    }

    [Fact]
    public void AgregarConfiguracionLocal_ArchivoLocalDefineUnaClave_PisaElValorDelArchivoBase()
    {
        var rutaBase = EscribirBase("""{ "Seccion": { "A": "base", "B": "base" } }""");
        EscribirLocal("""{ "Seccion": { "A": "local" } }""");

        var configuracion = Armar(rutaBase);

        configuracion["Seccion:A"].Should().Be("local");
        configuracion["Seccion:B"].Should().Be("base");
    }

    [Fact]
    public void AgregarConfiguracionLocal_FuenteMasPrioritaria_GanaSobreElArchivoLocal()
    {
        var rutaBase = EscribirBase("""{ "Seccion": { "A": "base" } }""");
        EscribirLocal("""{ "Seccion": { "A": "local" } }""");

        var configuracion = Armar(rutaBase, new Dictionary<string, string?> { ["Seccion:A"] = "entorno" });

        configuracion["Seccion:A"].Should().Be("entorno");
    }

    [Fact]
    public void AgregarConfiguracionLocal_SinArchivoLocal_NoFallaYQuedaElValorBase()
    {
        var rutaBase = EscribirBase("""{ "Seccion": { "A": "base" } }""");

        var configuracion = Armar(rutaBase);

        configuracion["Seccion:A"].Should().Be("base");
    }

    [Fact]
    public void AgregarConfiguracionLocal_CarpetaInexistente_NoFalla()
    {
        var rutaBase = EscribirBase("""{ "Seccion": { "A": "base" } }""");

        var configuracion = new ConfigurationBuilder()
            .AddJsonFile(rutaBase, optional: false)
            .AgregarConfiguracionLocal(Path.Combine(_carpeta, "no", "existe"))
            .Build();

        configuracion["Seccion:A"].Should().Be("base");
    }

    [Fact]
    public void ObtenerCarpeta_ConVariableRetailConfigDir_UsaEsaCarpeta()
    {
        var anterior = Environment.GetEnvironmentVariable(ConfiguracionLocal.VariableCarpeta);
        Environment.SetEnvironmentVariable(ConfiguracionLocal.VariableCarpeta, _carpeta);

        try
        {
            ConfiguracionLocal.ObtenerCarpeta().Should().Be(_carpeta);
            ConfiguracionLocal.ObtenerRutaArchivo().Should().Be(Path.Combine(_carpeta, "appsettings.local.json"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfiguracionLocal.VariableCarpeta, anterior);
        }
    }

    [Fact]
    public void ObtenerCarpeta_SinVariable_UsaProgramDataRetail()
    {
        var anterior = Environment.GetEnvironmentVariable(ConfiguracionLocal.VariableCarpeta);
        Environment.SetEnvironmentVariable(ConfiguracionLocal.VariableCarpeta, null);

        try
        {
            var esperada = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Retail");
            ConfiguracionLocal.ObtenerCarpeta().Should().Be(esperada);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfiguracionLocal.VariableCarpeta, anterior);
        }
    }
}

/// <summary>
/// Las pruebas que cambian variables de entorno del proceso no corren en paralelo con otras del mismo grupo.
/// </summary>
[CollectionDefinition("VariablesDeEntorno", DisableParallelization = true)]
public sealed class VariablesDeEntornoColeccion
{
}
