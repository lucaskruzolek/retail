using FluentAssertions;
using Retail.App.ViewModels.Usuarios;
using Retail.Application.DTOs.Usuarios;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class UsuarioFormViewModelTests
{
    private readonly UsuarioFormViewModel _sut = new();

    [Fact]
    public void NombreUsuario_ConEspacios_RemueveEspaciosAutomaticamente()
    {
        // Act
        _sut.NombreUsuario = "lucas perez ";

        // Assert
        _sut.NombreUsuario.Should().Be("lucasperez");
    }

    [Fact]
    public void Validar_NombreUsuarioInvalidoConCaracteresEspeciales_FallaValidacion()
    {
        // Arrange
        _sut.ConfigurarAlta();
        _sut.NombreUsuario = "lucas@perez";
        _sut.NombreCompleto = "Lucas Perez";
        _sut.Password = "password123";

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("letras, números, puntos o guiones bajos");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    public void Validar_NombreUsuarioMuyCorto_FallaValidacion(string usuarioCorto)
    {
        // Arrange
        _sut.ConfigurarAlta();
        _sut.NombreUsuario = usuarioCorto;
        _sut.NombreCompleto = "Lucas Perez";
        _sut.Password = "password123";

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("al menos 3 caracteres");
    }

    [Fact]
    public void Validar_NombreUsuarioMuyLargo_FallaValidacion()
    {
        // Arrange
        _sut.ConfigurarAlta();
        _sut.NombreUsuario = new string('a', 51);
        _sut.NombreCompleto = "Lucas Perez";
        _sut.Password = "password123";

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("50 caracteres");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Validar_NombreCompletoInvalido_FallaValidacion(string nombreCompletoInvalido)
    {
        // Arrange
        _sut.ConfigurarAlta();
        _sut.NombreUsuario = "lucas";
        _sut.NombreCompleto = nombreCompletoInvalido;
        _sut.Password = "password123";

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("al menos 3 caracteres");
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    public void Validar_PasswordMuyCortaEnAlta_FallaValidacion(string passwordCorta)
    {
        // Arrange
        _sut.ConfigurarAlta();
        _sut.NombreUsuario = "lucas";
        _sut.NombreCompleto = "Lucas Perez";
        _sut.Password = passwordCorta;

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeFalse();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("al menos 6 caracteres");
    }

    [Fact]
    public void Validar_ModoEdicion_NoExigePassword()
    {
        // Arrange
        var usuario = new UsuarioDto
        {
            IdUsuario = 1,
            NombreUsuario = "lucas",
            NombreCompleto = "Lucas Perez",
            Rol = RolUsuarioEnum.Cajero,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        _sut.ConfigurarEdicion(usuario);

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
        _sut.MensajeError.Should().BeNull();
    }

    [Fact]
    public void Validar_DatosValidos_RetornaTrueYSinError()
    {
        // Arrange
        _sut.ConfigurarAlta();
        _sut.NombreUsuario = "lucas.perez_99";
        _sut.NombreCompleto = "Lucas Perez";
        _sut.Password = "PasswordSegura123";
        _sut.Rol = RolUsuarioEnum.Encargado;

        // Act
        var esValido = _sut.Validar();

        // Assert
        esValido.Should().BeTrue();
        _sut.TieneError.Should().BeFalse();
        _sut.MensajeError.Should().BeNull();

        var dto = _sut.ObtenerCrearDto();
        dto.NombreUsuario.Should().Be("lucas.perez_99");
        dto.NombreCompleto.Should().Be("Lucas Perez");
        dto.Password.Should().Be("PasswordSegura123");
        dto.Rol.Should().Be(RolUsuarioEnum.Encargado);
    }
}
