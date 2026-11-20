using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Retail.Application.DTOs.Caja;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Services;
using Retail.Application.Validators.Caja;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Un turno abierto por terminal Y por cajero (Track 6.7, Paquete 3; MT-1 y MT-9): los dos índices únicos filtrados de
/// TURNOS_CAJA son la garantía final, y CajaService abre turnos en dos terminales a la vez.
/// </summary>
public class TurnoPorTerminalTests : PedidosPruebaBase
{
    public TurnoPorTerminalTests()
        : base("TurnoTerminal")
    {
    }

    [Fact]
    public async Task Indices_DosTerminalesConTurnoAbiertoDeCajerosDistintos_Conviven()
    {
        // Arrange
        var terminalA = await TerminalesDePrueba.SembrarAsync(Contexto);
        var terminalB = await TerminalesDePrueba.SembrarAsync(Contexto);
        var cajero = await SembrarCajeroAsync("cajero.dos");

        // Act
        Contexto.TurnosCaja.Add(TurnoCaja.Abrir(IdUsuario, terminalA, 1000m));
        Contexto.TurnosCaja.Add(TurnoCaja.Abrir(cajero.Id, terminalB, 2000m));
        await Contexto.SaveChangesAsync();

        // Assert
        (await Contexto.TurnosCaja.CountAsync(t => t.Estado == EstadoTurnoEnum.Abierto)).Should().Be(2);
    }

    [Fact]
    public async Task Indices_SegundoTurnoAbiertoEnLaMismaTerminal_LoRechazaElIndice()
    {
        // Arrange
        var terminal = await TerminalesDePrueba.SembrarAsync(Contexto);
        var cajero = await SembrarCajeroAsync("cajero.dos");
        Contexto.TurnosCaja.Add(TurnoCaja.Abrir(IdUsuario, terminal, 1000m));
        await Contexto.SaveChangesAsync();

        // Act
        Contexto.TurnosCaja.Add(TurnoCaja.Abrir(cajero.Id, terminal, 1000m));
        var act = () => new UnitOfWork(Contexto).SaveChangesAsync();

        // Assert
        var excepcion = await act.Should().ThrowAsync<RestriccionDeUnicidadException>();
        excepcion.Which.CorrespondeAIndice("UX_TURNOS_CAJA_AbiertoPorTerminal").Should().BeTrue();
    }

    [Fact]
    public async Task Indices_MismoCajeroConTurnoAbiertoEnDosTerminales_LoRechazaElIndice()
    {
        // Arrange
        var terminalA = await TerminalesDePrueba.SembrarAsync(Contexto);
        var terminalB = await TerminalesDePrueba.SembrarAsync(Contexto);
        Contexto.TurnosCaja.Add(TurnoCaja.Abrir(IdUsuario, terminalA, 1000m));
        await Contexto.SaveChangesAsync();

        // Act
        Contexto.TurnosCaja.Add(TurnoCaja.Abrir(IdUsuario, terminalB, 1000m));
        var act = () => new UnitOfWork(Contexto).SaveChangesAsync();

        // Assert
        var excepcion = await act.Should().ThrowAsync<RestriccionDeUnicidadException>();
        excepcion.Which.CorrespondeAIndice("UX_TURNOS_CAJA_AbiertoPorUsuario").Should().BeTrue();
    }

