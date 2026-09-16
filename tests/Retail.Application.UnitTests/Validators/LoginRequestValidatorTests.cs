using FluentAssertions;
using Retail.Application.DTOs.Auth;
using Retail.Application.Validators.Auth;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_DatosValidos_DebePasarValidacion()
    {
        // Arrange
        var dto = new LoginRequestDto
        {
            NombreUsuario = "admin",
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NombreUsuarioVacio_DebeFallarValidacion(string nombreUsuarioInvalido)
    {
        // Arrange
        var dto = new LoginRequestDto
        {
            NombreUsuario = nombreUsuarioInvalido,
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginRequestDto.NombreUsuario));
    }

    [Fact]
    public void Validate_NombreUsuarioMayorA50Caracteres_DebeFallarValidacion()
    {
        // Arrange
        var dto = new LoginRequestDto
        {
            NombreUsuario = new string('a', 51),
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginRequestDto.NombreUsuario));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_PasswordVacio_DebeFallarValidacion(string passwordInvalido)
    {
        // Arrange
        var dto = new LoginRequestDto
        {
            NombreUsuario = "operador",
            Password = passwordInvalido
        };

        // Act
        var result = _validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LoginRequestDto.Password));
    }
}
