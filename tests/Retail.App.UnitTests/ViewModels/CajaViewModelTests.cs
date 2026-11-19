using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Caja;
using Retail.Application.DTOs.Caja;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Pantalla de Caja con turno por cajero y terminal (MT-14): quién puede operar y cerrar, qué usuario viaja en cada
/// operación y la lista de turnos de otras terminales que solo ve un Gerente.
/// </summary>
public class CajaViewModelTests
{
    private const int IdCajero = 2;
    private const int IdGerente = 1;
    private const int IdOtroCajero = 9;

    private readonly ICajaService _cajaService = Substitute.For<ICajaService>();
    private readonly ICajaDialogService _dialogService = Substitute.For<ICajaDialogService>();
    private readonly ICurrentUserSession _sesion = Substitute.For<ICurrentUserSession>();

    public CajaViewModelTests()
    {
        _sesion.IdUsuario.Returns(IdCajero);
        _sesion.EsGerente.Returns(false);
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns((TurnoCajaDto?)null);
        _cajaService.ListarTurnosAbiertosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TurnoAbiertoDto>());
    }

    private static TurnoCajaDto Turno(int idTurno, int idUsuario, string nombre)
    {
        return new TurnoCajaDto
        {
            IdTurno = idTurno,
            IdUsuario = idUsuario,
            NombreUsuario = nombre,
            IdTerminal = 1,
            NombreTerminal = "Mostrador 1",
            FechaApertura = new DateTime(2026, 11, 19, 8, 0, 0),
            SaldoInicial = 20000m,
            SaldoTeoricoEfectivo = 150000m,
            TotalVentasElectronicas = 90000m,
            CantidadVentas = 14,
            Estado = EstadoTurnoEnum.Abierto
        };
    }

    private static TurnoAbiertoDto Abierto(int idTurno, string terminal, string cajero)
    {
        return new TurnoAbiertoDto
        {
            IdTurno = idTurno,
            IdTerminal = idTurno + 100,
            NombreTerminal = terminal,
            IdUsuario = IdOtroCajero,
            NombreCajero = cajero,
            FechaApertura = new DateTime(2026, 11, 19, 9, 30, 0),
            SaldoTeoricoEfectivo = 70000m,
            TotalVentasElectronicas = 31000m,
            CantidadVentas = 6
        };
    }

    private async Task<CajaViewModel> CrearAsync()
    {
        var viewModel = new CajaViewModel(_cajaService, _dialogService, _sesion);

        // El constructor lanza la lectura inicial: esta carga la reemplaza y espera a que el estado esté al día.
        await viewModel.CargarTurnoActivoAsync().WaitAsync(TimeSpan.FromSeconds(5));
        return viewModel;
    }

    [Fact]
    public async Task AbrirTurnoAsync_ConAperturaAceptada_UsaElIdDelUsuarioDeLaSesion()
    {
        _dialogService.MostrarDialogoApertura(out _).ReturnsForAnyArgs(x =>
        {
            x[0] = 5000m;
            return true;
        });
        _cajaService.AbrirTurnoAsync(Arg.Any<AperturaTurnoDto>(), Arg.Any<CancellationToken>())
            .Returns(Turno(7, IdCajero, "Juan Cajero"));
        var viewModel = await CrearAsync();

        await viewModel.AbrirTurnoAsync();

        await _cajaService.Received(1).AbrirTurnoAsync(
            Arg.Is<AperturaTurnoDto>(d => d.IdUsuario == IdCajero && d.SaldoInicial == 5000m),
            Arg.Any<CancellationToken>());
        viewModel.EsTurnoPropio.Should().BeTrue();
    }

    [Fact]
    public async Task Estado_TurnoPropio_PuedeOperarYCerrarPeroNoAbrir()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdCajero, "Juan Cajero"));

        var viewModel = await CrearAsync();

        viewModel.EsTurnoPropio.Should().BeTrue();
        viewModel.EsTurnoAjeno.Should().BeFalse();
        viewModel.PuedeOperar.Should().BeTrue();
        viewModel.PuedeCerrar.Should().BeTrue();
        viewModel.PuedeAbrirTurno.Should().BeFalse();
    }

    [Fact]
    public async Task Estado_SinTurno_SoloPuedeAbrir()
    {
        var viewModel = await CrearAsync();

        viewModel.PuedeAbrirTurno.Should().BeTrue();
        viewModel.PuedeOperar.Should().BeFalse();
        viewModel.PuedeCerrar.Should().BeFalse();
    }

    [Fact]
    public async Task Estado_TurnoAjenoVistoPorCajero_NoPuedeOperarNiCerrar()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdOtroCajero, "Ana Pérez"));

        var viewModel = await CrearAsync();

        viewModel.EsTurnoAjeno.Should().BeTrue();
        viewModel.EsTurnoPropio.Should().BeFalse();
        viewModel.PuedeOperar.Should().BeFalse();
        viewModel.PuedeCerrar.Should().BeFalse();
        viewModel.PuedeAbrirTurno.Should().BeFalse();
        viewModel.TituloTurnoAjeno.Should().Be("Turno de Ana Pérez sin cerrar");
    }

    [Fact]
    public async Task Estado_TurnoAjenoVistoPorGerente_NoPuedeOperarPeroSiCerrar()
    {
        _sesion.IdUsuario.Returns(IdGerente);
        _sesion.EsGerente.Returns(true);
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdOtroCajero, "Ana Pérez"));

        var viewModel = await CrearAsync();

        viewModel.EsTurnoAjeno.Should().BeTrue();
        viewModel.PuedeOperar.Should().BeFalse();
        viewModel.PuedeCerrar.Should().BeTrue();
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_ConTurnoPropio_EnviaElIdDeLaSesion()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdCajero, "Juan Cajero"));
        _dialogService.MostrarDialogoMovimiento(7).Returns(new MovimientoCajaDto
        {
            IdTurno = 7,
            IdUsuario = 0,
            TipoMovimiento = TipoMovimientoCajaEnum.RetiroExtraordinario,
            Monto = 3500m,
            Concepto = "Flete"
        });
        _cajaService.RegistrarMovimientoAsync(Arg.Any<MovimientoCajaDto>(), Arg.Any<CancellationToken>())
            .Returns(Turno(7, IdCajero, "Juan Cajero"));
        var viewModel = await CrearAsync();

        await viewModel.RegistrarMovimientoAsync();

        await _cajaService.Received(1).RegistrarMovimientoAsync(
            Arg.Is<MovimientoCajaDto>(d => d.IdUsuario == IdCajero && d.IdTurno == 7 && d.Monto == 3500m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_ConTurnoAjeno_NoAbreElDialogoNiLlamaAlServicio()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdOtroCajero, "Ana Pérez"));
        var viewModel = await CrearAsync();

        await viewModel.RegistrarMovimientoAsync();

        _dialogService.DidNotReceiveWithAnyArgs().MostrarDialogoMovimiento(default);
        await _cajaService.DidNotReceive().RegistrarMovimientoAsync(Arg.Any<MovimientoCajaDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarTurnoAsync_ConTurnoPropio_EnviaElActorYLosTresValoresLeidos()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdCajero, "Juan Cajero"));
        ConfigurarArqueo(declarado: 148000m, retenido: 20000m);
        _cajaService.CerrarTurnoConArqueoCiegoAsync(Arg.Any<ArqueoCiegoDto>(), Arg.Any<CancellationToken>())
            .Returns(Resultado());
        var viewModel = await CrearAsync();

        await viewModel.CerrarTurnoAsync();

        await _cajaService.Received(1).CerrarTurnoConArqueoCiegoAsync(
            Arg.Is<ArqueoCiegoDto>(d =>
                d.IdTurno == 7 &&
                d.IdUsuarioActor == IdCajero &&
                d.SaldoTeoricoLeido == 150000m &&
                d.TotalElectronicoLeido == 90000m &&
                d.CantidadVentasLeida == 14 &&
                d.SaldoDeclaradoEfectivo == 148000m &&
                d.MontoRetenidoEnCaja == 20000m),
            Arg.Any<CancellationToken>());
        viewModel.TurnoActivo.Should().BeNull();
        viewModel.PuedeAbrirTurno.Should().BeTrue();
    }

    [Fact]
    public async Task CerrarTurnoAsync_GerenteConTurnoAjeno_CierraConSuIdComoActor()
    {
        _sesion.IdUsuario.Returns(IdGerente);
        _sesion.EsGerente.Returns(true);
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdOtroCajero, "Ana Pérez"));
        ConfigurarArqueo(declarado: 148000m, retenido: 0m);
        _cajaService.CerrarTurnoConArqueoCiegoAsync(Arg.Any<ArqueoCiegoDto>(), Arg.Any<CancellationToken>())
            .Returns(Resultado());
        var viewModel = await CrearAsync();

        await viewModel.CerrarTurnoAsync();

        await _cajaService.Received(1).CerrarTurnoConArqueoCiegoAsync(
            Arg.Is<ArqueoCiegoDto>(d => d.IdTurno == 7 && d.IdUsuarioActor == IdGerente),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarTurnoAsync_CajeroConTurnoAjeno_NoHaceNada()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdOtroCajero, "Ana Pérez"));
        var viewModel = await CrearAsync();

        await viewModel.CerrarTurnoAsync();

        _dialogService.DidNotReceiveWithAnyArgs().MostrarDialogoArqueo(default, out _, out _);
        await _cajaService.DidNotReceive().CerrarTurnoConArqueoCiegoAsync(Arg.Any<ArqueoCiegoDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarTurnoAsync_ElTurnoCambioMientrasSeMiraba_AdviertaYVuelveALeer()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdCajero, "Juan Cajero"));
        ConfigurarArqueo(declarado: 148000m, retenido: 0m);
        _cajaService.CerrarTurnoConArqueoCiegoAsync(Arg.Any<ArqueoCiegoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TurnoModificadoException());
        var viewModel = await CrearAsync();
        var lecturasAntes = ContarLecturasDelTurno();

        await viewModel.CerrarTurnoAsync();

        _dialogService.Received(1).MostrarAdvertencia(Arg.Any<string>(), new TurnoModificadoException().Message);
        _dialogService.DidNotReceive().MostrarError(Arg.Any<string>(), Arg.Any<string>());

        // El turno cambió: tras el aviso se vuelve a leer, para mostrar lo que hay ahora
        ContarLecturasDelTurno().Should().Be(lecturasAntes + 1);
    }

    private int ContarLecturasDelTurno()
    {
        return _cajaService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ICajaService.ObtenerTurnoActivoAsync));
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_ServicioRechazaPorTurnoAjeno_MuestraAdvertencia()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdCajero, "Juan Cajero"));
        _dialogService.MostrarDialogoMovimiento(7).Returns(new MovimientoCajaDto
        {
            IdTurno = 7,
            IdUsuario = 0,
            TipoMovimiento = TipoMovimientoCajaEnum.IngresoExtraordinario,
            Monto = 100m,
            Concepto = "Cambio"
        });
        _cajaService.RegistrarMovimientoAsync(Arg.Any<MovimientoCajaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TurnoAjenoException("Solo el cajero que abrió el turno puede operarlo."));
        var viewModel = await CrearAsync();

        await viewModel.RegistrarMovimientoAsync();

        _dialogService.Received(1).MostrarAdvertencia(Arg.Any<string>(), "Solo el cajero que abrió el turno puede operarlo.");
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_ConflictoDeConcurrencia_MuestraAdvertencia()
    {
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdCajero, "Juan Cajero"));
        _dialogService.MostrarDialogoMovimiento(7).Returns(new MovimientoCajaDto
        {
            IdTurno = 7,
            IdUsuario = 0,
            TipoMovimiento = TipoMovimientoCajaEnum.IngresoExtraordinario,
            Monto = 100m,
            Concepto = "Cambio"
        });
        _cajaService.RegistrarMovimientoAsync(Arg.Any<MovimientoCajaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictoDeConcurrenciaException());
        var viewModel = await CrearAsync();

        await viewModel.RegistrarMovimientoAsync();

        _dialogService.Received(1).MostrarAdvertencia(Arg.Any<string>(), Arg.Any<string>());
        _dialogService.DidNotReceive().MostrarError(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CargarTurnoActivoAsync_ComoGerente_ListaLosTurnosDeLasOtrasTerminales()
    {
        _sesion.IdUsuario.Returns(IdGerente);
        _sesion.EsGerente.Returns(true);
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(Turno(7, IdGerente, "Gina Gerente"));
        _cajaService.ListarTurnosAbiertosAsync(IdGerente, Arg.Any<CancellationToken>()).Returns(new[]
        {
            Abierto(7, "Mostrador 1", "Gina Gerente"),
            Abierto(8, "Mostrador 2", "Ana Pérez"),
            Abierto(9, "Depósito", "Luis Díaz")
        });

        var viewModel = await CrearAsync();

        // El turno de esta terminal (7) ya está arriba: la lista solo trae las demás.
        viewModel.TurnosEnOtrasTerminales.Select(t => t.IdTurno).Should().Equal(8, 9);
        viewModel.NoHayTurnosEnOtrasTerminales.Should().BeFalse();
    }

    [Fact]
    public async Task CargarTurnoActivoAsync_ComoGerenteSinOtrosTurnos_MuestraElEstadoVacio()
    {
        _sesion.IdUsuario.Returns(IdGerente);
        _sesion.EsGerente.Returns(true);

        var viewModel = await CrearAsync();

        viewModel.TurnosEnOtrasTerminales.Should().BeEmpty();
        viewModel.NoHayTurnosEnOtrasTerminales.Should().BeTrue();
    }

    [Fact]
    public async Task CargarTurnoActivoAsync_ComoCajero_NoConsultaLosTurnosDeOtrasTerminales()
    {
        var viewModel = await CrearAsync();

        await _cajaService.DidNotReceive().ListarTurnosAbiertosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        viewModel.EsGerente.Should().BeFalse();
        viewModel.NoHayTurnosEnOtrasTerminales.Should().BeFalse();
    }

    [Fact]
    public async Task CerrarTurnoDeOtraTerminalAsync_Gerente_CierraConLosValoresDeLaFilaYRecarga()
    {
        _sesion.IdUsuario.Returns(IdGerente);
        _sesion.EsGerente.Returns(true);
        var fila = Abierto(8, "Mostrador 2", "Ana Pérez");
        _cajaService.ListarTurnosAbiertosAsync(IdGerente, Arg.Any<CancellationToken>()).Returns(new[] { fila });
        ConfigurarArqueo(declarado: 69500m, retenido: 0m);
        _cajaService.CerrarTurnoConArqueoCiegoAsync(Arg.Any<ArqueoCiegoDto>(), Arg.Any<CancellationToken>())
            .Returns(Resultado());
        var viewModel = await CrearAsync();

        // Tras el cierre, la lista que devuelve el servicio ya no incluye el turno
        _cajaService.ListarTurnosAbiertosAsync(IdGerente, Arg.Any<CancellationToken>()).Returns(Array.Empty<TurnoAbiertoDto>());

        await viewModel.CerrarTurnoDeOtraTerminalAsync(fila);

        await _cajaService.Received(1).CerrarTurnoConArqueoCiegoAsync(
            Arg.Is<ArqueoCiegoDto>(d =>
                d.IdTurno == 8 &&
                d.IdUsuarioActor == IdGerente &&
                d.SaldoTeoricoLeido == 70000m &&
                d.TotalElectronicoLeido == 31000m &&
                d.CantidadVentasLeida == 6 &&
                d.SaldoDeclaradoEfectivo == 69500m),
            Arg.Any<CancellationToken>());
        viewModel.TurnosEnOtrasTerminales.Should().BeEmpty();
    }

    [Fact]
    public async Task CerrarTurnoDeOtraTerminalAsync_NoGerente_NoHaceNada()
    {
        var viewModel = await CrearAsync();

        await viewModel.CerrarTurnoDeOtraTerminalAsync(Abierto(8, "Mostrador 2", "Ana Pérez"));

        _dialogService.DidNotReceiveWithAnyArgs().MostrarDialogoArqueo(default, out _, out _);
        await _cajaService.DidNotReceive().CerrarTurnoConArqueoCiegoAsync(Arg.Any<ArqueoCiegoDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarTurnoDeOtraTerminalAsync_ArqueoCancelado_NoLlamaAlServicio()
    {
        _sesion.IdUsuario.Returns(IdGerente);
        _sesion.EsGerente.Returns(true);
        _dialogService.MostrarDialogoArqueo(default, out _, out _).ReturnsForAnyArgs(false);
        var viewModel = await CrearAsync();

        await viewModel.CerrarTurnoDeOtraTerminalAsync(Abierto(8, "Mostrador 2", "Ana Pérez"));

        await _cajaService.DidNotReceive().CerrarTurnoConArqueoCiegoAsync(Arg.Any<ArqueoCiegoDto>(), Arg.Any<CancellationToken>());
    }

    private void ConfigurarArqueo(decimal declarado, decimal retenido)
    {
        _dialogService.MostrarDialogoArqueo(default, out _, out _).ReturnsForAnyArgs(x =>
        {
            x[1] = declarado;
            x[2] = retenido;
            return true;
        });
    }

    private static ResultadoArqueoDto Resultado()
    {
        return new ResultadoArqueoDto
        {
            IdTurno = 7,
            SaldoInicial = 20000m,
            TotalVentasEfectivo = 130000m,
            TotalIngresosEfectivo = 0m,
            TotalEgresosEfectivo = 0m,
            SaldoTeoricoEfectivo = 150000m,
            SaldoDeclaradoEfectivo = 148000m,
            DiferenciaEfectivo = -2000m,
            TotalVentasElectronicas = 90000m,
            MontoRetenidoEnCaja = 0m,
            FechaCierre = new DateTime(2026, 11, 19, 20, 0, 0)
        };
    }
}
