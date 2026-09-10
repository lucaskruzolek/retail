using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class UsuarioTests
{
    [Fact]
    public void ActualizarDatos_ValoresValidos_ActualizaNombreCompletoERol()
    {
        // Arrange
        var usuario = new Usuario
        {
            NombreUsuario = "operador",
            NombreCompleto = "Operador Inicial",
            IdRol = (int)RolUsuarioEnum.Cajero
        };

        // Act
        usuario.ActualizarDatos("Operador Modificado", (int)RolUsuarioEnum.Encargado);

        // Assert
        usuario.NombreCompleto.Should().Be("Operador Modificado");
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Encargado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ActualizarDatos_NombreCompletoVacio_LanzaArgumentException(string nombreInvalido)
    {
        // Arrange
        var usuario = new Usuario { NombreUsuario = "operador" };

        // Act
        var act = () => usuario.ActualizarDatos(nombreInvalido, (int)RolUsuarioEnum.Cajero);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ActualizarDatos_IdRolInvalido_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var usuario = new Usuario { NombreUsuario = "operador" };

        // Act
        var act = () => usuario.ActualizarDatos("Nombre Valido", 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ActualizarPassword_HashValido_ActualizaPasswordHash()
    {
        // Arrange
        var usuario = new Usuario { NombreUsuario = "operador", PasswordHash = "hash_antiguo" };

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
        var usuario = new Usuario { NombreUsuario = "operador" };

        // Act
        var act = () => usuario.ActualizarPassword(hashInvalido);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData((int)RolUsuarioEnum.Gerente, true)]
    [InlineData((int)RolUsuarioEnum.Encargado, false)]
    [InlineData((int)RolUsuarioEnum.Cajero, false)]
    public void EsGerente_SegunIdRol_RetornaResultadoEsperado(int idRol, bool esperado)
    {
        // Arrange
        var usuario = new Usuario { IdRol = idRol };

        // Act
        var esGerente = usuario.EsGerente();

        // Assert
        esGerente.Should().Be(esperado);
    }
}
