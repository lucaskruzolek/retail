using FluentAssertions;
using Retail.App.Services;
using Retail.Application.DTOs.Auth;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.Services;

public class CurrentUserSessionTests
{
    private readonly CurrentUserSession _sut = new();

    [Fact]
    public void EstadoInicial_NoDebeEstarAutenticado()
    {
        // Assert
        _sut.EstaAutenticado.Should().BeFalse();
        _sut.UsuarioActual.Should().BeNull();
        _sut.IdUsuario.Should().BeNull();
        _sut.NombreUsuario.Should().BeEmpty();
        _sut.NombreCompleto.Should().BeEmpty();
        _sut.Rol.Should().BeNull();
        _sut.EsGerente.Should().BeFalse();
        _sut.EsEncargado.Should().BeFalse();
        _sut.EsCajero.Should().BeFalse();
    }

    [Fact]
    public void EstablecerSesion_UsuarioValido_DebeActualizarPropiedadesYDispararEvento()
    {
        // Arrange
        var usuario = new LoginResultDto
        {
            IdUsuario = 10,
            NombreUsuario = "pfernandez",
            NombreCompleto = "Pablo Fernandez",
            Rol = RolUsuarioEnum.Gerente
        };

        var eventoDisparado = false;
        _sut.SessionChanged += () => eventoDisparado = true;

        // Act
        _sut.EstablecerSesion(usuario);

        // Assert
        _sut.EstaAutenticado.Should().BeTrue();
        _sut.IdUsuario.Should().Be(10);
        _sut.NombreUsuario.Should().Be("pfernandez");
        _sut.NombreCompleto.Should().Be("Pablo Fernandez");
        _sut.Rol.Should().Be(RolUsuarioEnum.Gerente);
        _sut.EsGerente.Should().BeTrue();
        _sut.EsEncargado.Should().BeFalse();
        _sut.EsCajero.Should().BeFalse();
        eventoDisparado.Should().BeTrue();
    }

    [Fact]
    public void EstablecerSesion_RolCajero_DebeActivarFlagsRBACCorrectos()
    {
        // Arrange
        var usuario = new LoginResultDto
        {
            IdUsuario = 3,
            NombreUsuario = "cajero1",
            NombreCompleto = "Operador de Caja",
            Rol = RolUsuarioEnum.Cajero
        };

        // Act
        _sut.EstablecerSesion(usuario);

        // Assert
        _sut.EsGerente.Should().BeFalse();
        _sut.EsEncargado.Should().BeFalse();
        _sut.EsCajero.Should().BeTrue();
    }

    [Fact]
    public void CerrarSesion_DebeLimpiarPropiedadesYDispararEvento()
    {
        // Arrange
        var usuario = new LoginResultDto
        {
            IdUsuario = 1,
            NombreUsuario = "admin",
            NombreCompleto = "Admin",
            Rol = RolUsuarioEnum.Gerente
        };
        _sut.EstablecerSesion(usuario);

        var contadorEventos = 0;
        _sut.SessionChanged += () => contadorEventos++;

        // Act
        _sut.CerrarSesion();

        // Assert
        _sut.EstaAutenticado.Should().BeFalse();
        _sut.UsuarioActual.Should().BeNull();
        _sut.EsGerente.Should().BeFalse();
        contadorEventos.Should().Be(1);
    }

    [Fact]
    public void EstablecerSesion_UsuarioNulo_DebeLanzarArgumentNullException()
    {
        // Act
        var act = () => _sut.EstablecerSesion(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
