using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.ViewModels.Listas;
using Retail.App.ViewModels.Ventas;
using Retail.Application.DTOs.Listas;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class GuardarListaViewModelTests : IDisposable
{
    private static readonly DateTime FechaLeida = new(2026, 11, 18, 13, 20, 0, DateTimeKind.Utc);

    private readonly IListaService _listaService = Substitute.For<IListaService>();
    private readonly CargaSerializada _acceso = new();

    private readonly IReadOnlyList<RenglonListaEntradaDto> _renglones =
    [
        new RenglonListaEntradaDto { IdArticulo = 1, Cantidad = 3 },
        new RenglonListaEntradaDto { IdArticulo = 2, Cantidad = 12 }
    ];

    public void Dispose()
    {
        _acceso.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SinListaVinculada_PideElNombreYGuardarEstaDeshabilitadoHastaEscribirlo()
    {
        var sut = CrearViewModel(vinculada: null);

        sut.TieneListaVinculada.Should().BeFalse();
        sut.PideNombre.Should().BeTrue();
        sut.GuardarCommand.CanExecute(null).Should().BeFalse();

        sut.Nombre = "3.º grado";

        sut.GuardarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void NombreVacioTocado_MuestraElErrorDeNombre()
    {
        var sut = CrearViewModel(vinculada: null);
        sut.TieneErrorNombre.Should().BeFalse("el aviso no aparece al abrir el diálogo");

        sut.Nombre = "x";
        sut.Nombre = "  ";

        sut.TieneErrorNombre.Should().BeTrue();
        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ConListaVinculada_ActualizarEsLaOpcionPorDefectoYNoPideNombre()
    {
        var sut = CrearViewModel(Vinculada());

        sut.TieneListaVinculada.Should().BeTrue();
        sut.ActualizarVinculada.Should().BeTrue();
        sut.GuardarComoNueva.Should().BeFalse();
        sut.PideNombre.Should().BeFalse();
        sut.TextoActualizar.Should().Be("Actualizar «3.º grado»");
        sut.GuardarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ElegirListaNueva_PideElNombreYNoGuardaHastaEscribirlo()
    {
        var sut = CrearViewModel(Vinculada());

        sut.GuardarComoNueva = true;

        sut.ActualizarVinculada.Should().BeFalse();
        sut.PideNombre.Should().BeTrue();
        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_ListaNueva_CreaConElNombreRecortadoElUsuarioYLosRenglones()
    {
        _listaService.CrearAsync(Arg.Any<CrearListaDto>(), Arg.Any<CancellationToken>()).Returns(ListaDevuelta(30, "Jardín"));
        var sut = CrearViewModel(vinculada: null);
        sut.Nombre = "  Jardín  ";
        bool cierreSolicitado = false;
        sut.CierreSolicitado += (_, _) => cierreSolicitado = true;

        await sut.GuardarCommand.ExecuteAsync(null);

        await _listaService.Received(1).CrearAsync(
            Arg.Is<CrearListaDto>(dto => dto.IdUsuario == 2 && dto.Nombre == "Jardín" && dto.Renglones.Count == 2),
            Arg.Any<CancellationToken>());
        sut.Guardada.Should().BeTrue();
        sut.ListaGuardada!.IdLista.Should().Be(30);
        cierreSolicitado.Should().BeTrue();
        sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_ActualizarLaVinculada_EnviaLaFechaLeidaAlCargarlaYElNombreDeLaLista()
    {
        _listaService.ActualizarAsync(12, Arg.Any<ActualizarListaDto>(), Arg.Any<CancellationToken>()).Returns(ListaDevuelta(12, "3.º grado"));
        var sut = CrearViewModel(Vinculada());

        await sut.GuardarCommand.ExecuteAsync(null);

        await _listaService.Received(1).ActualizarAsync(
            12,
            Arg.Is<ActualizarListaDto>(dto => dto.Nombre == "3.º grado" && dto.FechaModificacionLeida == FechaLeida && dto.Renglones.Count == 2),
            Arg.Any<CancellationToken>());
        await _listaService.DidNotReceiveWithAnyArgs().CrearAsync(default!, default);
        sut.Guardada.Should().BeTrue();
    }

    [Fact]
    public async Task Guardar_ConNombreRepetido_MuestraElErrorAdentroYNoCierra()
    {
        _listaService.CrearAsync(Arg.Any<CrearListaDto>(), Arg.Any<CancellationToken>())
            .Throws(new DomainException("Ya existe una lista activa con el nombre «Jardín»."));
        var sut = CrearViewModel(vinculada: null);
        sut.Nombre = "Jardín";
        bool cierreSolicitado = false;
        sut.CierreSolicitado += (_, _) => cierreSolicitado = true;

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Be("Ya existe una lista activa con el nombre «Jardín».");
        sut.TieneMensajeError.Should().BeTrue();
        sut.Guardada.Should().BeFalse();
        cierreSolicitado.Should().BeFalse();
        sut.IsBusy.Should().BeFalse();
        sut.GuardarCommand.CanExecute(null).Should().BeTrue("el cajero puede corregir el nombre y reintentar");
    }

    [Fact]
    public async Task Guardar_ConModificacionConcurrente_MuestraElMensajeDelServidor()
    {
        _listaService.ActualizarAsync(12, Arg.Any<ActualizarListaDto>(), Arg.Any<CancellationToken>())
            .Throws(new DomainException("Otro usuario modificó la lista «3.º grado» mientras la editaba."));
        var sut = CrearViewModel(Vinculada());

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("Otro usuario modificó la lista");
        sut.Guardada.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_ConErrorInesperado_MuestraUnMensajeGenericoSinElDetalleTecnico()
    {
        _listaService.CrearAsync(Arg.Any<CrearListaDto>(), Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("detalle interno"));
        var sut = CrearViewModel(vinculada: null);
        sut.Nombre = "Jardín";

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("No se pudo guardar la lista").And.NotContain("detalle interno");
        sut.Guardada.Should().BeFalse();
    }

    private GuardarListaViewModel CrearViewModel(ListaCargada? vinculada)
    {
        return new GuardarListaViewModel(2, _renglones, vinculada, _listaService, _acceso, NullLogger.Instance);
    }

    private static ListaCargada Vinculada()
    {
        return new ListaCargada(12, "3.º grado", FechaLeida);
    }

    private static ListaDto ListaDevuelta(int id, string nombre)
    {
        return new ListaDto
        {
            IdLista = id,
            Nombre = nombre,
            FechaModificacion = FechaLeida.AddHours(1),
            Renglones = [],
            TotalEstimado = 0m
        };
    }
}
