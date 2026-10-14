using FluentAssertions;
using Retail.App.ViewModels.Backups;
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

        public int Llamadas { get; private set; }

        public Exception? Falla { get; set; }

        public Task EjecutarAsync(string password, IProgress<int> progreso)
        {
            Llamadas++;
            PasswordRecibida = password;
            return Falla is null ? Task.CompletedTask : Task.FromException(Falla);
        }
    }

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
}
