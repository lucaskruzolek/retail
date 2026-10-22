using FluentAssertions;
using Retail.App.ViewModels.Backups;
using Retail.Application.DTOs.Backups;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ConfirmarCredencialViewModelTests
{
    private const string AvisoArca = "Conciliar con ARCA.";

    /// <summary>
    /// Operación de prueba que registra la contraseña recibida y, si se le pide, falla con la excepción indicada.
    /// </summary>
    private sealed class OperacionDePrueba
    {
        public string? PasswordRecibida { get; private set; }

        public CredencialAutorizanteDto? CredencialRecibida { get; set; }

        public string? PasswordActorRecibida { get; set; }

        public int Llamadas { get; private set; }

        public Exception? Falla { get; set; }

        public Task EjecutarAsync(string password, IProgress<int> progreso)
        {
            Llamadas++;
            PasswordRecibida = password;
            return Falla is null ? Task.CompletedTask : Task.FromException(Falla);
        }
    }

    private static ConfirmarCredencialViewModel CrearAutorizada(OperacionDePrueba operacion) =>
        ConfirmarCredencialViewModel.ParaRestauracionAutorizada(
            (passwordActor, credencial, progreso) =>
            {
                operacion.PasswordActorRecibida = passwordActor;
                operacion.CredencialRecibida = credencial;
                return operacion.EjecutarAsync(credencial.Password, progreso);
            },
            "01/10/2026 10:00",
            AvisoArca);

    private static ConfirmarCredencialViewModel CrearBackup(OperacionDePrueba operacion) =>
        ConfirmarCredencialViewModel.ParaBackupManual(operacion.EjecutarAsync);

    private static ConfirmarCredencialViewModel CrearRestauracion(OperacionDePrueba operacion) =>
        ConfirmarCredencialViewModel.ParaRestauracion(operacion.EjecutarAsync, "01/10/2026 10:00", AvisoArca);

    [Fact]
    public void ParaBackupManual_Configuracion_NoExigeFraseNiEsDestructivo()
    {
        // Act
        var vm = CrearBackup(new OperacionDePrueba());

        // Assert
        vm.ExigeFrase.Should().BeFalse();
        vm.EsDestructiva.Should().BeFalse();
        vm.NoEsDestructiva.Should().BeTrue();
        vm.TieneAviso.Should().BeFalse();
    }

    [Fact]
    public void ParaRestauracion_Configuracion_ExigeFraseMuestraFechaYAvisoFiscal()
    {
        // Act
        var vm = CrearRestauracion(new OperacionDePrueba());

        // Assert
        vm.ExigeFrase.Should().BeTrue();
        vm.EsDestructiva.Should().BeTrue();
        vm.TieneAviso.Should().BeTrue();
        vm.Aviso.Should().Be(AvisoArca);
        vm.Mensaje.Should().Contain("01/10/2026 10:00").And.Contain("TODOS los datos");
    }

    [Fact]
    public async Task EjecutarAsync_PasswordVacia_NoEjecutaLaOperacionYPideLaContrasena()
    {
        // Arrange
        var operacion = new OperacionDePrueba();
        var vm = CrearBackup(operacion);

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeFalse();
        operacion.Llamadas.Should().Be(0);
        vm.MensajeError.Should().Contain("contraseña");
        vm.TieneError.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("restaurar")]
    [InlineData("RESTAURA")]
    public async Task EjecutarAsync_RestauracionConFraseIncorrecta_NoEjecutaLaOperacion(string frase)
    {
        // Arrange
        var operacion = new OperacionDePrueba();
        var vm = CrearRestauracion(operacion);
        vm.Frase = frase;
        vm.Password = "Clave2026!";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeFalse();
        operacion.Llamadas.Should().Be(0);
        vm.MensajeError.Should().Contain(ConfirmarCredencialViewModel.FraseRestauracion);
    }

    [Fact]
    public async Task EjecutarAsync_RestauracionConFraseYPassword_EjecutaLaOperacionConLaPassword()
    {
        // Arrange
        var operacion = new OperacionDePrueba();
        var vm = CrearRestauracion(operacion);
        vm.Frase = " RESTAURAR ";
        vm.Password = "Clave2026!";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeTrue();
        vm.Completada.Should().BeTrue();
        vm.FalloOperacion.Should().BeNull();
        vm.IsBusy.Should().BeFalse();
        operacion.PasswordRecibida.Should().Be("Clave2026!");
    }

    [Fact]
    public async Task EjecutarAsync_CredencialesInvalidas_MuestraElErrorEnElDialogoYPermiteReintentar()
    {
        // Arrange
        var operacion = new OperacionDePrueba { Falla = new CredencialesInvalidasException() };
        var vm = CrearBackup(operacion);
        vm.Password = "incorrecta";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeFalse("el Gerente debe poder corregir la contraseña sin reabrir el diálogo");
        vm.Completada.Should().BeFalse();
        vm.FalloOperacion.Should().BeNull();
        vm.TieneError.Should().BeTrue();
        vm.Password.Should().BeEmpty();
        vm.IsBusy.Should().BeFalse();

        // El reintento con la contraseña correcta funciona.
        operacion.Falla = null;
        vm.Password = "Clave2026!";
        (await vm.EjecutarAsync()).Should().BeTrue();
        vm.Completada.Should().BeTrue();
        vm.MensajeError.Should().BeNull();
    }

    [Fact]
    public async Task EjecutarAsync_OperacionFallaPorOtroMotivo_CierraElDialogoYConservaLaExcepcion()
    {
        // Arrange
        var falla = new InvalidOperationException("disco lleno");
        var operacion = new OperacionDePrueba { Falla = falla };
        var vm = CrearBackup(operacion);
        vm.Password = "Clave2026!";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeTrue();
        vm.Completada.Should().BeFalse();
        vm.FalloOperacion.Should().BeSameAs(falla);
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void ParaBackupManual_Mensaje_NoMencionaAlGerente()
    {
        // Act
        var vm = CrearBackup(new OperacionDePrueba());

        // Assert: lo usan también los Administradores (D-32)
        vm.Mensaje.Should().NotContain("Gerente");
        vm.PideUsuarioAutorizante.Should().BeFalse();
        vm.PlaceholderPassword.Should().Be("Su contraseña");
    }

    [Fact]
    public void ParaRestauracionAutorizada_Configuracion_PideUsuarioYExplicaLaAutorizacion()
    {
        // Act
        var vm = CrearAutorizada(new OperacionDePrueba());

        // Assert
        vm.PideUsuarioAutorizante.Should().BeTrue();
        vm.ExigeFrase.Should().BeTrue();
        vm.TieneAviso.Should().BeTrue();
        vm.Mensaje.Should().Contain("01/10/2026 10:00").And.Contain("un Gerente debe ingresar SU usuario");
        vm.EtiquetaPassword.Should().Be("Contraseña del Gerente");
    }

    [Fact]
    public async Task EjecutarAsync_ModoAutorizadoSinUsuario_NoEjecutaLaOperacion()
    {
        // Arrange
        var operacion = new OperacionDePrueba();
        var vm = CrearAutorizada(operacion);
        vm.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
        vm.PasswordActor = "ClaveAdmin!";
        vm.Password = "Clave2026!";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeFalse();
        operacion.Llamadas.Should().Be(0);
        vm.MensajeError.Should().Be("Ingrese el usuario del Gerente que autoriza.");
    }

    [Fact]
    public async Task EjecutarAsync_ModoAutorizadoCompleto_LaOperacionRecibeUsuarioYPassword()
    {
        // Arrange
        var operacion = new OperacionDePrueba();
        var vm = CrearAutorizada(operacion);
        vm.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
        vm.NombreUsuarioAutorizante = " gerente ";
        vm.PasswordActor = "ClaveAdmin!";
        vm.Password = "Clave2026!";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeTrue();
        vm.Completada.Should().BeTrue();
        operacion.CredencialRecibida.Should().Be(new CredencialAutorizanteDto("gerente", "Clave2026!"));
        operacion.PasswordActorRecibida.Should().Be("ClaveAdmin!");
    }

    [Fact]
    public async Task EjecutarAsync_ModoAutorizadoSinPasswordPropia_NoEjecutaLaOperacionYLaPide()
    {
        // Arrange
        var operacion = new OperacionDePrueba();
        var vm = CrearAutorizada(operacion);
        vm.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
        vm.NombreUsuarioAutorizante = "gerente";
        vm.Password = "Clave2026!";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeFalse();
        operacion.Llamadas.Should().Be(0);
        vm.MensajeError.Should().Be("Ingrese su contraseña.");
    }

    [Fact]
    public async Task EjecutarAsync_ModoAutorizadoCredencialInvalida_ConservaElUsuarioYLimpiaAmbasPasswords()
    {
        // Arrange
        var operacion = new OperacionDePrueba { Falla = new CredencialesInvalidasException() };
        var vm = CrearAutorizada(operacion);
        vm.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
        vm.NombreUsuarioAutorizante = "gerente";
        vm.PasswordActor = "ClaveAdmin!";
        vm.Password = "incorrecta";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeFalse();
        vm.NombreUsuarioAutorizante.Should().Be("gerente");
        vm.Password.Should().BeEmpty();
        vm.PasswordActor.Should().BeEmpty();
        vm.TieneError.Should().BeTrue();
        vm.FalloOperacion.Should().BeNull();
    }

    [Fact]
    public async Task EjecutarAsync_ModoAutorizadoCuentaBloqueada_CierraElDialogoConLaExcepcion()
    {
        // Arrange
        var bloqueo = CuentaBloqueadaException.PorAutorizacionesFallidas();
        var operacion = new OperacionDePrueba { Falla = bloqueo };
        var vm = CrearAutorizada(operacion);
        vm.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
        vm.NombreUsuarioAutorizante = "gerente";
        vm.PasswordActor = "ClaveAdmin!";
        vm.Password = "Clave2026!";

        // Act
        var debeCerrar = await vm.EjecutarAsync();

        // Assert
        debeCerrar.Should().BeTrue();
        vm.FalloOperacion.Should().BeSameAs(bloqueo);
    }
}
