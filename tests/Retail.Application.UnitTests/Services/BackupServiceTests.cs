using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class BackupServiceTests
{
    private const int IdGerente = 1;
    private const int IdAdministrador = 2;
    private const int IdCajero = 3;
    private const string Migracion = "20261013083426_AgregarRegistrosBackup";
    private static readonly string Hash = new('c', 64);

    // Repositorio en memoria: el sustituto evalúa los predicados reales que usa el servicio.
    private readonly List<RegistroBackup> _historial = [];
    private readonly IRepository<RegistroBackup> _registros = Substitute.For<IRepository<RegistroBackup>>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IEjecutorBackupBaseDatos _ejecutor = Substitute.For<IEjecutorBackupBaseDatos>();
    private readonly IArchivosBackup _archivos = Substitute.For<IArchivosBackup>();
    private readonly IEsquemaBaseDatos _esquema = Substitute.For<IEsquemaBaseDatos>();
    private readonly IBloqueoRestauracion _bloqueo = Substitute.For<IBloqueoRestauracion>();
    private readonly BackupService _sut;
    private int _rutasGeneradas;

    public BackupServiceTests()
    {
        _registros.FindAsync(Arg.Any<Expression<Func<RegistroBackup, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => _historial.Where(ci.Arg<Expression<Func<RegistroBackup, bool>>>().Compile()).ToList());
        _registros.ListAllAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => _historial.ToList());
        _registros.AddAsync(Arg.Any<RegistroBackup>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var registro = ci.Arg<RegistroBackup>();
                registro.Id = _historial.Count == 0 ? 1 : _historial.Max(r => r.Id) + 1;
                _historial.Add(registro);
                return registro;
            });

        RegistrarUsuario(IdGerente, "gerente", RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdAdministrador, "soporte", RolUsuarioEnum.Administrador);
        RegistrarUsuario(IdCajero, "cajero", RolUsuarioEnum.Cajero);

        _esquema.ExisteHistorialDeBackupsAsync(Arg.Any<CancellationToken>()).Returns(true);
        _esquema.ObtenerUltimaMigracionAplicadaAsync(Arg.Any<CancellationToken>()).Returns(Migracion);
        _archivos.PrepararRutaNueva(Arg.Any<TipoBackupEnum>())
            .Returns(ci => $@"C:\RetailData\Backups\copia_{++_rutasGeneradas}_{ci.Arg<TipoBackupEnum>()}.bak");
        _ejecutor.CrearCopiaAsync(Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(new ArchivoBackupDto(2048, Hash));

        _bloqueo.AdquirirCompartidoAsync(Arg.Any<CancellationToken>()).Returns(_ => Substitute.For<IAsyncDisposable>());

        // Por defecto el exclusivo no está disponible (hay otra operación en marcha): los EnCurso se respetan (B-0).
        _bloqueo.IntentarAdquirirExclusivoAsync(Arg.Any<CancellationToken>()).Returns((IAsyncDisposable?)null);

        _sut = new BackupService(
            _registros, _usuarios, _unitOfWork, _authService, _ejecutor, _archivos, _esquema, _bloqueo,
            new PoliticaBackup(CantidadMaximaCopias: 2, VersionApp: "1.0.0"));
    }

    private void RegistrarUsuario(int id, string nombreUsuario, RolUsuarioEnum rol)
    {
        var email = rol == RolUsuarioEnum.Gerente ? $"{nombreUsuario}@libreria.com" : null;
        var usuario = Usuario.Crear(nombreUsuario, "Juan", "Pérez", "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    /// <summary>
    /// Copia previa a restaurar tomada hace <paramref name="diasAtras"/> días. La fecha se fija por reflexión, como la
    /// materializaría EF Core: el agregado solo crea registros con la fecha actual.
    /// </summary>
    private RegistroBackup CopiaPreRestauracionPrevia(string ruta, int diasAtras)
    {
        var registro = RegistroBackup.Iniciar(TipoBackupEnum.PreRestauracion, IdGerente, "gerente", ruta, Migracion, "1.0.0");
        registro.RegistrarExito(1024, Hash);
        registro.MarcarVerificado();
        typeof(RegistroBackup).GetProperty(nameof(RegistroBackup.FechaInicio))!
            .SetValue(registro, DateTime.UtcNow.AddDays(-diasAtras));
        registro.Id = _historial.Count + 1;
        _historial.Add(registro);
        return registro;
    }

    private RegistroBackup CopiaVerificadaPrevia(string ruta)
    {
        var registro = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, ruta, Migracion, "1.0.0");
        registro.RegistrarExito(1024, Hash);
        registro.MarcarVerificado();
        registro.Id = _historial.Count + 1;
        _historial.Add(registro);
        return registro;
    }

    // ───────────────────── Autorización ─────────────────────

    [Theory]
    [InlineData(IdAdministrador)]
    [InlineData(IdCajero)]
    [InlineData(999)]
    public async Task CrearBackupManualAsync_ActorQueNoEsGerente_LanzaOperacionNoAutorizadaSinCopiar(int idActor)
    {
        // Act
        var act = async () => await _sut.CrearBackupManualAsync(idActor, "Clave2026!");

        // Assert: la copia contiene la base completa; solo la pide su responsable (D-3).
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        _historial.Should().BeEmpty();
        await _ejecutor.DidNotReceiveWithAnyArgs().CrearCopiaAsync(default!, default);
    }

    [Fact]
    public async Task CrearBackupManualAsync_PasswordIncorrecta_PropagaCredencialesInvalidasSinRegistrarNada()
    {
        // Arrange
        _authService.VerificarCredencialAsync(IdGerente, "incorrecta", Arg.Any<CancellationToken>())
            .ThrowsAsync(new CredencialesInvalidasException());

        // Act
        var act = async () => await _sut.CrearBackupManualAsync(IdGerente, "incorrecta");

        // Assert: la reautenticación ocurre antes de crear el registro (D-8).
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
        _historial.Should().BeEmpty();
        _archivos.DidNotReceiveWithAnyArgs().PrepararRutaNueva(default);
    }

    // ───────────────────── Ciclo completo ─────────────────────

    [Fact]
    public async Task CrearBackupManualAsync_TodoCorrecto_RegistraVerificadoYEscribeElManifiesto()
    {
        // Act
        var resultado = await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert
        var registro = _historial.Should().ContainSingle().Subject;
        registro.Tipo.Should().Be(TipoBackupEnum.Manual);
        registro.Estado.Should().Be(EstadoBackupEnum.Verificado);
        registro.NombreUsuario.Should().Be("gerente");
        registro.UltimaMigracion.Should().Be(Migracion);
        registro.HashSha256.Should().Be(Hash);

        resultado.RegistradaEnHistorial.Should().BeTrue();
        resultado.Referencia.Should().Be(registro.Referencia);

        await _ejecutor.Received(1).VerificarCopiaAsync(registro.RutaArchivo, Arg.Any<CancellationToken>());
        await _archivos.Received(1).EscribirManifiestoAsync(
            registro.RutaArchivo,
            Arg.Is<ManifiestoBackupDto>(m => m.Referencia == registro.Referencia && m.Operador == "gerente" && m.RegistradaEnHistorial),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearBackupManualAsync_FallaElMotor_RegistraFallidoBorraElParcialYLanzaBackupFallido()
    {
        // Arrange
        _ejecutor.CrearCopiaAsync(Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Operating system error 112 (disco lleno)"));

        // Act
        var act = async () => await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert
        await act.Should().ThrowAsync<BackupFallidoException>().WithMessage("*disco lleno*");
        var registro = _historial.Should().ContainSingle().Subject;
        registro.Estado.Should().Be(EstadoBackupEnum.Fallido);
        registro.MensajeError.Should().Contain("disco lleno");
        _archivos.Received(1).EliminarCopia(registro.RutaArchivo);
        await _archivos.DidNotReceiveWithAnyArgs().EscribirManifiestoAsync(default!, default!, default);
    }

    [Fact]
    public async Task CrearBackupManualAsync_ArchivoQueNoPasaLaVerificacion_QuedaFallidoYNoRestaurable()
    {
        // Arrange
        _ejecutor.VerificarCopiaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("RESTORE VERIFYONLY: checksum incorrecto"));

        // Act
        var act = async () => await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert
        await act.Should().ThrowAsync<BackupFallidoException>();
        var registro = _historial.Single();
        registro.Estado.Should().Be(EstadoBackupEnum.Fallido);
        registro.EsRestaurable.Should().BeFalse();
    }

    [Fact]
    public async Task CrearBackupManualAsync_Cancelada_RegistraElFalloYPropagaLaCancelacion()
    {
        // Arrange
        _ejecutor.CrearCopiaAsync(Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert: la cancelación no se disfraza de error, pero queda en el historial.
        await act.Should().ThrowAsync<OperationCanceledException>();
        _historial.Single().MensajeError.Should().Be("Copia cancelada.");
    }

    [Fact]
    public async Task CrearBackupManualAsync_YaHayUnaCopiaEnCurso_LanzaDomainExceptionSinTocarElDisco()
    {
        // Arrange: el servicio automático empezó una copia hace un momento.
        _historial.Add(RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\otra.bak", Migracion, "1.0.0"));

        // Act
        var act = async () => await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*en curso*");
        _archivos.DidNotReceiveWithAnyArgs().PrepararRutaNueva(default);
    }

    [Fact]
    public async Task CrearBackupManualAsync_ElIndiceUnicoRechazaElRegistro_LanzaDomainExceptionDeCopiaEnCurso()
    {
        // Arrange: otro proceso pasó el control de "copia en curso" a la vez y su insert llegó primero.
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RestriccionDeUnicidadException());

        // Act
        var act = async () => await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert: el operador ve el mismo mensaje que con el control previo, no un error crudo de la base.
        var excepcion = (await act.Should().ThrowAsync<DomainException>().WithMessage("Ya hay una copia de seguridad en curso*")).Which;
        excepcion.InnerException.Should().BeOfType<RestriccionDeUnicidadException>();
        await _ejecutor.DidNotReceiveWithAnyArgs().CrearCopiaAsync(default!, default);
    }

    // ───────────────────── Copias huérfanas (B-0) ─────────────────────

    private RegistroBackup CopiaEnCursoHuerfana()
    {
        var huerfana = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\huerfana.bak", Migracion, "1.0.0");
        huerfana.Id = _historial.Count + 1;
        _historial.Add(huerfana);
        return huerfana;
    }

    [Fact]
    public async Task CrearBackupManualAsync_HuerfanaYExclusivoDisponible_LaCierraYCopia()
    {
        // Arrange: el exclusivo se obtiene, así que ninguna copia corre y la EnCurso es de un proceso muerto.
        var huerfana = CopiaEnCursoHuerfana();
        var exclusivo = Substitute.For<IAsyncDisposable>();
        _bloqueo.IntentarAdquirirExclusivoAsync(Arg.Any<CancellationToken>()).Returns(exclusivo);

        // Act
        var resultado = await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert
        huerfana.Estado.Should().Be(EstadoBackupEnum.Fallido);
        huerfana.MensajeError.Should().Be(RegistroBackup.MensajeCopiaHuerfana);
        resultado.RegistradaEnHistorial.Should().BeTrue();
        await exclusivo.Received(1).DisposeAsync();
        Received.InOrder(() =>
        {
            _ = _bloqueo.IntentarAdquirirExclusivoAsync(Arg.Any<CancellationToken>());
            _ = _bloqueo.AdquirirCompartidoAsync(Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(TipoBackupEnum.Automatico)]
    [InlineData(TipoBackupEnum.PreMigracion)]
    public async Task CrearBackupDelSistemaAsync_HuerfanaYExclusivoDisponible_LaCierraYCopia(TipoBackupEnum tipo)
    {
        // Arrange
        var huerfana = CopiaEnCursoHuerfana();
        _bloqueo.IntentarAdquirirExclusivoAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(tipo);

        // Assert
        huerfana.Estado.Should().Be(EstadoBackupEnum.Fallido);
        resultado.RegistradaEnHistorial.Should().BeTrue();
        await _ejecutor.Received(1).CrearCopiaAsync(Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_CopiaExitosaSinVerificarYExclusivoDisponible_LaCierraYBorraSusRestos()
    {
        // Arrange: el proceso murió durante la verificación; el registro quedó Exitoso y ocuparía un lugar de la retención
        // para siempre. Con el exclusivo tomado, ninguna copia corre: es huérfana.
        var huerfana = CopiaEnCursoHuerfana();
        huerfana.RegistrarExito(1024, Hash);
        var verificada = CopiaVerificadaPrevia(@"C:\RetailData\Backups\buena.bak");
        _bloqueo.IntentarAdquirirExclusivoAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert
        huerfana.Estado.Should().Be(EstadoBackupEnum.Fallido);
        huerfana.MensajeError.Should().Be(RegistroBackup.MensajeCopiaHuerfana);
        verificada.Estado.Should().Be(EstadoBackupEnum.Verificado, "una copia verificada nunca es huérfana");
        _archivos.Received(1).EliminarCopia(@"C:\huerfana.bak");
        resultado.RegistradaEnHistorial.Should().BeTrue();
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_CopiaExitosaSinVerificarYExclusivoNoDisponible_NoLaToca()
    {
        // Arrange: otra copia que está verificando sostiene el compartido: su registro Exitoso es legítimo.
        var enVerificacion = CopiaEnCursoHuerfana();
        enVerificacion.RegistrarExito(1024, Hash);

        // Act
        var act = async () => await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert: no hay otra EnCurso, así que la copia nueva sigue; el registro ajeno no se cierra ni se borra su archivo.
        await act.Should().NotThrowAsync();
        enVerificacion.Estado.Should().Be(EstadoBackupEnum.Exitoso);
        _archivos.DidNotReceive().EliminarCopia(@"C:\huerfana.bak");
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_NoSePuedeBorrarElRestoDeLaHuerfana_IgualCopia()
    {
        // Arrange: el archivo parcial sigue abierto por el motor tras la caída del proceso.
        CopiaEnCursoHuerfana();
        _bloqueo.IntentarAdquirirExclusivoAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());
        _archivos.When(a => a.EliminarCopia(@"C:\huerfana.bak")).Do(_ => throw new IOException("en uso"));

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert
        resultado.RegistradaEnHistorial.Should().BeTrue();
    }

    [Fact]
    public async Task CrearBackupManualAsync_EnCursoYExclusivoNoDisponible_NoTocaElRegistroYRechaza()
    {
        // Arrange: otra copia o una restauración tiene el bloqueo; su EnCurso es legítimo.
        var enCurso = CopiaEnCursoHuerfana();

        // Act
        var act = async () => await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*en curso*");
        enCurso.Estado.Should().Be(EstadoBackupEnum.EnCurso);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_ExclusivoDisponibleSinTablaDeHistorial_NoConsultaRegistros()
    {
        // Arrange
        _bloqueo.IntentarAdquirirExclusivoAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IAsyncDisposable>());
        _esquema.ExisteHistorialDeBackupsAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.PreMigracion);

        // Assert
        await _registros.DidNotReceiveWithAnyArgs().FindAsync(default!, default, default);
    }

    [Fact]
    public async Task CrearBackupPreRestauracionAsync_NuncaIntentaElExclusivo()
    {
        // Corre bajo el exclusivo de su restauración, que ya cerró los huérfanos.
        await _sut.CrearBackupPreRestauracionAsync(42, "gerente");

        await _bloqueo.DidNotReceiveWithAnyArgs().IntentarAdquirirExclusivoAsync(default);
    }

    // ───────────────────── Copias del sistema ─────────────────────

    [Theory]
    [InlineData(TipoBackupEnum.Manual)]
    [InlineData(TipoBackupEnum.PreRestauracion)]
    public async Task CrearBackupDelSistemaAsync_TipoQuePideUnaPersona_LanzaDomainException(TipoBackupEnum tipo)
    {
        var act = async () => await _sut.CrearBackupDelSistemaAsync(tipo);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_Automatica_QuedaRegistradaComoCopiaDelSistema()
    {
        await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        var registro = _historial.Single();
        registro.EsDelSistema.Should().BeTrue();
        registro.Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_BaseSinTablaDeHistorial_CopiaYDocumentaSoloEnElManifiesto()
    {
        // Arrange: una base de una versión anterior, a punto de migrarse.
        _esquema.ExisteHistorialDeBackupsAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.PreMigracion);

        // Assert: hay copia verificada aunque no haya dónde registrarla (nunca se migra sin red de seguridad).
        resultado.RegistradaEnHistorial.Should().BeFalse();
        await _ejecutor.Received(1).VerificarCopiaAsync(resultado.RutaArchivo, Arg.Any<CancellationToken>());
        await _registros.DidNotReceiveWithAnyArgs().AddAsync(default!);
        await _archivos.Received(1).EscribirManifiestoAsync(
            resultado.RutaArchivo,
            Arg.Is<ManifiestoBackupDto>(m => !m.RegistradaEnHistorial && m.Tipo == "PreMigracion"),
            Arg.Any<CancellationToken>());
    }

    // ───────────────────── Copia previa a restaurar (D-20) ─────────────────────

    [Fact]
    public async Task CrearBackupPreRestauracionAsync_ConHistorial_RegistraLaCopiaVerificadaConElOperador()
    {
        // Act
        var resultado = await _sut.CrearBackupPreRestauracionAsync(42, "gerente");

        // Assert
        var registro = _historial.Single();
        registro.Tipo.Should().Be(TipoBackupEnum.PreRestauracion);
        registro.IdUsuario.Should().Be(42);
        registro.NombreUsuario.Should().Be("gerente");
        registro.Estado.Should().Be(EstadoBackupEnum.Verificado);
        resultado.Referencia.Should().Be(registro.Referencia);
    }

    [Fact]
    public async Task CrearBackupPreRestauracionAsync_BaseSinTablaDeHistorial_DocumentaAlOperadorEnElManifiesto()
    {
        // Arrange: desde la consola, la base vigente puede ser de una versión anterior al historial.
        _esquema.ExisteHistorialDeBackupsAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var resultado = await _sut.CrearBackupPreRestauracionAsync(42, "gerente");

        // Assert
        resultado.RegistradaEnHistorial.Should().BeFalse();
        await _archivos.Received(1).EscribirManifiestoAsync(
            resultado.RutaArchivo,
            Arg.Is<ManifiestoBackupDto>(m => m.Operador == "gerente" && m.Tipo == "PreRestauracion"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearBackupPreRestauracionAsync_NoTomaElBloqueoCompartido()
    {
        // Corre bajo el bloqueo exclusivo de la restauración que la pide: tomar el compartido la bloquearía a sí misma.
        await _sut.CrearBackupPreRestauracionAsync(42, "gerente");

        await _bloqueo.DidNotReceiveWithAnyArgs().AdquirirCompartidoAsync(default);
    }

    // ───────────────────── Exclusión con las restauraciones (C-2) ─────────────────────

    [Fact]
    public async Task CrearBackupManualAsync_RestauracionEnCurso_RechazaSinRegistrarNiCopiar()
    {
        // Arrange
        _bloqueo.AdquirirCompartidoAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new BackupFallidoException("Hay una restauración en curso."));

        // Act
        var act = async () => await _sut.CrearBackupManualAsync(IdGerente, "Clave2026!");

        // Assert
        await act.Should().ThrowAsync<BackupFallidoException>().WithMessage("*restauración en curso*");
        _historial.Should().BeEmpty();
        await _ejecutor.DidNotReceiveWithAnyArgs().CrearCopiaAsync(default!, default);
    }

    [Theory]
    [InlineData(TipoBackupEnum.Automatico)]
    [InlineData(TipoBackupEnum.PreMigracion)]
    public async Task CrearBackupDelSistemaAsync_RestauracionEnCurso_RechazaSinCopiar(TipoBackupEnum tipo)
    {
        // Arrange
        _bloqueo.AdquirirCompartidoAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new BackupFallidoException("Hay una restauración en curso."));

        // Act
        var act = async () => await _sut.CrearBackupDelSistemaAsync(tipo);

        // Assert
        await act.Should().ThrowAsync<BackupFallidoException>();
        await _ejecutor.DidNotReceiveWithAnyArgs().CrearCopiaAsync(default!, default);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_CopiaTerminada_LiberaElBloqueoCompartido()
    {
        // Arrange
        var liberacion = Substitute.For<IAsyncDisposable>();
        _bloqueo.AdquirirCompartidoAsync(Arg.Any<CancellationToken>()).Returns(liberacion);

        // Act
        await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert
        await liberacion.Received(1).DisposeAsync();
    }

    // ───────────────────── Retención ─────────────────────

    [Fact]
    public async Task CrearBackupDelSistemaAsync_CopiasPreviasARestaurarRecientes_NoCuentanEnLaCantidadMaxima()
    {
        // Arrange (D-24): la política conserva 2 copias; las previas a restaurar no ocupan esos lugares.
        var previa1 = CopiaPreRestauracionPrevia(@"C:\RetailData\Backups\previa1.bak", diasAtras: 3);
        var previa2 = CopiaPreRestauracionPrevia(@"C:\RetailData\Backups\previa2.bak", diasAtras: 2);
        var anterior = CopiaVerificadaPrevia(@"C:\RetailData\Backups\anterior.bak");

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert
        resultado.CopiasPurgadas.Should().BeEmpty();
        previa1.Estado.Should().Be(EstadoBackupEnum.Verificado);
        previa2.Estado.Should().Be(EstadoBackupEnum.Verificado);
        anterior.Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_CopiaPreviaARestaurarVencida_SePurgaPorAntiguedad()
    {
        // Arrange: la política por defecto conserva las copias previas a restaurar durante 30 días.
        var vencida = CopiaPreRestauracionPrevia(@"C:\RetailData\Backups\vencida.bak", diasAtras: 31);
        var vigente = CopiaPreRestauracionPrevia(@"C:\RetailData\Backups\vigente.bak", diasAtras: 29);

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert
        resultado.CopiasPurgadas.Should().Equal(vencida.RutaArchivo);
        vencida.Estado.Should().Be(EstadoBackupEnum.ArchivoPurgado);
        vigente.Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_SuperaLaCantidadMaxima_PurgaLasMasViejasYConservaSusRegistros()
    {
        // Arrange: dos copias anteriores; la política conserva 2, así que con la nueva sobra la más vieja.
        var masVieja = CopiaVerificadaPrevia(@"C:\RetailData\Backups\vieja.bak");
        var anterior = CopiaVerificadaPrevia(@"C:\RetailData\Backups\anterior.bak");

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert
        resultado.CopiasPurgadas.Should().Equal(masVieja.RutaArchivo);
        _archivos.Received(1).EliminarCopia(masVieja.RutaArchivo);
        masVieja.Estado.Should().Be(EstadoBackupEnum.ArchivoPurgado, "el registro queda como historia (Ley 3)");
        anterior.Estado.Should().Be(EstadoBackupEnum.Verificado);
        _historial.Should().HaveCount(3);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_ArchivoViejoQueNoSePuedeBorrar_LoInformaSinInvalidarLaCopiaNueva()
    {
        // Arrange
        var masVieja = CopiaVerificadaPrevia(@"C:\RetailData\Backups\abierta.bak");
        CopiaVerificadaPrevia(@"C:\RetailData\Backups\anterior.bak");
        _archivos.When(a => a.EliminarCopia(masVieja.RutaArchivo)).Throw(new IOException("El archivo está en uso"));

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert
        resultado.ErroresDeRetencion.Should().ContainSingle().Which.Should().Contain("en uso");
        masVieja.Estado.Should().Be(EstadoBackupEnum.Verificado, "se reintenta en la próxima copia");
        _historial.Last().Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_RutaRechazadaPorElBorradoSeguro_LoInformaSinInvalidarLaCopiaNueva()
    {
        // Arrange: la ruta de un registro fue alterada para apuntar fuera del directorio de respaldos.
        var adulterada = CopiaVerificadaPrevia(@"C:\Windows\System32\config\SAM");
        CopiaVerificadaPrevia(@"C:\RetailData\Backups\anterior.bak");
        _archivos.When(a => a.EliminarCopia(adulterada.RutaArchivo))
            .Throw(new UnauthorizedAccessException("Se rechazó borrar la ruta"));

        // Act
        var resultado = await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);

        // Assert: se informa, el registro no se marca como purgado y la copia nueva queda verificada.
        resultado.ErroresDeRetencion.Should().ContainSingle().Which.Should().Contain("Se rechazó borrar");
        adulterada.Estado.Should().Be(EstadoBackupEnum.Verificado);
        _historial.Last().Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_SinHistorialYCancelada_BorraElParcialYPropagaLaCancelacion()
    {
        // Arrange
        _esquema.ExisteHistorialDeBackupsAsync(Arg.Any<CancellationToken>()).Returns(false);
        _ejecutor.CrearCopiaAsync(Arg.Any<string>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _sut.CrearBackupDelSistemaAsync(TipoBackupEnum.PreMigracion);

        // Assert: igual que en la copia con historial, no queda un .bak a medias.
        await act.Should().ThrowAsync<OperationCanceledException>();
        _archivos.Received(1).EliminarCopia(@"C:\RetailData\Backups\copia_1_PreMigracion.bak");
    }

    // ───────────────────── Historial ─────────────────────

    [Fact]
    public async Task ListarAsync_ActorAdministrador_DevuelveElHistorialDelMasRecienteAlMasViejo()
    {
        // Arrange
        CopiaVerificadaPrevia(@"C:\1.bak");
        CopiaVerificadaPrevia(@"C:\2.bak");

        // Act
        var historial = await _sut.ListarAsync(IdAdministrador);

        // Assert
        historial.Select(r => r.RutaArchivo).Should().Equal(@"C:\2.bak", @"C:\1.bak");
        historial.Should().OnlyContain(r => r.Operador == "Sistema" && r.EsRestaurable);
    }

    [Fact]
    public async Task ListarAsync_ActorCajero_LanzaOperacionNoAutorizada()
    {
        var act = async () => await _sut.ListarAsync(IdCajero);

        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
    }
}
