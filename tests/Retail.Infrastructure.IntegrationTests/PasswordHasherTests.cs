using FluentAssertions;
using Retail.Infrastructure.Security;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ConPasswordValida_DebeGenerarHashBCryptValido()
    {
        // Arrange
        const string password = "PasswordSegura123!";

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$2");
    }

    [Fact]
    public void VerifyPassword_ConPasswordCorrecta_DebeRetornarTrue()
    {
        // Arrange
        const string password = "MiClaveSecreta99";
        var hash = _hasher.HashPassword(password);

        // Act
        var resultado = _hasher.VerifyPassword(password, hash);

        // Assert
        resultado.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ConPasswordIncorrecta_DebeRetornarFalse()
    {
        // Arrange
        const string passwordCorrecta = "ClaveValida123";
        const string passwordErronea = "ClaveInvalida999";
        var hash = _hasher.HashPassword(passwordCorrecta);

        // Act
        var resultado = _hasher.VerifyPassword(passwordErronea, hash);

        // Assert
        resultado.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "$2a$11$e8k...")]
    [InlineData("password", "")]
    [InlineData("password", "hash_invalido_sin_formato")]
    public void VerifyPassword_ConEntradasInvalidas_DebeRetornarFalseSinExcepcion(string password, string hash)
    {
        // Act
        var resultado = _hasher.VerifyPassword(password, hash);

        // Assert
        resultado.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HashPassword_ConPasswordVacia_DebeLanzarArgumentException(string passwordVacia)
    {
        // Act
        var act = () => _hasher.HashPassword(passwordVacia);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
