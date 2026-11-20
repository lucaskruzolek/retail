using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Retail.Infrastructure.Security;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Despliegue multiterminal (Track 6.7, Paquete 5): <c>scripts/configurar-terminal.ps1</c>, las funciones nuevas de
/// <c>RetailPermisos.psm1</c> y la sintaxis de todos los scripts. Cada prueba ejecuta un <c>powershell.exe</c> real (el de
/// Windows, el mismo que usa quien instala) con <c>RETAIL_CONFIG_DIR</c> apuntando a una carpeta temporal propia: nunca se toca
/// <c>%ProgramData%\Retail</c>.
/// </summary>
/// <remarks>
/// La prueba clave es el CONTRATO DE CIFRADO: lo que cifra el script con DPAPI debe descifrarlo
/// <see cref="ProtectorCredencialSql"/> de la aplicación. No hace falta ser administrador ni tener SQL Server.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class ScriptsDeDespliegueIntegracionTests : IDisposable
{
    private static readonly string[] ScriptsDelZip =
    [
        "RetailPermisos.psm1",
        "restore-retail.ps1",
        "registrar-tarea-backup.ps1",
        "crear-login-aplicacion.ps1",
        "configurar-terminal.ps1"
    ];

    private static readonly byte[] MarcaBom = [0xEF, 0xBB, 0xBF];

    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"RetailDespliegueTests_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static string CarpetaScripts()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            var candidata = Path.Combine(carpeta.FullName, "scripts", "RetailPermisos.psm1");
            if (File.Exists(candidata))
            {
                return Path.GetDirectoryName(candidata)!;
            }
        }

        throw new FileNotFoundException("No se encontró la carpeta scripts subiendo desde la carpeta de las pruebas.");
    }

    private static string Literal(string texto) => "'" + texto.Replace("'", "''", StringComparison.Ordinal) + "'";

    private string CarpetaLocal => Path.Combine(_raiz, "Retail");

    private string ArchivoLocal => Path.Combine(CarpetaLocal, "appsettings.local.json");

    private static (int Codigo, string Salida, string Error) EjecutarPowerShell(string codigo, string? carpetaConfiguracion)
    {
        var script = "$ProgressPreference = 'SilentlyContinue'; $ErrorActionPreference = 'Stop'; " + codigo;

        var proceso = new ProcessStartInfo("powershell.exe")
        {
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        if (carpetaConfiguracion is not null)
        {
            proceso.Environment["RETAIL_CONFIG_DIR"] = carpetaConfiguracion;
        }

        using var powershell = Process.Start(proceso)!;
        var salida = powershell.StandardOutput.ReadToEndAsync();
        var error = powershell.StandardError.ReadToEndAsync();
        powershell.WaitForExit();
        return (powershell.ExitCode, salida.Result, error.Result);
    }

    private (int Codigo, string Salida, string Error) ConfigurarTerminal(string argumentos, string password = "Clave-Retail-2026!")
    {
        var script = Path.Combine(CarpetaScripts(), "configurar-terminal.ps1");
        return EjecutarPowerShell(
            $"& {Literal(script)} {argumentos} -Password (ConvertTo-SecureString {Literal(password)} -AsPlainText -Force)",
            CarpetaLocal);
    }

    private JsonDocument LeerArchivoLocal()
    {
        var bytes = File.ReadAllBytes(ArchivoLocal);
        bytes.Take(3).Should().NotBeEquivalentTo(MarcaBom, "el archivo se escribe en UTF-8 sin BOM");
        return JsonDocument.Parse(bytes);
    }

    [Fact]
    public void ConfigurarTerminal_ArchivoNuevo_EscribeLasClavesYLaPasswordSeDescifraEnLaAplicacion()
    {
        // Act
        var (codigo, salida, error) = ConfigurarTerminal(@"-Servidor 'PCSERVIDOR\SQLEXPRESS'");

        // Assert
        codigo.Should().Be(0, $"Salida: {salida}{Environment.NewLine}Error: {error}");
        salida.Should().NotContain("Clave-Retail-2026!", "la contraseña nunca se imprime");

        using var documento = LeerArchivoLocal();
        var raiz = documento.RootElement;
        raiz.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString().Should().Be(
            @"Server=PCSERVIDOR\SQLEXPRESS;Database=RetailDb;User Id=retail_app;TrustServerCertificate=True;MultipleActiveResultSets=true");
        raiz.GetProperty("ConnectionStrings").TryGetProperty("Mantenimiento", out _).Should().BeFalse("solo la pide el servidor");
        raiz.GetProperty("ArcaSettings").GetProperty("BaseUrl").GetString().Should().Be("http://PCSERVIDOR:8080");

        var cifrada = raiz.GetProperty("BaseDeDatos").GetProperty("PasswordCifrada").GetString()!;
        ProtectorCredencialSql.Descifrar(cifrada).Should().Be("Clave-Retail-2026!", "el script y la aplicación comparten el contrato de DPAPI");
    }

    [Fact]
    public void ConfigurarTerminal_ArchivoNuevo_UsaDosEspaciosDeSangriaYNoEscapaLasComillasSimples()
    {
        // Act
        var (codigo, salida, error) = ConfigurarTerminal(@"-Servidor 'PCSERVIDOR\SQLEXPRESS' -UrlFiscal 'http://PCSERVIDOR:8080'");

        // Assert
        codigo.Should().Be(0, $"Salida: {salida}{Environment.NewLine}Error: {error}");
        var texto = File.ReadAllText(ArchivoLocal);
        texto.Should().Contain("\n  \"ConnectionStrings\": {").And.Contain("\n    \"DefaultConnection\": \"Server=");
        texto.Should().NotContain("\\u0027").And.NotContain("\t");
    }

    [Fact]
    public void ConfigurarTerminal_ArchivoExistente_ConservaLasOtrasClaves()
    {
        // Arrange
        Directory.CreateDirectory(CarpetaLocal);
        File.WriteAllText(
            ArchivoLocal,
            """
            {
              "ConnectionStrings": {
                "DefaultConnection": "Server=VIEJO;Database=Otra;User Id=otro",
                "Mantenimiento": "Server=.\\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True"
              },
              "Terminal": { "Nombre": "Caja 2", "Puertos": [1, 2] },
              "ArcaSettings": { "BaseUrl": "http://viejo:1", "Otra": true }
            }
            """);

        // Act
        var (codigo, salida, error) = ConfigurarTerminal(@"-Servidor 'PCSERVIDOR\SQLEXPRESS'");

        // Assert
        codigo.Should().Be(0, $"Salida: {salida}{Environment.NewLine}Error: {error}");
        using var documento = LeerArchivoLocal();
        var raiz = documento.RootElement;
        raiz.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString().Should().Contain("Server=PCSERVIDOR");
        raiz.GetProperty("ConnectionStrings").GetProperty("Mantenimiento").GetString().Should().Contain("Trusted_Connection=True");
        raiz.GetProperty("Terminal").GetProperty("Nombre").GetString().Should().Be("Caja 2");
        raiz.GetProperty("Terminal").GetProperty("Puertos").GetArrayLength().Should().Be(2);
        raiz.GetProperty("ArcaSettings").GetProperty("Otra").GetBoolean().Should().BeTrue();
        raiz.GetProperty("ArcaSettings").GetProperty("BaseUrl").GetString().Should().Be("http://PCSERVIDOR:8080");
    }

    [Fact]
    public void ConfigurarTerminal_ConexionMantenimientoEnElServidor_AgregaLaConexionLocalConWindows()
    {
        // Act
        var (codigo, salida, error) = ConfigurarTerminal($@"-Servidor '{Environment.MachineName}\SQLEXPRESS' -ConexionMantenimiento");

        // Assert
        codigo.Should().Be(0, $"Salida: {salida}{Environment.NewLine}Error: {error}");
        using var documento = LeerArchivoLocal();
        documento.RootElement.GetProperty("ConnectionStrings").GetProperty("Mantenimiento").GetString()
            .Should().Be(@"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True");
    }

    [Fact]
    public void ConfigurarTerminal_ConexionMantenimientoEnOtraPc_SeNiegaYNoEscribeNada()
    {
        // Act
        var (codigo, _, error) = ConfigurarTerminal(@"-Servidor 'OTRA-PC\SQLEXPRESS' -ConexionMantenimiento");

        // Assert
        codigo.Should().NotBe(0);
        error.Should().Contain("solo se usa en la PC servidor");
        File.Exists(ArchivoLocal).Should().BeFalse();
    }

    [Theory]
    [InlineData("a;b")]
    [InlineData("a=b")]
    public void ConfigurarTerminal_ServidorConCaracteresPeligrosos_SeRechaza(string servidor)
    {
        // Act
        var (codigo, _, error) = ConfigurarTerminal($"-Servidor {Literal(servidor)}");

        // Assert
        codigo.Should().NotBe(0);
        error.Should().Contain("no es valido");
        File.Exists(ArchivoLocal).Should().BeFalse();
    }

    [Fact]
    public void ReadConfiguracionDeCli_ConArchivoLocal_ElLocalGanaYMantenimientoDefineLaInstancia()
    {
        // Arrange
        var instalacion = Directory.CreateDirectory(Path.Combine(_raiz, "App")).FullName;
        File.WriteAllText(
            Path.Combine(instalacion, "appsettings.json"),
            """
            {
              "ConnectionStrings": { "DefaultConnection": "Server=.\\SQLEXPRESS;Database=RetailDb;Trusted_Connection=True" },
              "BackupSettings": { "Directorio": "D:\\Respaldos" }
            }
            """);
        Directory.CreateDirectory(CarpetaLocal);
        File.WriteAllText(
            ArchivoLocal,
            """
            {
              "ConnectionStrings": {
                "DefaultConnection": "Server=PCSERVIDOR\\SQLEXPRESS;Database=RetailLibreria;User Id=retail_app",
                "Mantenimiento": "Server=.\\OTRA;Trusted_Connection=True;TrustServerCertificate=True"
              }
            }
            """);
        var modulo = Path.Combine(CarpetaScripts(), "RetailPermisos.psm1");

        // Act
        var (codigo, salida, error) = EjecutarPowerShell(
            $"Import-Module {Literal(modulo)} -Force; Read-ConfiguracionDeCli -CarpetaCli {Literal(instalacion)} | ConvertTo-Json -Compress",
            CarpetaLocal);

        // Assert
        codigo.Should().Be(0, $"Salida: {salida}{Environment.NewLine}Error: {error}");
        using var documento = JsonDocument.Parse(salida);
        documento.RootElement.GetProperty("Instancia").GetString().Should().Be(@".\OTRA", "la conexión de mantenimiento es la que usan los scripts");
        documento.RootElement.GetProperty("BaseDeDatos").GetString().Should().Be("RetailLibreria", "el archivo local pisa al del ZIP");
        documento.RootElement.GetProperty("Directorio").GetString().Should().Be(@"D:\Respaldos");
        documento.RootElement.GetProperty("Mantenimiento").GetString().Should().Contain("Trusted_Connection=True");
    }

    [Fact]
    public void Scripts_LosQueViajanEnElZip_NoTienenErroresDeSintaxisEnWindowsPowerShell()
    {
        // Arrange: los que copia release.yml junto a los ejecutables; los corre quien instala, con Windows PowerShell 5.1
        var carpeta = CarpetaScripts();
        var archivos = ScriptsDelZip.Select(nombre => Path.Combine(carpeta, nombre)).ToList();
        archivos.Should().OnlyContain(a => File.Exists(a));

        var comprobaciones = string.Join(
            "; ",
            archivos.Select(a =>
                $"$e = $null; [void][System.Management.Automation.Language.Parser]::ParseFile({Literal(a)}, [ref]$null, [ref]$e); " +
                $"foreach ($x in $e) {{ Write-Output ({Literal(Path.GetFileName(a))} + ': ' + $x.Message + ' (linea ' + $x.Extent.StartLineNumber + ')') }}"));

        // Act
        var (codigo, salida, error) = EjecutarPowerShell(comprobaciones, carpetaConfiguracion: null);

        // Assert
        codigo.Should().Be(0, error);
        salida.Trim().Should().BeEmpty("ningún script debe tener errores de sintaxis");
    }
}
