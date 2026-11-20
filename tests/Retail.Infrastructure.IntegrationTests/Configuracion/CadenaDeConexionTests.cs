using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Retail.Infrastructure.Configuracion;
using Retail.Infrastructure.Security;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Configuracion;

/// <summary>
/// Contraseña de SQL Server cifrada con DPAPI (MT-10) y su composición con la cadena de conexión.
/// </summary>
public class CadenaDeConexionTests
{
    private const string CadenaSinPassword =
        "Server=SERVIDOR\\SQLEXPRESS;Database=RetailDb;User Id=retail_app;TrustServerCertificate=True";

    private static IConfiguration Configuracion(params (string Clave, string? Valor)[] valores)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor)))
            .Build();
    }

    [Fact]
    public void CifrarYDescifrar_IdaYVuelta_DevuelveLaContrasenaOriginal()
    {
        var cifrada = ProtectorCredencialSql.Cifrar("Cl4ve-Ñandú!2026");

        cifrada.Should().NotContain("Cl4ve");
        ProtectorCredencialSql.Descifrar(cifrada).Should().Be("Cl4ve-Ñandú!2026");
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Descifrar_ValorCifradoConOtraEntropia_FallaConElMensajeClaro()
    {
        var conOtraEntropia = Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes("secreto"), Encoding.UTF8.GetBytes("otra.entropia"), DataProtectionScope.LocalMachine));

        var act = () => ProtectorCredencialSql.Descifrar(conOtraEntropia);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("La contraseña de la base guardada en appsettings.local.json no se puede descifrar en esta PC: vuelva a configurarla con configurar-terminal.ps1.")
            .WithInnerException<CryptographicException>();
    }

    [Fact]
    public void Descifrar_ValorQueNoEsBase64_FallaConElMensajeClaro()
    {
        var act = () => ProtectorCredencialSql.Descifrar("esto no es base64!");

        act.Should().Throw<InvalidOperationException>().WithMessage("*no se puede descifrar en esta PC*");
    }

    [Fact]
    public void Resolver_ConPasswordCifrada_AplicaLaContrasenaYApagaLaSeguridadIntegrada()
    {
        var configuracion = Configuracion(
            ("ConnectionStrings:DefaultConnection", CadenaSinPassword + ";Trusted_Connection=True"),
            (CadenaDeConexion.ClavePasswordCifrada, ProtectorCredencialSql.Cifrar("Clave-Del-Login")));

        var cadena = new SqlConnectionStringBuilder(CadenaDeConexion.Resolver(configuracion));

        cadena.Password.Should().Be("Clave-Del-Login");
        cadena.IntegratedSecurity.Should().BeFalse();
        cadena.UserID.Should().Be("retail_app");
        cadena.InitialCatalog.Should().Be("RetailDb");
    }

    [Fact]
    public void Resolver_SinPasswordCifrada_DejaLaCadenaIntacta()
    {
        var configuracion = Configuracion(("ConnectionStrings:DefaultConnection", CadenaSinPassword));

        CadenaDeConexion.Resolver(configuracion).Should().Be(CadenaSinPassword);
    }

    [Fact]
    public void Resolver_SinNingunaCadena_UsaLaPorDefectoConSeguridadIntegrada()
    {
        var cadena = new SqlConnectionStringBuilder(CadenaDeConexion.Resolver(Configuracion()));

        cadena.DataSource.Should().Be(".");
        cadena.InitialCatalog.Should().Be("RetailDb");
        cadena.IntegratedSecurity.Should().BeTrue();
    }

    [Fact]
    public void Resolver_PasswordCifradaIlegible_FallaConElMensajeClaro()
    {
        var configuracion = Configuracion(
            ("ConnectionStrings:DefaultConnection", CadenaSinPassword),
            (CadenaDeConexion.ClavePasswordCifrada, "AAAA"));

        var act = () => CadenaDeConexion.Resolver(configuracion);

        act.Should().Throw<InvalidOperationException>().WithMessage(ProtectorCredencialSql.MensajeNoSePuedeDescifrar);
    }

    [Fact]
    public void ResolverMantenimiento_SinClave_DevuelveNull()
    {
        CadenaDeConexion.ResolverMantenimiento(Configuracion()).Should().BeNull();
    }

    [Fact]
    public void ResolverMantenimiento_ConClave_DevuelveLaCadena()
    {
        var configuracion = Configuracion(("ConnectionStrings:Mantenimiento", "Server=.;Trusted_Connection=True"));

        CadenaDeConexion.ResolverMantenimiento(configuracion).Should().Be("Server=.;Trusted_Connection=True");
    }
}
