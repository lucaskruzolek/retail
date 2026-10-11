using FluentAssertions;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Validators.Usuarios;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Validators;

public class UsuarioValidatorTests
{
    private readonly CrearUsuarioValidator _crearValidator = new();
    private readonly ModificarUsuarioValidator _modificarValidator = new();
    private readonly CambiarPasswordValidator _cambiarPasswordValidator = new();

    private static readonly CrearUsuarioDto AltaValida = new()
    {
        NombreUsuario = "jperez",
        Nombre = "Juan",
        Apellido = "Pérez",
        Password = "Clave2026!",
        Rol = RolUsuarioEnum.Cajero
    };

    private static readonly ModificarUsuarioDto ModificacionValida = new()
    {
        IdUsuario = 1,
        Nombre = "Juan",
        Apellido = "Pérez",
        Rol = RolUsuarioEnum.Encargado
    };

    [Fact]
    public void CrearUsuarioValidator_DatosValidos_DebePasarValidacion()
    {
        _crearValidator.Validate(AltaValida).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CrearUsuarioValidator_DatosSinFormatoPeroNormalizables_DebePasarValidacion()
    {
        // Arrange: el validador evalúa la forma normalizada, igual que el agregado.
        var dto = AltaValida with { NombreUsuario = " JPerez ", Nombre = "  juan  carlos ", Apellido = "de la FUENTE" };

        // Act & Assert
        _crearValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Juan123")]
    [InlineData("1234")]
    [InlineData("@@@")]
    [InlineData("😀😀")]
    public void CrearUsuarioValidator_NombreConDigitosOSimbolos_DebeFallarEnNombre(string nombre)
    {
        // Act
        var result = _crearValidator.Validate(AltaValida with { Nombre = nombre });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearUsuarioDto.Nombre))
            .Which.ErrorMessage.Should().Contain("solo admite letras");
    }

    [Fact]
    public void CrearUsuarioValidator_ApellidoConDigitos_DebeFallarEnApellido()
    {
        var result = _crearValidator.Validate(AltaValida with { Apellido = "P3rez" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearUsuarioDto.Apellido));
    }

    [Fact]
    public void CrearUsuarioValidator_NombreVacio_MuestraSoloElMensajeDeObligatorio()
    {
        // Act
        var result = _crearValidator.Validate(AltaValida with { Nombre = "" });

        // Assert: con CascadeMode.Stop no se acumula el error de formato sobre el de campo vacío.
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Complete el nombre.");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("...")]
    [InlineData("a..b")]
    [InlineData("jperez.")]
    [InlineData("jpérez")]
    public void CrearUsuarioValidator_NombreUsuarioConFormatoInvalido_DebeFallar(string nombreUsuario)
    {
        var result = _crearValidator.Validate(AltaValida with { NombreUsuario = nombreUsuario });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearUsuarioDto.NombreUsuario));
    }

    [Fact]
    public void CrearUsuarioValidator_PasswordMuyCorta_DebeFallar()
    {
        var result = _crearValidator.Validate(AltaValida with { Password = "12345" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearUsuarioDto.Password));
    }

    [Fact]
    public void CrearUsuarioValidator_PasswordSuperaLos72BytesDeBcrypt_DebeFallar()
    {
        // Arrange: 40 "ñ" son 40 caracteres pero 80 bytes en UTF-8.
        var password = new string('ñ', 40);

        // Act
        var result = _crearValidator.Validate(AltaValida with { Password = password });

        // Assert
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearUsuarioDto.Password))
            .Which.ErrorMessage.Should().Contain("72 bytes");
    }

    [Fact]
    public void CrearUsuarioValidator_PasswordDe72Bytes_DebePasarValidacion()
    {
        var result = _crearValidator.Validate(AltaValida with { Password = new string('a', 72) });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("jperez")]
    [InlineData("JPEREZ")]
    public void CrearUsuarioValidator_PasswordIgualAlNombreUsuario_DebeFallar(string password)
    {
        var result = _crearValidator.Validate(AltaValida with { Password = password });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearUsuarioDto.Password))
            .Which.ErrorMessage.Should().Contain("igual al nombre de usuario");
    }

    [Fact]
    public void CrearUsuarioValidator_RolInexistente_DebeFallar()
    {
        var result = _crearValidator.Validate(AltaValida with { Rol = (RolUsuarioEnum)99 });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CrearUsuarioDto.Rol));
    }

    [Fact]
    public void ModificarUsuarioValidator_DatosValidos_DebePasarValidacion()
    {
        _modificarValidator.Validate(ModificacionValida).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ModificarUsuarioValidator_NombreConDigitos_DebeFallar()
    {
        // La regla es la misma del alta: Crear y Modificar no pueden divergir (V-5).
        var result = _modificarValidator.Validate(ModificacionValida with { Nombre = "Juan123" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ModificarUsuarioDto.Nombre));
    }

    [Fact]
    public void ModificarUsuarioValidator_IdInvalido_DebeFallar()
    {
        var result = _modificarValidator.Validate(ModificacionValida with { IdUsuario = 0 });

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ModificarUsuarioDto.IdUsuario));
    }

    [Fact]
    public void CambiarPasswordValidator_PasswordSuperaLos72Bytes_DebeFallar()
    {
        var dto = new CambiarPasswordDto { IdUsuario = 1, NuevaPassword = new string('a', 73) };

        _cambiarPasswordValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CambiarPasswordValidator_PasswordValida_DebePasarValidacion()
    {
        var dto = new CambiarPasswordDto { IdUsuario = 1, NuevaPassword = "Clave2026!" };

        _cambiarPasswordValidator.Validate(dto).IsValid.Should().BeTrue();
    }
}
