using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Pestaña "Pares propuestos" del término seleccionado (M-49, M-71). La carga la hace la sección Términos; acá se
/// prueba lo que muestra y lo que guarda, con un contador en lugar de la recarga real.
/// </summary>
public class ParesPropuestosViewModelTests : IDisposable
{
    private const int IdActor = 7;

    private readonly ITerminoClasificacionService _terminoService = Substitute.For<ITerminoClasificacionService>();
    private readonly IClasificacionDialogService _dialogService = Substitute.For<IClasificacionDialogService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly CargaSerializada _carga = new();
    private readonly ParesPropuestosViewModel _sut;
    private int _recargas;

    private readonly List<CategoriaDto> _categorias =
    [
        new() { IdCategoria = 10, NombreCategoria = "Cuadernos", IdRubro = 1 },
        new() { IdCategoria = 11, NombreCategoria = "Mochilas", IdRubro = 1 }
    ];

    private readonly List<ParPropuestoDto> _propuestos =
    [
        Propuesto("TAPA", 10, 8, 8),
        Propuesto("ESCOLAR", 10, 5, 6)
    ];

    public ParesPropuestosViewModelTests()
    {
        _session.IdUsuario.Returns(IdActor);
        _session.EsGerente.Returns(true);

        _sut = new ParesPropuestosViewModel(
            _terminoService,
            _dialogService,
            _session,
            _carga,
            NullLogger.Instance,
            () =>
            {
                _recargas++;
                return Task.CompletedTask;
            });
    }

    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ParPropuestoDto Propuesto(string segundaPalabra, int idCategoria, int coincidencias, int total)
    {
        return new ParPropuestoDto
        {
            IdTermino = 1,
            Termino = "CUADERNO",
            SegundaPalabra = segundaPalabra,
            IdCategoria = idCategoria,
            NombreCategoria = "Cuadernos",
            Coincidencias = coincidencias,
            Total = total,
            Ejemplos = [$"CUADERNO {segundaPalabra} 48H"]
        };
    }

    [Fact]
    public void Mostrar_ConPropuestas_CargaLasFilasConSuEvidenciaYCategoriaPropuesta()
    {
        _sut.Mostrar(_categorias, _propuestos);

        _sut.Propuestas.Should().HaveCount(2);
        _sut.HayPropuestas.Should().BeTrue();
        _sut.Propuestas[0].Evidencia.Should().Be("8 de 8");
        _sut.Propuestas[0].CategoriaElegida.IdCategoria.Should().Be(10);
        _sut.Propuestas[0].Ejemplos.Should().Contain("TAPA");
    }

    [Fact]
    public void Mostrar_ConCategoriaPropuestaDadaDeBaja_DescartaEsaFila()
    {
        _sut.Mostrar(_categorias, [Propuesto("TAPA", 10, 8, 8), Propuesto("RAYADO", 99, 3, 3)]);

        _sut.Propuestas.Should().ContainSingle().Which.SegundaPalabra.Should().Be("TAPA");
    }

    [Fact]
    public void Mostrar_SinPropuestas_VaciaLasFilasAnteriores()
    {
        _sut.Mostrar(_categorias, _propuestos);

        _sut.Mostrar(_categorias, []);

        _sut.HayPropuestas.Should().BeFalse();
    }

    [Fact]
    public async Task Aceptar_ConLaCategoriaPropuesta_AsignaElParYRecarga()
    {
        _sut.Mostrar(_categorias, _propuestos);

        await _sut.AceptarCommand.ExecuteAsync(_sut.Propuestas[0]);

        await _terminoService.Received(1).AsignarParesAsync(
            IdActor,
            Arg.Is<AsignarParesDto>(d => d.Pares.Count == 1 &&
                d.Pares[0].SegundaPalabra == "TAPA" && d.Pares[0].IdCategoria == 10),
            Arg.Any<CancellationToken>());
        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task Aceptar_ConLaCategoriaCorregida_AsignaLaCategoriaElegida()
    {
        _sut.Mostrar(_categorias, _propuestos);
        _sut.Propuestas[0].CategoriaElegida = _sut.Categorias[1];

        await _sut.AceptarCommand.ExecuteAsync(_sut.Propuestas[0]);

        await _terminoService.Received(1).AsignarParesAsync(
            IdActor,
            Arg.Is<AsignarParesDto>(d => d.Pares[0].IdCategoria == 11),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Descartar_AsignaElParComoIgnoradoYRecarga()
    {
        _sut.Mostrar(_categorias, _propuestos);

        await _sut.DescartarCommand.ExecuteAsync(_sut.Propuestas[1]);

        await _terminoService.Received(1).AsignarParesAsync(
            IdActor,
            Arg.Is<AsignarParesDto>(d => d.Pares.Count == 1 &&
                d.Pares[0].SegundaPalabra == "ESCOLAR" && d.Pares[0].IdCategoria == null),
            Arg.Any<CancellationToken>());
        _recargas.Should().Be(1);
    }

    [Fact]
    public async Task AceptarSeleccionadas_ConDosMarcadas_AsignaAmbasEnUnaSolaLlamada()
    {
        _sut.Mostrar(_categorias, _propuestos);
        foreach (var propuesta in _sut.Propuestas)
        {
            propuesta.Seleccionado = true;
        }

        await _sut.AceptarSeleccionadasCommand.ExecuteAsync(null);

        await _terminoService.Received(1).AsignarParesAsync(
            IdActor,
            Arg.Is<AsignarParesDto>(d => d.Pares.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DescartarSeleccionadas_SinNingunaMarcada_NoLlamaAlServicio()
    {
        _sut.Mostrar(_categorias, _propuestos);

        await _sut.DescartarSeleccionadasCommand.ExecuteAsync(null);

        await _terminoService.DidNotReceiveWithAnyArgs().AsignarParesAsync(default, default!, default);
        _recargas.Should().Be(0);
    }

    [Fact]
    public async Task Aceptar_ConErrorDeDominio_MuestraElMensajeYNoRecarga()
    {
        _sut.Mostrar(_categorias, _propuestos);
        _terminoService.AsignarParesAsync(IdActor, Arg.Any<AsignarParesDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El término ya tiene categoría."));

        await _sut.AceptarCommand.ExecuteAsync(_sut.Propuestas[0]);

        _sut.MensajeError.Should().Be("El término ya tiene categoría.");
        _recargas.Should().Be(0);
    }

    [Fact]
    public void Comandos_SinPermisoDeClasificacion_QuedanDeshabilitados()
    {
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(false);
        _sut.Mostrar(_categorias, _propuestos);

        _sut.PuedeEscribir.Should().BeFalse();
        _sut.AceptarCommand.CanExecute(_sut.Propuestas[0]).Should().BeFalse();
        _sut.DescartarCommand.CanExecute(_sut.Propuestas[0]).Should().BeFalse();
        _sut.AceptarSeleccionadasCommand.CanExecute(null).Should().BeFalse();
        _sut.DescartarSeleccionadasCommand.CanExecute(null).Should().BeFalse();
    }
}