    [Fact]
    public async Task Indices_TurnoCerrado_LiberaLaTerminalYAlCajero()
    {
        // Arrange
        var terminal = await TerminalesDePrueba.SembrarAsync(Contexto);
        var primero = TurnoCaja.Abrir(IdUsuario, terminal, 1000m);
        Contexto.TurnosCaja.Add(primero);
        await Contexto.SaveChangesAsync();
        primero.CerrarConArqueoCiego(1000m, 0m);
        await Contexto.SaveChangesAsync();

        // Act
        Contexto.TurnosCaja.Add(TurnoCaja.Abrir(IdUsuario, terminal, 1000m));
        var act = () => Contexto.SaveChangesAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AbrirTurnoAsync_DosTerminalesConCajerosDistintos_AbrenTurnosIndependientes()
    {
        // Arrange
        var terminalA = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 1");
        var terminalB = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 2");
        var cajero = await SembrarCajeroAsync("cajero.dos");
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        var cajaA = CrearServicio(contextoA, terminalA);
        var cajaB = CrearServicio(contextoB, terminalB);

        // Act
        var turnoA = await cajaA.AbrirTurnoAsync(new AperturaTurnoDto { IdUsuario = IdUsuario, SaldoInicial = 1000m });
        var turnoB = await cajaB.AbrirTurnoAsync(new AperturaTurnoDto { IdUsuario = cajero.Id, SaldoInicial = 2000m });

        // Assert: cada terminal ve solamente su turno, con los nombres del cajero y de la terminal.
        turnoA.IdTerminal.Should().Be(terminalA);
        turnoA.NombreTerminal.Should().Be("Mostrador 1");
        turnoA.NombreUsuario.Should().Be("Ana Perez");
        turnoB.IdTerminal.Should().Be(terminalB);
        (await cajaA.ObtenerTurnoActivoAsync())!.IdTurno.Should().Be(turnoA.IdTurno);
        (await cajaB.ObtenerTurnoActivoAsync())!.IdTurno.Should().Be(turnoB.IdTurno);
    }

    [Fact]
    public async Task AbrirTurnoAsync_TerminalConTurnoAbiertoDeOtroCajero_LanzaTurnoYaAbiertoConElNombreDelCajero()
    {
        // Arrange
        var terminal = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 1");
        var cajero = await SembrarCajeroAsync("cajero.dos");
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        await CrearServicio(contextoA, terminal).AbrirTurnoAsync(new AperturaTurnoDto { IdUsuario = IdUsuario, SaldoInicial = 1000m });

        // Act
        var act = () => CrearServicio(contextoB, terminal).AbrirTurnoAsync(new AperturaTurnoDto { IdUsuario = cajero.Id, SaldoInicial = 500m });

        // Assert
        await act.Should().ThrowAsync<TurnoYaAbiertoException>().WithMessage("*turno abierto de Ana Perez*");
    }

    [Fact]
    public async Task AbrirTurnoAsync_CajeroConTurnoAbiertoEnOtraTerminal_LanzaTurnoYaAbiertoConElNombreDeLaTerminal()
    {
        // Arrange
        var terminalA = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 1");
        var terminalB = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 2");
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        await CrearServicio(contextoA, terminalA).AbrirTurnoAsync(new AperturaTurnoDto { IdUsuario = IdUsuario, SaldoInicial = 1000m });

        // Act
        var act = () => CrearServicio(contextoB, terminalB).AbrirTurnoAsync(new AperturaTurnoDto { IdUsuario = IdUsuario, SaldoInicial = 500m });

        // Assert
        await act.Should().ThrowAsync<TurnoYaAbiertoException>().WithMessage("*terminal «Mostrador 1»*");
    }

    [Fact]
    public async Task ListarTurnosAbiertosAsync_GerenteConDosTurnosAbiertos_ListaAmbosConSusNombres()
    {
        // Arrange
        var terminalA = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 1");
        var terminalB = await TerminalesDePrueba.SembrarAsync(Contexto, "Mostrador 2");
        var cajero = await SembrarCajeroAsync("cajero.dos");
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();
        await CrearServicio(contextoA, terminalA).AbrirTurnoAsync(new AperturaTurnoDto { IdUsuario = IdUsuario, SaldoInicial = 1000m });
        await CrearServicio(contextoB, terminalB).AbrirTurnoAsync(new AperturaTurnoDto { IdUsuario = cajero.Id, SaldoInicial = 2000m });

        // Act
        var turnos = await CrearServicio(Contexto, terminalA).ListarTurnosAbiertosAsync(IdUsuario);

        // Assert
        turnos.Select(t => t.NombreTerminal).Should().Equal("Mostrador 1", "Mostrador 2");
        turnos.Select(t => t.NombreCajero).Should().Equal("Ana Perez", "Cajero Dos");
        turnos[1].SaldoTeoricoEfectivo.Should().Be(2000m);
    }

    private async Task<Usuario> SembrarCajeroAsync(string nombreUsuario)
    {
        var cajero = Usuario.Crear(nombreUsuario, "Cajero", "Dos", "hash-de-prueba", RolUsuarioEnum.Cajero, null);
        Contexto.Usuarios.Add(cajero);
        await Contexto.SaveChangesAsync();
        return cajero;
    }

    private static CajaService CrearServicio(RetailDbContext contexto, int idTerminal)
    {
        var terminalLocal = Substitute.For<ITerminalLocal>();
        terminalLocal.IdTerminal.Returns(idTerminal);

        return new CajaService(
            new Repository<TurnoCaja>(contexto),
            new Repository<Usuario>(contexto),
            new Repository<Terminal>(contexto),
            new TurnoQueryService(contexto),
            terminalLocal,
            new UnitOfWork(contexto),
            new AperturaTurnoValidator(),
            new MovimientoCajaValidator(),
            new ArqueoCiegoValidator());
    }
}
