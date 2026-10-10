using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class UsuarioTests
{
    private static Usuario CrearOperador(RolUsuarioEnum rol = RolUsuarioEnum.Cajero)
    {
        return Usuario.Crear("operador", "Juan", "Pérez", "hash_inicial", rol);
    }

    [Fact]
    public void Crear_DatosSinFormato_NormalizaLoginNombreYApellido()
    {
        // Act
        var usuario = Usuario.Crear("  JPerez ", "  juan   CARLOS ", "de la FUENTE", "hash", RolUsuarioEnum.Encargado);

        // Assert
        usuario.NombreUsuario.Should().Be("jperez");
        usuario.Nombre.Should().Be("Juan Carlos");
        usuario.Apellido.Should().Be("De la Fuente");
        usuario.NombreCompleto.Should().Be("Juan Carlos De la Fuente");
        usuario.PasswordHash.Should().Be("hash");
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Encargado);
    }

    [Theory]
    [InlineData("Juan123", "Pérez")]
    [InlineData("Juan", "P3rez")]
    [InlineData("@@@", "Pérez")]
    [InlineData("Juan", "")]
    public void Crear_NombreOApellidoInvalido_LanzaDomainException(string nombre, string apellido)
    {
        var act = () => Usuario.Crear("jperez", nombre, apellido, "hash", RolUsuarioEnum.Cajero);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("123")]
    [InlineData("j perez")]
    [InlineData("a..b")]
    public void Crear_NombreUsuarioInvalido_LanzaDomainException(string nombreUsuario)
    {
        var act = () => Usuario.Crear(nombreUsuario, "Juan", "Pérez", "hash", RolUsuarioEnum.Cajero);

        act.Should().Throw<DomainException>().WithMessage("*nombre de usuario*");
    }

    [Fact]
    public void Crear_RolInexistente_LanzaDomainException()
    {
        var act = () => Usuario.Crear("jperez", "Juan", "Pérez", "hash", (RolUsuarioEnum)99);

        act.Should().Throw<DomainException>().WithMessage("*rol*");
    }

    [Fact]
    public void Crear_HashVacio_LanzaArgumentException()
    {
        var act = () => Usuario.Crear("jperez", "Juan", "Pérez", " ", RolUsuarioEnum.Cajero);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ActualizarDatos_ValoresValidos_NormalizaYActualizaNombreApellidoYRol()
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        usuario.ActualizarDatos("maría  josé", "o’connor", RolUsuarioEnum.Gerente);

        // Assert
        usuario.Nombre.Should().Be("María José");
        usuario.Apellido.Should().Be("O'Connor");
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Gerente);
    }

    [Theory]
    [InlineData("Juan123", "Gómez")]
    [InlineData("Pedro", "G0mez")]
    public void ActualizarDatos_NombreOApellidoInvalido_NoModificaElUsuario(string nombre, string apellido)
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        var act = () => usuario.ActualizarDatos(nombre, apellido, RolUsuarioEnum.Gerente);

        // Assert: la invariante se verifica antes de asignar, así que el agregado queda intacto.
        act.Should().Throw<DomainException>();
        usuario.Nombre.Should().Be("Juan");
        usuario.Apellido.Should().Be("Pérez");
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Cajero);
    }

    [Fact]
    public void ActualizarPassword_HashValido_ActualizaPasswordHash()
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        usuario.ActualizarPassword("nuevo_hash_seguro");

        // Assert
        usuario.PasswordHash.Should().Be("nuevo_hash_seguro");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ActualizarPassword_HashInvalido_LanzaArgumentException(string hashInvalido)
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        var act = () => usuario.ActualizarPassword(hashInvalido);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, true)]
    [InlineData(RolUsuarioEnum.Encargado, false)]
    [InlineData(RolUsuarioEnum.Cajero, false)]
    public void EsGerente_SegunRol_RetornaResultadoEsperado(RolUsuarioEnum rol, bool esperado)
    {
        // Arrange
        var usuario = CrearOperador(rol);

        // Act
        var esGerente = usuario.EsGerente();

        // Assert
        esGerente.Should().Be(esperado);
    }
}
