using FluentAssertions;
using Retail.App.ViewModels.Usuarios;
using Retail.Application.DTOs.Usuarios;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class CambiarPasswordViewModelTests
{
    private readonly CambiarPasswordViewModel _sut = new();

    private static UsuarioDto Cuenta() => new()
    {
        IdUsuario = 3,
        NombreUsuario = "cajero",
        Nombre = "Carla",
        Apellido = "Díaz",
        Rol = RolUsuarioEnum.Cajero,
        Activo = true,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void ConfigurarCambioPropio_SinPasswordActual_NoPasaLaValidacion()
    {
        // Arrange
        _sut.ConfigurarCambioPropio(Cuenta());
        _sut.NuevaPassword = "Nueva2026!";
        _sut.ConfirmarPassword = "Nueva2026!";

        // Act
        var esValido = _sut.Validar();

        // Assert
        _sut.RequierePasswordActual.Should().BeTrue();
        esValido.Should().BeFalse();
        _sut.MensajeError.Should().Contain("contraseña actual");
    }

    [Fact]
    public void ObtenerDto_CambioPropio_IncluyeLaPasswordActual()
    {
        _sut.ConfigurarCambioPropio(Cuenta());
        _sut.PasswordActual = "Vieja2026!";
        _sut.NuevaPassword = "Nueva2026!";

        _sut.ObtenerDto().PasswordActual.Should().Be("Vieja2026!");
    }

    [Fact]
    public void ObtenerDto_Blanqueo_NoEnviaPasswordActual()
    {
        // Arrange: quien blanquea no conoce (ni debe conocer) la contraseña del titular.
        _sut.Configurar(Cuenta());
        _sut.PasswordActual = "no-deberia-viajar";
        _sut.NuevaPassword = "Temporal2026!";

        // Act & Assert
        _sut.RequierePasswordActual.Should().BeFalse();
        _sut.ObtenerDto().PasswordActual.Should().BeNull();
    }

    [Fact]
    public void ConfigurarCambioObligatorio_MuestraElAvisoSinPedirLaPasswordActual()
    {
        _sut.ConfigurarCambioObligatorio(3, "cajero", "Carla Díaz");

        _sut.EsCambioObligatorio.Should().BeTrue();
        _sut.RequierePasswordActual.Should().BeFalse();
        _sut.TextoBotonGuardar.Should().Be("Cambiar e Ingresar");
    }
}
