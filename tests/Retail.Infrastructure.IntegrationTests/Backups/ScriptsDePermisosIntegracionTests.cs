using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Fase 7: el módulo <c>scripts/RetailPermisos.psm1</c> que usa <c>registrar-tarea-backup.ps1</c>. Cada prueba lo importa en un
/// <c>powershell.exe</c> real, lo aplica a carpetas temporales y lee la lista de acceso (ACL) resultante desde C#.
/// </summary>
/// <remarks>
/// No hace falta ser administrador: quien crea una carpeta es su propietario y puede cambiar su lista de acceso. Todo ocurre
/// bajo una carpeta temporal propia; nunca se toca <c>C:\RetailData</c> ni <c>Program Files</c>.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class ScriptsDePermisosIntegracionTests : IDisposable
{
    private const string SidTodos = "S-1-1-0";
    private const string SidUsuariosAutenticados = "S-1-5-11";
    private const string SidUsuarios = "S-1-5-32-545";
    private const string SidAdministradores = "S-1-5-32-544";
    private const string SidSistema = "S-1-5-18";

    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"RetailAclTests_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static string RutaDelModulo()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            var candidata = Path.Combine(carpeta.FullName, "scripts", "RetailPermisos.psm1");
            if (File.Exists(candidata))
            {
                return candidata;
            }
        }

        throw new FileNotFoundException("No se encontró scripts/RetailPermisos.psm1 subiendo desde la carpeta de las pruebas.");
    }

    private static string Literal(string texto) => "'" + texto.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// Ejecuta el código en un powershell.exe nuevo (el de Windows, el mismo que usa el administrador) con el módulo ya
    /// importado, y devuelve su salida estándar. Falla con el detalle si el proceso termina con error.
    /// </summary>
    private static string EjecutarConElModulo(string codigo)
    {
        var script =
            "$ProgressPreference = 'SilentlyContinue'; $ErrorActionPreference = 'Stop'; " +
            $"Import-Module {Literal(RutaDelModulo())} -Force; {codigo}";

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

        using var powershell = Process.Start(proceso)!;
        var salida = powershell.StandardOutput.ReadToEndAsync();
        var error = powershell.StandardError.ReadToEndAsync();
        powershell.WaitForExit();

        powershell.ExitCode.Should().Be(0, $"powershell.exe falló.{Environment.NewLine}Salida: {salida.Result}{Environment.NewLine}Error: {error.Result}");
        return salida.Result;
    }

    /// <summary>
    /// Carpeta padre que da "Usuarios autenticados" Modificar por HERENCIA, como C:\ en una instalación de Windows, y una
    /// carpeta hija creada debajo: hereda ese permiso.
    /// </summary>
    private string CrearHijaQueHeredaUsuariosAutenticados()
    {
        var padre = Directory.CreateDirectory(Path.Combine(_raiz, "padre"));
        var seguridad = padre.GetAccessControl();
        seguridad.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(SidUsuariosAutenticados),
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        padre.SetAccessControl(seguridad);

        var hija = Directory.CreateDirectory(Path.Combine(padre.FullName, "Backups"));
        var heredadas = SidsConPermiso(hija.FullName);
        heredadas.Should().Contain(SidUsuariosAutenticados, "la precondición de la prueba es la situación real: la hija hereda el permiso amplio");
        return hija.FullName;
    }

    /// <summary>
    /// Lee la lista de acceso por nombre de ruta, sin abrir la carpeta: una carpeta solo para Administradores y SYSTEM no se
    /// puede abrir con la cuenta de las pruebas (que no es administradora), pero su propietario sí puede leer su lista.
    /// </summary>
    private static DirectorySecurity Seguridad(string ruta) => new(ruta, AccessControlSections.Access);

    private static List<FileSystemAccessRule> Reglas(string ruta)
    {
        return Seguridad(ruta)
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(r => r.AccessControlType == AccessControlType.Allow)
            .ToList();
    }

    private static HashSet<string> SidsConPermiso(string ruta) => Reglas(ruta).Select(r => r.IdentityReference.Value).ToHashSet();

    private static string CuentaDeLaApp => WindowsIdentity.GetCurrent().Name;

    private static string SidDeLaApp => WindowsIdentity.GetCurrent().User!.Value;

    private static string SidDelMotor
    {
        get
        {
            var cuenta = ServidorDePruebas.ObtenerCuentaDelServicioDelMotor();
            return new NTAccount(cuenta).Translate(typeof(SecurityIdentifier)).Value;
        }
    }

    private static void AplicarPermisosDeRespaldos(string carpeta)
    {
        var cuentaMotor = ServidorDePruebas.ObtenerCuentaDelServicioDelMotor();
        EjecutarConElModulo(
            $"Set-PermisoDeCarpeta -Ruta {Literal(carpeta)} -CuentasModificar @({Literal(cuentaMotor)}, {Literal("S-1-5-18")}, {Literal(CuentaDeLaApp)})");
    }

    [Fact]
    public void SetPermisoDeCarpeta_ConPermisoAmplioHeredado_LoQuitaYDejaSoloLosCuatroPrincipales()
    {
        // Arrange: el caso que fallaba en la primera ejecución real: "Usuarios autenticados (M)" y "Usuarios (RX)" heredados
        // de la carpeta padre quedaban como reglas explícitas de la carpeta de respaldos.
        var carpeta = CrearHijaQueHeredaUsuariosAutenticados();

        // Act
        AplicarPermisosDeRespaldos(carpeta);

        // Assert
        var sids = SidsConPermiso(carpeta);
        sids.Should().NotContain([SidUsuariosAutenticados, SidUsuarios, SidTodos]);
        sids.Should().Contain([SidAdministradores, SidSistema, SidDelMotor, SidDeLaApp]);

        Seguridad(carpeta).AreAccessRulesProtected
            .Should().BeTrue("la carpeta ya no hereda de su padre");
        Reglas(carpeta).Should().OnlyContain(r => !r.IsInherited, "no queda ninguna regla heredada: todas son propias y explícitas");
    }

    [Fact]
    public void SetPermisoDeCarpeta_ConLosPermisosDeCadaPrincipal_AdministradoresYSistemaControlTotalYElRestoModificar()
    {
        // Arrange
        var carpeta = CrearHijaQueHeredaUsuariosAutenticados();

        // Act
        AplicarPermisosDeRespaldos(carpeta);

        // Assert
        var reglas = Reglas(carpeta);
        reglas.Single(r => r.IdentityReference.Value == SidAdministradores).FileSystemRights.Should().HaveFlag(FileSystemRights.FullControl);
        reglas.Single(r => r.IdentityReference.Value == SidSistema).FileSystemRights.Should().HaveFlag(FileSystemRights.FullControl);

        foreach (var sid in new[] { SidDelMotor, SidDeLaApp })
        {
            var derechos = reglas.Single(r => r.IdentityReference.Value == sid).FileSystemRights;
            derechos.Should().HaveFlag(FileSystemRights.Modify);
            derechos.HasFlag(FileSystemRights.FullControl).Should().BeFalse("el motor y la app modifican, no administran permisos");
        }

        reglas.Should().OnlyContain(r => r.InheritanceFlags == (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit),
            "los archivos nuevos (los .bak) heredan estos permisos");
    }

    [Fact]
    public void SetPermisoDeCarpeta_ConReglasExplicitasDeUnaEjecucionAnteriorConError_LasPurga()
    {
        // Arrange: lo que dejó la versión con el defecto: reglas explícitas de los grupos amplios. Además se ejecuta dos veces.
        var carpeta = CrearHijaQueHeredaUsuariosAutenticados();
        var seguridad = new DirectoryInfo(carpeta).GetAccessControl();
        foreach (var sid in new[] { SidTodos, SidUsuariosAutenticados, SidUsuarios })
        {
            seguridad.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(sid),
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        new DirectoryInfo(carpeta).SetAccessControl(seguridad);

        // Act
        AplicarPermisosDeRespaldos(carpeta);
        AplicarPermisosDeRespaldos(carpeta);

        // Assert
        var sids = SidsConPermiso(carpeta);
        sids.Should().NotContain([SidTodos, SidUsuariosAutenticados, SidUsuarios]);
        sids.Should().BeEquivalentTo([SidAdministradores, SidSistema, SidDelMotor, SidDeLaApp], "es idempotente: sin reglas duplicadas ni sobrantes");
    }

    [Fact]
    public void SetPermisoDeCarpeta_SinCuentasAdicionales_DejaSoloAdministradoresYSistema()
    {
        // Arrange: una carpeta que hereda el acceso de "Usuarios autenticados" y debe quedar protegida sin cuentas adicionales.
        var carpeta = CrearHijaQueHeredaUsuariosAutenticados();

        // Act
        EjecutarConElModulo($"Set-PermisoDeCarpeta -Ruta {Literal(carpeta)} -CuentasModificar @()");

        // Assert
        try
        {
            SidsConPermiso(carpeta).Should().BeEquivalentTo([SidAdministradores, SidSistema]);
        }
        finally
        {
            // La cuenta de las pruebas no es administradora: se le devuelve el acceso solo para poder borrar la carpeta.
            var seguridad = Seguridad(carpeta);
            seguridad.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(SidDeLaApp),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            new DirectoryInfo(carpeta).SetAccessControl(seguridad);
        }
    }

    [Fact]
    public void GetEscrituraDeNoAdministradores_CarpetaQueHeredaPermisoDeEscrituraAmplio_InformaElHallazgo()
    {
        // Arrange: un archivo dentro de una carpeta que los usuarios autenticados pueden modificar (el ejecutable de la tarea
        // podría reemplazarlo cualquiera: escalada de privilegios).
        var carpeta = CrearHijaQueHeredaUsuariosAutenticados();
        File.WriteAllText(Path.Combine(carpeta, "Retail.Cli.exe"), "x");

        // Act
        var hallazgos = EjecutarConElModulo($"Get-EscrituraDeNoAdministradores -Ruta {Literal(carpeta)}");

        // Assert
        hallazgos.Should().Contain(SidUsuariosAutenticados).And.Contain("Retail.Cli.exe");
    }

    [Fact]
    public void GetEscrituraDeNoAdministradores_CarpetaProtegida_NoInformaNada()
    {
        // Arrange
        var carpeta = CrearHijaQueHeredaUsuariosAutenticados();
        File.WriteAllText(Path.Combine(carpeta, "Retail.Cli.exe"), "x");
        EjecutarConElModulo($"Set-PermisoDeCarpeta -Ruta {Literal(carpeta)} -CuentasModificar @({Literal(CuentaDeLaApp)})");

        // Act
        var hallazgos = EjecutarConElModulo($"Get-EscrituraDeNoAdministradores -Ruta {Literal(carpeta)}");

        // Assert: al cortar la herencia en la carpeta, Windows también se la quita al archivo que ya estaba dentro.
        hallazgos.Should().NotContain("Retail.Cli.exe", "la herencia amplia se cortó en la carpeta");
    }

    [Fact]
    public void GetEscrituraDeNoAdministradores_CuentaExtraConPermisoDeEscritura_InformaElHallazgo()
    {
        // Arrange: la cuenta con la que se usa Retail POS no debe poder modificar la copia del ejecutable.
        var carpeta = CrearHijaQueHeredaUsuariosAutenticados();
        EjecutarConElModulo($"Set-PermisoDeCarpeta -Ruta {Literal(carpeta)} -CuentasModificar @({Literal(CuentaDeLaApp)})");

        // Act
        var sinExtra = EjecutarConElModulo($"Get-EscrituraDeNoAdministradores -Ruta {Literal(carpeta)}");
        var conExtra = EjecutarConElModulo(
            $"Get-EscrituraDeNoAdministradores -Ruta {Literal(carpeta)} -CuentasExtra @({Literal(CuentaDeLaApp)})");

        // Assert
        sinExtra.Trim().Should().BeEmpty();
        conExtra.Should().Contain(SidDeLaApp);
    }

    /// <summary>
    /// Instalación de prueba: <c>_raiz\instalacion\app</c> protegida (herencia cortada; solo Administradores, SYSTEM y la cuenta
    /// de las pruebas, que no es administradora y no figura como sospechosa salvo que se pase como cuenta extra) con un
    /// <c>Retail.Cli.exe</c> adentro. Toda la cadena bajo <c>_raiz</c> queda protegida.
    /// </summary>
    private string CrearInstalacionProtegida()
    {
        var superior = Directory.CreateDirectory(Path.Combine(_raiz, "instalacion")).FullName;
        EjecutarConElModulo($"Set-PermisoDeCarpeta -Ruta {Literal(superior)} -CuentasModificar @({Literal(CuentaDeLaApp)})");

        var instalacion = Directory.CreateDirectory(Path.Combine(superior, "app")).FullName;
        File.WriteAllText(Path.Combine(instalacion, "Retail.Cli.exe"), "x");
        return instalacion;
    }

    [Fact]
    public void GetEscrituraEnInstalacion_CarpetaSuperiorEscribiblePorUsuariosAutenticados_InformaLaCarpetaSuperior()
    {
        // Arrange: la carpeta de la instalación está protegida, pero su padre da Modificar a "Usuarios autenticados": quien puede
        // renombrar el padre puede poner otra carpeta con otro ejecutable que la tarea (SYSTEM) ejecutaría.
        var instalacion = CrearHijaQueHeredaUsuariosAutenticados();
        File.WriteAllText(Path.Combine(instalacion, "Retail.Cli.exe"), "x");
        EjecutarConElModulo($"Set-PermisoDeCarpeta -Ruta {Literal(instalacion)} -CuentasModificar @({Literal(CuentaDeLaApp)})");
        var padre = Path.GetDirectoryName(instalacion)!;

        // Act
        var hallazgos = EjecutarConElModulo($"Get-EscrituraEnInstalacion -Ruta {Literal(instalacion)}");

        // Assert
        hallazgos.Should().Contain($"{padre}: {SidUsuariosAutenticados}");
        hallazgos.Should().NotContain($"{instalacion}:", "la propia carpeta sí está protegida: el problema es solo la superior");
    }

    [Fact]
    public void GetEscrituraEnInstalacion_CadenaProtegida_NoInformaNadaBajoLaRaizDeLaPrueba()
    {
        // Arrange
        var instalacion = CrearInstalacionProtegida();

        // Act
        var hallazgos = EjecutarConElModulo($"Get-EscrituraEnInstalacion -Ruta {Literal(instalacion)}");

        // Assert: las carpetas por encima de la raíz temporal dependen de la PC que corre la prueba y no se evalúan.
        hallazgos.Should().NotContain(_raiz);
    }

    [Fact]
    public void GetEscrituraEnInstalacion_CuentaDeLaAppConModificarEnLaInstalacion_InformaElHallazgo()
    {
        // Arrange: la cuenta con la que se usa Retail POS no debe poder modificar el ejecutable.
        var instalacion = CrearInstalacionProtegida();

        // Act
        var hallazgos = EjecutarConElModulo(
            $"Get-EscrituraEnInstalacion -Ruta {Literal(instalacion)} -CuentasExtra @({Literal(CuentaDeLaApp)})");

        // Assert: la regla de la cuenta está en la carpeta superior (heredada por la instalación y por el ejecutable).
        hallazgos.Should().Contain(SidDeLaApp).And.Contain("Retail.Cli.exe");
    }

    [Fact]
    public void GetEscrituraEnInstalacion_CarpetaLogsEscribibleParaUsuarios_LaInforma()
    {
        // Arrange: instalación protegida cuya subcarpeta logs es la única escribible por un grupo amplio. Los registros ya no
        // viven en la instalación (van a %LOCALAPPDATA%), así que "logs" no tiene ninguna excepción: SYSTEM no debe escribir
        // donde un usuario sin privilegios puede redirigir las escrituras.
        var instalacion = CrearInstalacionProtegida();
        var logs = Directory.CreateDirectory(Path.Combine(instalacion, "logs")).FullName;
        var seguridad = new DirectoryInfo(logs).GetAccessControl();
        seguridad.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(SidUsuarios),
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        new DirectoryInfo(logs).SetAccessControl(seguridad);
        File.WriteAllText(Path.Combine(logs, "retail-cli.log"), "x");

        // Act
        var hallazgos = EjecutarConElModulo($"Get-EscrituraEnInstalacion -Ruta {Literal(instalacion)}");

        // Assert
        hallazgos.Should().Contain($"{logs}: {SidUsuarios}").And.Contain("retail-cli.log");
    }

    [Fact]
    public void ReadConfiguracionDeCli_ConAppsettings_DevuelveInstanciaBaseYDirectorioExpandido()
    {
        // Arrange
        Directory.CreateDirectory(_raiz);
        File.WriteAllText(
            Path.Combine(_raiz, "appsettings.json"),
            """
            {
              "ConnectionStrings": { "DefaultConnection": "Server=PC-CAJA\\SQLEXPRESS;Database=RetailLibreria;Trusted_Connection=True;TrustServerCertificate=True" },
              "BackupSettings": { "Directorio": "%TEMP%\\RetailRespaldos" }
            }
            """);

        // Act
        var json = EjecutarConElModulo($"Read-ConfiguracionDeCli -CarpetaCli {Literal(_raiz)} | ConvertTo-Json -Compress");

        // Assert
        using var documento = JsonDocument.Parse(json);
        documento.RootElement.GetProperty("Instancia").GetString().Should().Be(@"PC-CAJA\SQLEXPRESS");
        documento.RootElement.GetProperty("BaseDeDatos").GetString().Should().Be("RetailLibreria");
        documento.RootElement.GetProperty("Directorio").GetString()
            .Should().Be(Path.Combine(Environment.GetEnvironmentVariable("TEMP")!, "RetailRespaldos"));
    }

    [Fact]
    public void ReadConfiguracionDeCli_SinAppsettings_DevuelveValoresNulos()
    {
        // Arrange
        Directory.CreateDirectory(_raiz);

        // Act
        var json = EjecutarConElModulo($"Read-ConfiguracionDeCli -CarpetaCli {Literal(_raiz)} | ConvertTo-Json -Compress");

        // Assert
        using var documento = JsonDocument.Parse(json);
        documento.RootElement.GetProperty("Instancia").ValueKind.Should().Be(JsonValueKind.Null);
        documento.RootElement.GetProperty("Directorio").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(@".\SQLEXPRESS", true)]
    [InlineData(@"(localdb)\MSSQLLocalDB", true)]
    [InlineData("localhost,1433", true)]
    [InlineData("PC-CAJA", true)]
    [InlineData(@".\SQLEXPRESS;Initial Catalog=otra", false)]
    [InlineData("a=b", false)]
    [InlineData("a'b", false)]
    [InlineData("a\"b", false)]
    [InlineData("a b", false)]
    [InlineData("", false)]
    public void TestInstanciaValida_SegunLosCaracteres_AceptaORechaza(string instancia, bool esperado)
    {
        // Act
        var salida = EjecutarConElModulo($"Test-InstanciaValida {Literal(instancia)}");

        // Assert
        salida.Trim().Should().Be(esperado ? "True" : "False");
    }
}
