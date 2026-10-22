using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Backups;
using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class BackupViewModelTests : IDisposable
{
    private const int IdActor = 7;
    private const string Password = "Clave2026!";
    private const string UsuarioGerente = "gerente";

    private readonly IBackupService _backupService = Substitute.For<IBackupService>();
    private readonly IRestauracionService _restauracionService = Substitute.For<IRestauracionService>();
    private readonly IBackupDialogService _dialogService = Substitute.For<IBackupDialogService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly IReinicioAplicacion _reinicio = Substitute.For<IReinicioAplicacion>();
    private readonly BackupViewModel _sut;

    public BackupViewModelTests()
    {
        _session.IdUsuario.Returns(IdActor);
        _session.EsGerente.Returns(true);

        _backupService.ListarAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new List<RegistroBackupDto>());
        _restauracionService.ObtenerUltimaRestauracionAsync(IdActor, Arg.Any<CancellationToken>())
            .Returns((RegistroRestauracionDto?)null);

        _sut = new BackupViewModel(_backupService, _restauracionService, _dialogService, _session, _reinicio);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    private static RegistroBackupDto Copia(
        EstadoBackupEnum estado = EstadoBackupEnum.Verificado,
        bool restaurable = true,
        DateTime? fechaUtc = null) => new()
        {
            IdRegistroBackup = 1,
            Referencia = Guid.NewGuid(),
            Tipo = TipoBackupEnum.Manual,
            Estado = estado,
            Operador = "gerente",
            FechaInicio = fechaUtc ?? new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Unspecified),
            RutaArchivo = @"C:\RetailData\Backups\Retail_20261001.bak",
            TamanoBytes = 2048,
            EsRestaurable = restaurable
        };

    /// <summary>
    /// Simula al Gerente en el diálogo: escribe la contraseña (y la palabra de confirmación) y confirma.
    /// </summary>
    private void GerenteConfirmaEnElDialogo(string password = Password)
    {
        _dialogService.SolicitarCredencial(Arg.Any<ConfirmarCredencialViewModel>()).Returns(ci =>
        {
            var vm = ci.Arg<ConfirmarCredencialViewModel>();
            vm.Password = password;
            vm.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
            vm.EjecutarAsync().GetAwaiter().GetResult();
            return new ResultadoCredencial(vm.Completada, vm.FalloOperacion);
        });
    }

    private void ComoAdministrador()
    {
        _session.EsGerente.Returns(false);
        _session.EsAdministrador.Returns(true);
    }

    /// <summary>
    /// Simula al Administrador en el diálogo: el usuario y la contraseña del Gerente que autoriza, y la palabra de confirmación.
    /// </summary>
    private void AdministradorConfirmaConAutorizacion(string usuario = UsuarioGerente, string password = Password)
    {
        _dialogService.SolicitarCredencial(Arg.Any<ConfirmarCredencialViewModel>()).Returns(ci =>
        {
            var vm = ci.Arg<ConfirmarCredencialViewModel>();
            vm.NombreUsuarioAutorizante = usuario;
            vm.Password = password;
            vm.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
            vm.EjecutarAsync().GetAwaiter().GetResult();
            return new ResultadoCredencial(vm.Completada, vm.FalloOperacion);
        });
    }

    private void GerenteCancelaElDialogo()
    {
        _dialogService.SolicitarCredencial(Arg.Any<ConfirmarCredencialViewModel>())
            .Returns(new ResultadoCredencial(false, null));
    }

    private static ResultadoBackupDto Creada() => new()
    {
        Referencia = Guid.NewGuid(),
        RutaArchivo = @"C:\RetailData\Backups\nueva.bak",
        TamanoBytes = 4096,
        HashSha256 = new string('a', 64),
        RegistradaEnHistorial = true
    };

    private static ResultadoRestauracionDto Restaurada() => new()
    {
        ReferenciaRestauracion = Guid.NewGuid(),
        ReferenciaCopia = Guid.NewGuid(),
        Advertencias = [ResultadoRestauracionDto.AvisoConciliacionArca, "Otra advertencia."]
    };

    // ───────────────────────────── Historial ─────────────────────────────

    [Fact]
    public async Task CargarAsync_Exito_CargaLasFilasYLaUltimaRestauracion()
    {
        // Arrange
        _backupService.ListarAsync(IdActor, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroBackupDto> { Copia(), Copia(EstadoBackupEnum.Fallido, restaurable: false) });
        _restauracionService.ObtenerUltimaRestauracionAsync(IdActor, Arg.Any<CancellationToken>())
            .Returns(new RegistroRestauracionDto
            {
                Referencia = Guid.NewGuid(),
                Fecha = DateTime.UtcNow,
                Origen = OrigenRestauracionEnum.Consola,
                NombreUsuario = "gerente",
                ReferenciaCopia = Guid.NewGuid(),
                RutaArchivo = @"C:\RetailData\Backups\vieja.bak"
            });

        // Act
        await _sut.CargarCommand.ExecuteAsync(null);

        // Assert
        _sut.Copias.Should().HaveCount(2);
        _sut.IsBusy.Should().BeFalse();
        _sut.MensajeError.Should().BeNull();
        _sut.TieneUltimaRestauracion.Should().BeTrue();
        _sut.UltimaRestauracionTexto.Should().Contain("gerente").And.Contain("consola").And.Contain("vieja.bak");
    }

    [Fact]
    public async Task CargarAsync_ServicioFalla_InformaElErrorSinPropagarlo()
    {
        // Arrange
        _backupService.ListarAsync(IdActor, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("sin conexión"));

        // Act
        await _sut.CargarCommand.ExecuteAsync(null);

        // Assert
        _sut.MensajeError.Should().Contain("sin conexión");
        _sut.TieneError.Should().BeTrue();
        _sut.IsBusy.Should().BeFalse();
        _dialogService.Received(1).MostrarError("Error de Carga", Arg.Is<string>(m => m.Contains("sin conexión")));
    }

    [Fact]
    public void RegistroBackupFila_FechaEnUtc_SeMuestraEnHoraLocal()
    {
        // Arrange: EF Core devuelve las fechas sin zona, aunque se guardaron en UTC.
        var utc = new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Unspecified);

        // Act
        var fila = new RegistroBackupFila(Copia(fechaUtc: utc));

        // Assert
        var esperada = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        fila.FechaLocal.Should().Be(esperada);
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1536, "KB")]
    [InlineData(5L * 1024 * 1024, "MB")]
    public void FormatearTamano_Bytes_UsaLaUnidadMasGrande(long bytes, string esperado)
    {
        // Act
        var texto = RegistroBackupFila.FormatearTamano(bytes);

        // Assert
        texto.Should().Contain(esperado);
    }

    // ───────────────────────────── Permisos de pantalla ─────────────────────────────

    [Fact]
    public void PuedeOperar_Gerente_HabilitaLosBotones()
    {
        // Assert
        _sut.PuedeOperar.Should().BeTrue();
        _sut.EsSoloLectura.Should().BeFalse();
        _sut.CrearBackupManualCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void PuedeOperar_Administrador_HabilitaLosBotones()
    {
        // Arrange
        ComoAdministrador();

        // Assert
        _sut.PuedeOperar.Should().BeTrue();
        _sut.EsSoloLectura.Should().BeFalse();
        _sut.CrearBackupManualCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void PuedeOperar_RolSinAutoridad_EsSoloLectura()
    {
        // Arrange
        _session.EsGerente.Returns(false);
        _session.EsAdministrador.Returns(false);

        // Assert
        _sut.PuedeOperar.Should().BeFalse();
        _sut.EsSoloLectura.Should().BeTrue();
        _sut.CrearBackupManualCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task CrearBackupManualAsync_RolSinAutoridad_NoAbreElDialogoNiLlamaAlServicio()
    {
        // Arrange
        _session.EsGerente.Returns(false);
        _session.EsAdministrador.Returns(false);

        // Act
        await _sut.CrearBackupManualCommand.ExecuteAsync(null);

        // Assert
        _dialogService.DidNotReceiveWithAnyArgs().SolicitarCredencial(default!);
        await _backupService.DidNotReceiveWithAnyArgs()
            .CrearBackupManualAsync(default, default!, default, default);
    }

    [Fact]
    public async Task CrearBackupManualAsync_AdministradorConSuPassword_CreaLaCopia()
    {
        // Arrange
        ComoAdministrador();
        GerenteConfirmaEnElDialogo();
        _backupService.CrearBackupManualAsync(IdActor, Password, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(Creada());

        // Act
        await _sut.CrearBackupManualCommand.ExecuteAsync(null);

        // Assert
        await _backupService.Received(1).CrearBackupManualAsync(IdActor, Password, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurarCopiaCommand_SegunLaCopiaSeleccionada_SoloSeHabilitaConUnaRestaurable()
    {
        // Arrange
        _backupService.ListarAsync(IdActor, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroBackupDto> { Copia(), Copia(EstadoBackupEnum.Fallido, restaurable: false) });
        await _sut.CargarCommand.ExecuteAsync(null);

        // Assert: sin selección
        _sut.RestaurarCopiaCommand.CanExecute(null).Should().BeFalse();

        // Copia no restaurable
        _sut.CopiaSeleccionada = _sut.Copias[1];
        _sut.RestaurarCopiaCommand.CanExecute(null).Should().BeFalse();

        // Copia restaurable
        _sut.CopiaSeleccionada = _sut.Copias[0];
        _sut.RestaurarCopiaCommand.CanExecute(null).Should().BeTrue();
    }

    // ───────────────────────────── Copia manual ─────────────────────────────

    [Fact]
    public async Task CrearBackupManualAsync_PasswordCorrecta_CreaLaCopiaYRecargaElHistorial()
    {
        // Arrange
        GerenteConfirmaEnElDialogo();
        _backupService.CrearBackupManualAsync(IdActor, Password, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(Creada());

        // Act
        await _sut.CrearBackupManualCommand.ExecuteAsync(null);

        // Assert
        await _backupService.Received(1).CrearBackupManualAsync(IdActor, Password, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Copia creada", Arg.Is<string>(m => m.Contains("nueva.bak")));
        await _backupService.Received(1).ListarAsync(IdActor, Arg.Any<CancellationToken>());
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task CrearBackupManualAsync_GerenteCancela_NoLlamaAlServicioNiRecarga()
    {
        // Arrange
        GerenteCancelaElDialogo();

        // Act
        await _sut.CrearBackupManualCommand.ExecuteAsync(null);

        // Assert
        await _backupService.DidNotReceiveWithAnyArgs().CrearBackupManualAsync(default, default!, default, default);
        await _backupService.DidNotReceiveWithAnyArgs().ListarAsync(default, default);
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task CrearBackupManualAsync_BackupFallido_MuestraErrorYRecargaElHistorial()
    {
        // Arrange
        GerenteConfirmaEnElDialogo();
        _backupService.CrearBackupManualAsync(IdActor, Password, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BackupFallidoException("Sin espacio en disco."));

        // Act
        await _sut.CrearBackupManualCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarError(Arg.Any<string>(), "Sin espacio en disco.");
        _sut.MensajeError.Should().Be("Sin espacio en disco.");

        // La copia fallida deja su registro: el historial se vuelve a leer.
        await _backupService.Received(1).ListarAsync(IdActor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearBackupManualAsync_YaHayUnaCopiaEnCurso_MuestraElMensajeDelDominio()
    {
        // Arrange
        GerenteConfirmaEnElDialogo();
        _backupService.CrearBackupManualAsync(IdActor, Password, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("Ya hay una copia en curso."));

        // Act
        await _sut.CrearBackupManualCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarError(Arg.Any<string>(), "Ya hay una copia en curso.");
    }

    // ───────────────────────────── Restauración ─────────────────────────────

    private async Task SeleccionarCopiaRestaurableAsync()
    {
        _backupService.ListarAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new List<RegistroBackupDto> { Copia() });
        await _sut.CargarCommand.ExecuteAsync(null);
        _sut.CopiaSeleccionada = _sut.Copias[0];
        _backupService.ClearReceivedCalls();
    }

    [Fact]
    public async Task RestaurarCopiaAsync_SinCopiaSeleccionada_MuestraErrorSinAbrirElDialogo()
    {
        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarError("Restauración no disponible", Arg.Any<string>());
        _dialogService.DidNotReceiveWithAnyArgs().SolicitarCredencial(default!);
    }

    [Fact]
    public async Task RestaurarCopiaAsync_Exito_MuestraLasAdvertenciasYReiniciaLaAplicacion()
    {
        // Arrange
        await SeleccionarCopiaRestaurableAsync();
        var referencia = _sut.CopiaSeleccionada!.Referencia;
        GerenteConfirmaEnElDialogo();
        _restauracionService.RestaurarDesdeHistorialAsync(IdActor, Password, referencia, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(Restaurada());

        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarInformacion(
            "Restauración completada",
            Arg.Is<string>(m => m.Contains("ARCA") && m.Contains("Otra advertencia.")));
        _reinicio.Received(1).Reiniciar();
        await _backupService.DidNotReceiveWithAnyArgs().ListarAsync(default, default);
    }

    [Fact]
    public async Task RestaurarCopiaAsync_Falla_MuestraElErrorYNoReinicia()
    {
        // Arrange
        await SeleccionarCopiaRestaurableAsync();
        GerenteConfirmaEnElDialogo();
        _restauracionService.RestaurarDesdeHistorialAsync(IdActor, Password, Arg.Any<Guid>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RestauracionFallidaException("Hay un turno de caja abierto."));

        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarError(Arg.Is<string>(t => t.Contains("no fue modificada")), "Hay un turno de caja abierto.");
        _reinicio.DidNotReceive().Reiniciar();
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task RestaurarCopiaAsync_GerenteCancela_NoRestauraNiReinicia()
    {
        // Arrange
        await SeleccionarCopiaRestaurableAsync();
        GerenteCancelaElDialogo();

        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        await _restauracionService.DidNotReceiveWithAnyArgs()
            .RestaurarDesdeHistorialAsync(default, default!, default, default, default);
        _reinicio.DidNotReceive().Reiniciar();
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task RestaurarCopiaAsync_ElReinicioFalla_PideReiniciarAMano()
    {
        // Arrange
        await SeleccionarCopiaRestaurableAsync();
        GerenteConfirmaEnElDialogo();
        _restauracionService.RestaurarDesdeHistorialAsync(IdActor, Password, Arg.Any<Guid>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(Restaurada());
        _reinicio.When(r => r.Reiniciar()).Do(_ => throw new InvalidOperationException("sin ejecutable"));

        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarError("Reinicio manual necesario", Arg.Any<string>());
    }

    [Fact]
    public async Task RestaurarCopiaAsync_Administrador_UsaLaAutorizacionDeGerenteYNuncaElCaminoDelGerente()
    {
        // Arrange
        ComoAdministrador();
        await SeleccionarCopiaRestaurableAsync();
        var referencia = _sut.CopiaSeleccionada!.Referencia;
        AdministradorConfirmaConAutorizacion();
        _restauracionService.RestaurarConAutorizacionDeGerenteAsync(
                IdActor, Arg.Any<CredencialAutorizanteDto>(), referencia, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(Restaurada());

        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        await _restauracionService.Received(1).RestaurarConAutorizacionDeGerenteAsync(
            IdActor,
            Arg.Is<CredencialAutorizanteDto>(c => c.NombreUsuario == UsuarioGerente && c.Password == Password),
            referencia,
            Arg.Any<IProgress<int>?>(),
            Arg.Any<CancellationToken>());
        await _restauracionService.DidNotReceiveWithAnyArgs()
            .RestaurarDesdeHistorialAsync(default, default!, default, default, default);
        _reinicio.Received(1).Reiniciar();
    }

    [Fact]
    public async Task RestaurarCopiaAsync_Gerente_SigueUsandoRestaurarDesdeHistorial()
    {
        // Arrange
        await SeleccionarCopiaRestaurableAsync();
        GerenteConfirmaEnElDialogo();
        _restauracionService.RestaurarDesdeHistorialAsync(IdActor, Password, Arg.Any<Guid>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(Restaurada());

        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        await _restauracionService.Received(1)
            .RestaurarDesdeHistorialAsync(IdActor, Password, Arg.Any<Guid>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
        await _restauracionService.DidNotReceiveWithAnyArgs()
            .RestaurarConAutorizacionDeGerenteAsync(default, default!, default, default, default);
    }

    [Fact]
    public async Task RestaurarCopiaAsync_AdministradorConCuentaBloqueada_InformaReiniciaYNoRecarga()
    {
        // Arrange
        ComoAdministrador();
        await SeleccionarCopiaRestaurableAsync();
        AdministradorConfirmaConAutorizacion();
        _restauracionService.RestaurarConAutorizacionDeGerenteAsync(
                IdActor, Arg.Any<CredencialAutorizanteDto>(), Arg.Any<Guid>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(CuentaBloqueadaException.PorAutorizacionesFallidas());

        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarError("Cuenta bloqueada", Arg.Is<string>(m => m.Contains("bloqueada") && m.Contains("reiniciará")));
        _reinicio.Received(1).Reiniciar();
        await _backupService.DidNotReceiveWithAnyArgs().ListarAsync(default, default);
    }

    [Fact]
    public async Task RestaurarCopiaAsync_AdministradorConGerenteIncorrecto_QuedaAbiertoSinReiniciar()
    {
        // Arrange: el diálogo queda abierto ante la credencial inválida y el operador cancela.
        ComoAdministrador();
        await SeleccionarCopiaRestaurableAsync();
        _restauracionService.RestaurarConAutorizacionDeGerenteAsync(
                IdActor, Arg.Any<CredencialAutorizanteDto>(), Arg.Any<Guid>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new CredencialesInvalidasException());
        var quedoAbierto = false;
        _dialogService.SolicitarCredencial(Arg.Any<ConfirmarCredencialViewModel>()).Returns(ci =>
        {
            var vm = ci.Arg<ConfirmarCredencialViewModel>();
            vm.NombreUsuarioAutorizante = UsuarioGerente;
            vm.Password = "incorrecta";
            vm.Frase = ConfirmarCredencialViewModel.FraseRestauracion;
            quedoAbierto = !vm.EjecutarAsync().GetAwaiter().GetResult();
            return new ResultadoCredencial(false, null);
        });

        // Act
        await _sut.RestaurarCopiaCommand.ExecuteAsync(null);

        // Assert
        quedoAbierto.Should().BeTrue();
        _reinicio.DidNotReceive().Reiniciar();
    }

    // ───────────────────────────── Última restauración ─────────────────────────────

    [Theory]
    [InlineData(null, "por gerente (")]
    [InlineData("admin", "ejecutada por admin, autorizada por gerente")]
    public async Task CargarAsync_UltimaRestauracion_DescribeAlOperadorSoloSiLoHay(string? operador, string esperado)
    {
        // Arrange
        _restauracionService.ObtenerUltimaRestauracionAsync(IdActor, Arg.Any<CancellationToken>())
            .Returns(new RegistroRestauracionDto
            {
                Referencia = Guid.NewGuid(),
                Fecha = DateTime.UtcNow,
                Origen = OrigenRestauracionEnum.Aplicacion,
                NombreUsuario = UsuarioGerente,
                NombreOperador = operador,
                ReferenciaCopia = Guid.NewGuid(),
                RutaArchivo = @"C:\RetailData\Backups\vieja.bak"
            });

        // Act
        await _sut.CargarCommand.ExecuteAsync(null);

        // Assert
        _sut.UltimaRestauracionTexto.Should().Contain(esperado);
    }
}
