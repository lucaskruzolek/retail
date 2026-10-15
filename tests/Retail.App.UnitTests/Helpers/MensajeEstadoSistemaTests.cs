using FluentAssertions;
using Retail.App.Helpers;
using Retail.Application.DTOs.Sistema;
using Xunit;

namespace Retail.App.UnitTests.Helpers;

public class MensajeEstadoSistemaTests
{
    private static EstadoSistemaDto Estado(
        bool conecta = true,
        string[]? pendientes = null,
        string[]? desconocidas = null,
        bool hayGerente = true,
        string[]? sinEmail = null) => new()
        {
            Esquema = new EstadoEsquemaDto
            {
                PuedeConectar = conecta,
                MigracionesPendientes = pendientes ?? [],
                MigracionesDesconocidas = desconocidas ?? []
            },
            ExisteGerenteActivo = hayGerente,
            GerentesSinEmail = sinEmail ?? []
        };

    [Fact]
    public void Describir_SinConexion_BloqueaYPideMigrarYLuegoInicializar()
    {
        var diagnostico = MensajeEstadoSistema.Describir(Estado(conecta: false));

        diagnostico.Bloquea.Should().BeTrue();
        diagnostico.Mensaje.Should().Contain("No se pudo conectar").And.NotContain("dotnet run");
        diagnostico.Argumentos.Should().Equal("migrar", "inicializar");
    }

    [Fact]
    public void Describir_MigracionesPendientes_BloqueaYPideMigrar()
    {
        var diagnostico = MensajeEstadoSistema.Describir(Estado(pendientes: ["M1", "M2"]));

        diagnostico.Bloquea.Should().BeTrue();
        diagnostico.Mensaje.Should().Contain("2 migraciones pendientes").And.NotContain("dotnet run");
        diagnostico.Argumentos.Should().Equal("migrar");
    }

    [Fact]
    public void Describir_BaseMasNuevaQueLaApp_BloqueaPideActualizarLaAppYNoOfreceComandos()
    {
        // Arrange: también hay pendientes, pero migrar no resolvería una base de una versión posterior.
        var diagnostico = MensajeEstadoSistema.Describir(Estado(pendientes: ["M1"], desconocidas: ["20991231_Futura"]));

        // Assert
        diagnostico.Bloquea.Should().BeTrue();
        diagnostico.Mensaje.Should().Contain("más nueva").And.Contain("20991231_Futura").And.Contain("Actualice la aplicación");
        diagnostico.Argumentos.Should().BeEmpty();
    }

    [Fact]
    public void Describir_SinGerente_BloqueaYPideInicializar()
    {
        var diagnostico = MensajeEstadoSistema.Describir(Estado(hayGerente: false));

        diagnostico.Bloquea.Should().BeTrue();
        diagnostico.Mensaje.Should().Contain("Gerente").And.NotContain("dotnet run");
        diagnostico.Argumentos.Should().Equal("inicializar");
    }

    [Fact]
    public void Describir_GerentesSinEmail_AvisaSinBloquearNiOfrecerComandos()
    {
        var diagnostico = MensajeEstadoSistema.Describir(Estado(sinEmail: ["admin"]));

        diagnostico.Bloquea.Should().BeFalse();
        diagnostico.Mensaje.Should().Contain("admin").And.Contain("Usuarios");
        diagnostico.Argumentos.Should().BeEmpty();
    }

    [Fact]
    public void Describir_TodoEnOrden_NoBloqueaNiMuestraMensaje()
    {
        var diagnostico = MensajeEstadoSistema.Describir(Estado());

        diagnostico.Bloquea.Should().BeFalse();
        diagnostico.Mensaje.Should().BeNull();
        diagnostico.Argumentos.Should().BeEmpty();
    }
}
