using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class UsuarioTests
{
    private const string EmailGerente = "gerente@libreria.com";

    private static Usuario CrearOperador(RolUsuarioEnum rol = RolUsuarioEnum.Cajero, string? email = null)
    {
        // Un Gerente siempre necesita email: si el test no indica uno, se usa el de referencia.
        var emailEfectivo = rol == RolUsuarioEnum.Gerente ? email ?? EmailGerente : email;
        return Usuario.Crear("operador", "Juan", "Pérez", "hash_inicial", rol, emailEfectivo);
    }

    [Fact]
    public void Crear_DatosSinFormato_NormalizaLoginNombreApellidoYEmail()
    {
        // Act
        var usuario = Usuario.Crear(
            "  JPerez ", "  juan   CARLOS ", "de la FUENTE", "hash", RolUsuarioEnum.Encargado, "  JPerez@Libreria.COM ");

        // Assert
        usuario.NombreUsuario.Should().Be("jperez");
        usuario.Nombre.Should().Be("Juan Carlos");
        usuario.Apellido.Should().Be("De la Fuente");
        usuario.NombreCompleto.Should().Be("Juan Carlos De la Fuente");
        usuario.EmailRecuperacion.Should().Be("jperez@libreria.com");
        usuario.PasswordHash.Should().Be("hash");
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Encargado);
    }

    [Fact]
    public void Crear_CuentaNueva_QuedaMarcadaParaCambiarPassword()
    {
        // Act: la contraseña inicial la eligió quien dio el alta, no el titular.
        var usuario = CrearOperador();

        // Assert
        usuario.DebeCambiarPassword.Should().BeTrue();
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Cajero)]
    [InlineData(RolUsuarioEnum.Encargado)]
    [InlineData(RolUsuarioEnum.Administrador)]
    public void Crear_RolDistintoDeGerenteSinEmail_GuardaEmailNulo(RolUsuarioEnum rol)
    {
        // Act
        var usuario = Usuario.Crear("jperez", "Juan", "Pérez", "hash", rol, "   ");

        // Assert
        usuario.EmailRecuperacion.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_GerenteSinEmail_LanzaDomainException(string? email)
    {
        var act = () => Usuario.Crear("jperez", "Juan", "Pérez", "hash", RolUsuarioEnum.Gerente, email);

        act.Should().Throw<DomainException>().WithMessage("*Gerente*email*");
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@b")]
    public void Crear_EmailInvalido_LanzaDomainException(string email)
    {
        var act = () => Usuario.Crear("jperez", "Juan", "Pérez", "hash", RolUsuarioEnum.Cajero, email);

        act.Should().Throw<DomainException>().WithMessage("*email*");
    }

    [Theory]
    [InlineData("Juan123", "Pérez")]
    [InlineData("Juan", "P3rez")]
    [InlineData("@@@", "Pérez")]
    [InlineData("Juan", "")]
    public void Crear_NombreOApellidoInvalido_LanzaDomainException(string nombre, string apellido)
    {
        var act = () => Usuario.Crear("jperez", nombre, apellido, "hash", RolUsuarioEnum.Cajero, null);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("123")]
    [InlineData("j perez")]
    [InlineData("a..b")]
    public void Crear_NombreUsuarioInvalido_LanzaDomainException(string nombreUsuario)
    {
        var act = () => Usuario.Crear(nombreUsuario, "Juan", "Pérez", "hash", RolUsuarioEnum.Cajero, null);

        act.Should().Throw<DomainException>().WithMessage("*nombre de usuario*");
    }

    [Fact]
    public void Crear_RolInexistente_LanzaDomainException()
    {
        var act = () => Usuario.Crear("jperez", "Juan", "Pérez", "hash", (RolUsuarioEnum)99, null);

        act.Should().Throw<DomainException>().WithMessage("*rol*");
    }

    [Fact]
    public void Crear_HashVacio_LanzaArgumentException()
    {
        var act = () => Usuario.Crear("jperez", "Juan", "Pérez", " ", RolUsuarioEnum.Cajero, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ActualizarDatos_ValoresValidos_NormalizaYActualizaNombreApellidoRolYEmail()
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        usuario.ActualizarDatos("maría  josé", "o’connor", RolUsuarioEnum.Gerente, "MJ@Libreria.com");

        // Assert
        usuario.Nombre.Should().Be("María José");
        usuario.Apellido.Should().Be("O'Connor");
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Gerente);
        usuario.EmailRecuperacion.Should().Be("mj@libreria.com");
    }

    [Theory]
    [InlineData("Juan123", "Gómez")]
    [InlineData("Pedro", "G0mez")]
    public void ActualizarDatos_NombreOApellidoInvalido_NoModificaElUsuario(string nombre, string apellido)
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        var act = () => usuario.ActualizarDatos(nombre, apellido, RolUsuarioEnum.Gerente, EmailGerente);

        // Assert: la invariante se verifica antes de asignar, así que el agregado queda intacto.
        act.Should().Throw<DomainException>();
        usuario.Nombre.Should().Be("Juan");
        usuario.Apellido.Should().Be("Pérez");
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Cajero);
        usuario.EmailRecuperacion.Should().BeNull();
    }

    [Fact]
    public void ActualizarDatos_AscenderAGerenteSinEmail_LanzaDomainExceptionYNoModificaElUsuario()
    {
        // Arrange
        var usuario = CrearOperador(RolUsuarioEnum.Encargado);

        // Act
        var act = () => usuario.ActualizarDatos("Pedro", "Gómez", RolUsuarioEnum.Gerente, null);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*Gerente*email*");
        usuario.Nombre.Should().Be("Juan");
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Encargado);
    }

    [Fact]
    public void ActualizarDatos_GerenteQuitaSuEmail_LanzaDomainExceptionYConservaElEmail()
    {
        // Arrange
        var usuario = CrearOperador(RolUsuarioEnum.Gerente);

        // Act
        var act = () => usuario.ActualizarDatos("Juan", "Pérez", RolUsuarioEnum.Gerente, "");

        // Assert
        act.Should().Throw<DomainException>();
        usuario.EmailRecuperacion.Should().Be(EmailGerente);
    }

    [Fact]
    public void ActualizarDatos_GerenteDegradadoACajero_PuedeQuitarElEmail()
    {
        // Arrange
        var usuario = CrearOperador(RolUsuarioEnum.Gerente);

        // Act
        usuario.ActualizarDatos("Juan", "Pérez", RolUsuarioEnum.Cajero, null);

        // Assert
        usuario.IdRol.Should().Be((int)RolUsuarioEnum.Cajero);
        usuario.EmailRecuperacion.Should().BeNull();
    }

    [Fact]
    public void BlanquearPassword_HashValido_ReemplazaHashYExigeCambioAlTitular()
    {
        // Arrange
        var usuario = CrearOperador();
        usuario.CambiarPasswordPropia("hash_del_titular");

        // Act
        usuario.BlanquearPassword("hash_temporal");

        // Assert
        usuario.PasswordHash.Should().Be("hash_temporal");
        usuario.DebeCambiarPassword.Should().BeTrue();
    }

    [Fact]
    public void CambiarPasswordPropia_HashValido_ReemplazaHashYLevantaLaObligacion()
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        usuario.CambiarPasswordPropia("nuevo_hash_seguro");

        // Assert
        usuario.PasswordHash.Should().Be("nuevo_hash_seguro");
        usuario.DebeCambiarPassword.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlanquearPassword_HashInvalido_LanzaArgumentException(string hashInvalido)
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        var act = () => usuario.BlanquearPassword(hashInvalido);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CambiarPasswordPropia_HashInvalido_LanzaArgumentExceptionYMantieneLaObligacion(string hashInvalido)
    {
        // Arrange
        var usuario = CrearOperador();

        // Act
        var act = () => usuario.CambiarPasswordPropia(hashInvalido);

        // Assert
        act.Should().Throw<ArgumentException>();
        usuario.DebeCambiarPassword.Should().BeTrue();
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Gerente, true)]
    [InlineData(RolUsuarioEnum.Administrador, false)]
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

    [Theory]
    // El Gerente gestiona cualquier rol.
    [InlineData(RolUsuarioEnum.Gerente, RolUsuarioEnum.Cajero, true)]
    [InlineData(RolUsuarioEnum.Gerente, RolUsuarioEnum.Encargado, true)]
    [InlineData(RolUsuarioEnum.Gerente, RolUsuarioEnum.Gerente, true)]
    [InlineData(RolUsuarioEnum.Gerente, RolUsuarioEnum.Administrador, true)]
    // El Administrador solo gestiona roles con menos poder que el suyo.
    [InlineData(RolUsuarioEnum.Administrador, RolUsuarioEnum.Cajero, true)]
    [InlineData(RolUsuarioEnum.Administrador, RolUsuarioEnum.Encargado, true)]
    [InlineData(RolUsuarioEnum.Administrador, RolUsuarioEnum.Gerente, false)]
    [InlineData(RolUsuarioEnum.Administrador, RolUsuarioEnum.Administrador, false)]
    // Los roles operativos no gestionan cuentas.
    [InlineData(RolUsuarioEnum.Encargado, RolUsuarioEnum.Cajero, false)]
    [InlineData(RolUsuarioEnum.Cajero, RolUsuarioEnum.Cajero, false)]
    public void PuedeGestionarCuentasDeRol_SegunRolDelActor_RespetaLaMatrizDeAutoridad(
        RolUsuarioEnum rolActor, RolUsuarioEnum rolObjetivo, bool esperado)
    {
        // Arrange
        var actor = CrearOperador(rolActor);

        // Act
        var puede = actor.PuedeGestionarCuentasDeRol(rolObjetivo);

        // Assert
        puede.Should().Be(esperado);
    }

    [Fact]
    public void PuedeGestionarCuentasDeRol_RolInexistente_RetornaFalseInclusoParaElGerente()
    {
        var gerente = CrearOperador(RolUsuarioEnum.Gerente);

        gerente.PuedeGestionarCuentasDeRol((RolUsuarioEnum)99).Should().BeFalse();
    }

    [Theory]
    [InlineData(RolUsuarioEnum.Administrador, true)]
    [InlineData(RolUsuarioEnum.Gerente, false)]
    [InlineData(RolUsuarioEnum.Encargado, false)]
    [InlineData(RolUsuarioEnum.Cajero, false)]
    public void EsAdministrador_SegunRol_RetornaResultadoEsperado(RolUsuarioEnum rol, bool esperado)
    {
        // Arrange
        var usuario = CrearOperador(rol);

        // Act
        var esAdministrador = usuario.EsAdministrador();

        // Assert
        esAdministrador.Should().Be(esperado);
    }
}
