using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Listas;
using Retail.Application.DTOs.Listas;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ListasViewModelTests : IDisposable
{
    private const int IdLista = 7;

    private readonly IListaService _listaService = Substitute.For<IListaService>();
    private readonly IListaDialogService _dialogService = Substitute.For<IListaDialogService>();
    private readonly ListasViewModel _sut;

    public ListasViewModelTests()
    {
        PrepararListado(Resumen(IdLista, "3.º grado — Colegio San Martín"));

        _sut = new ListasViewModel(
            _listaService,
            _dialogService,
            new RelojDePrueba(new DateTimeOffset(2026, 11, 19, 15, 0, 0, TimeSpan.Zero)),
            NullLogger<ListasViewModel>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ListaResumenDto Resumen(int id, string nombre)
    {
        return new ListaResumenDto
        {
            IdLista = id,
            Nombre = nombre,
            CantidadArticulos = 4,
            TotalEstimado = 18500m,
            CantidadVentas = 2,
            FechaModificacion = new DateTime(2026, 11, 18, 2, 30, 0, DateTimeKind.Utc)
        };
    }

    private static ListaDto Detalle(int id, string nombre)
    {
        return new ListaDto
        {
            IdLista = id,
            Nombre = nombre,
            FechaModificacion = new DateTime(2026, 11, 18, 2, 30, 0, DateTimeKind.Utc),
            Renglones =
            [
                new RenglonListaDto { IdArticulo = 1, CodigoBarras = "7790001", Descripcion = "Cuaderno", Cantidad = 2, PrecioActual = 900m, Subtotal = 1800m, EstaDisponible = true }
            ],
            TotalEstimado = 1800m
        };
    }

    private void PrepararListado(params ListaResumenDto[] items)
    {
        PrepararListado(items, items.Length);
    }

    private void PrepararListado(ListaResumenDto[] items, int totalRegistros)
    {
        _listaService.ListarAsync(Arg.Any<ConsultaListasDto>(), Arg.Any<CancellationToken>()).Returns(new ListasPaginadosDto
        {
            Items = items,
            TotalRegistros = totalRegistros,
            PaginaActual = 1,
            TamanoPagina = 15
        });
    }

    private int LlamadasAlListado => _listaService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IListaService.ListarAsync));

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        for (int i = 0; i < 100 && !condicion(); i++)
        {
            await Task.Delay(20);
        }

        condicion().Should().BeTrue("la condición debía cumplirse tras la carga asíncrona");
    }

    [Fact]
    public async Task InicializarAsync_CargaLaPrimeraPagina_MuestraLasFilasConSusDatos()
    {
        await _sut.InicializarAsync();

        var fila = _sut.Listas.Should().ContainSingle().Subject;
        fila.IdLista.Should().Be(IdLista);
        fila.Nombre.Should().Be("3.º grado — Colegio San Martín");
        fila.CantidadArticulos.Should().Be(4);
        fila.TotalDeHoy.Should().Be(18500m);
        fila.CantidadVentas.Should().Be(2);
        fila.ModificadaLocal.Should().Be(new DateTime(2026, 11, 17, 23, 30, 0), "la fecha se muestra en la hora local (UTC-3)");
        _sut.TotalRegistros.Should().Be(1);
        _sut.IsBusy.Should().BeFalse();
        await _listaService.Received(1).ListarAsync(
            Arg.Is<ConsultaListasDto>(c => c.Pagina == 1 && c.TamanoPagina == 15 && string.IsNullOrEmpty(c.TerminoBusqueda)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InicializarAsync_SinListas_MuestraElEstadoVacioConLaOrientacion()
    {
        PrepararListado();

        await _sut.InicializarAsync();

        _sut.EstaVacio.Should().BeTrue();
        _sut.TextoVacio.Should().Contain("Nueva lista");
    }

    [Fact]
    public async Task InicializarAsync_ElServicioFalla_MuestraElErrorYNoElEstadoVacio()
    {
        _listaService.ListarAsync(Arg.Any<ConsultaListasDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("sin conexión"));

        await _sut.InicializarAsync();

        _sut.TieneMensajeError.Should().BeTrue();
        _sut.EstaVacio.Should().BeFalse();
    }

    [Fact]
    public async Task TextoBusqueda_AlCambiar_VuelveALaPrimeraPaginaYConsultaPorNombre()
    {
        PrepararListado(Array.Empty<ListaResumenDto>(), totalRegistros: 40);
        await _sut.InicializarAsync();
        await _sut.IrAPaginaAsync(3);
        _sut.PaginaActual.Should().Be(3);

        _sut.TextoBusqueda = "grado";

        await EsperarAsync(() => _listaService.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(IListaService.ListarAsync)
            && c.GetArguments()[0] is ConsultaListasDto consulta
            && consulta.TerminoBusqueda == "grado"
            && consulta.Pagina == 1));
        _sut.PaginaActual.Should().Be(1);
        _sut.HayFiltrosActivos.Should().BeTrue();
    }

    [Fact]
    public async Task LimpiarFiltros_ConBusquedaActiva_VaciaElTexto()
    {
        await _sut.InicializarAsync();
        _sut.TextoBusqueda = "grado";

        _sut.LimpiarFiltrosCommand.Execute(null);

        _sut.TextoBusqueda.Should().BeEmpty();
        _sut.LimpiarFiltrosCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Eliminar_ConfirmaElUsuario_DaDeBajaYRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.ConfirmarBaja(Arg.Any<string>()).Returns(true);

        await _sut.EliminarCommand.ExecuteAsync(_sut.Listas.Single());

        _dialogService.Received(1).ConfirmarBaja("3.º grado — Colegio San Martín");
        await _listaService.Received(1).EliminarAsync(IdLista, Arg.Any<CancellationToken>());
        LlamadasAlListado.Should().Be(2, "tras la baja se vuelve a consultar la página");
    }

    [Fact]
    public async Task Eliminar_CancelaElUsuario_NoLlamaAlServicioNiRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.ConfirmarBaja(Arg.Any<string>()).Returns(false);

        await _sut.EliminarCommand.ExecuteAsync(_sut.Listas.Single());

        await _listaService.DidNotReceiveWithAnyArgs().EliminarAsync(default, default);
        LlamadasAlListado.Should().Be(1);
    }

    [Fact]
    public async Task Eliminar_OtraTerminalYaLaDioDeBaja_InformaYRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.ConfirmarBaja(Arg.Any<string>()).Returns(true);
        _listaService.EliminarAsync(IdLista, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("No se encontró la lista 7."));

        await _sut.EliminarCommand.ExecuteAsync(_sut.Listas.Single());

        _dialogService.Received(1).MostrarAlerta(Arg.Any<string>(), "No se encontró la lista 7.");
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task Editar_LeeLaListaYAbreElEditor_RecargaSoloSiSeGuardo()
    {
        await _sut.InicializarAsync();
        var detalle = Detalle(IdLista, "3.º grado — Colegio San Martín");
        _listaService.ObtenerAsync(IdLista, Arg.Any<CancellationToken>()).Returns(detalle);
        _dialogService.AbrirEditor(detalle, Arg.Any<CargaSerializada>()).Returns(true);

        await _sut.EditarCommand.ExecuteAsync(_sut.Listas.Single());

        _dialogService.Received(1).AbrirEditor(detalle, Arg.Any<CargaSerializada>());
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task Editar_ElUsuarioCancelaElEditor_NoRecarga()
    {
        await _sut.InicializarAsync();
        var detalle = Detalle(IdLista, "3.º grado — Colegio San Martín");
        _listaService.ObtenerAsync(IdLista, Arg.Any<CancellationToken>()).Returns(detalle);
        _dialogService.AbrirEditor(detalle, Arg.Any<CargaSerializada>()).Returns(false);

        await _sut.EditarCommand.ExecuteAsync(_sut.Listas.Single());

        LlamadasAlListado.Should().Be(1);
    }

    [Fact]
    public async Task Editar_LaListaYaNoExiste_InformaYRecargaSinAbrirElEditor()
    {
        await _sut.InicializarAsync();
        _listaService.ObtenerAsync(IdLista, Arg.Any<CancellationToken>()).Returns((ListaDto?)null);

        await _sut.EditarCommand.ExecuteAsync(_sut.Listas.Single());

        _dialogService.Received(1).MostrarAlerta("La lista ya no existe", Arg.Any<string>());
        _dialogService.DidNotReceiveWithAnyArgs().AbrirEditor(default, default!);
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task NuevaLista_SeGuardaEnElEditor_AbreSinListaYRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirEditor(null, Arg.Any<CargaSerializada>()).Returns(true);

        await _sut.NuevaListaCommand.ExecuteAsync(null);

        _dialogService.Received(1).AbrirEditor(null, Arg.Any<CargaSerializada>());
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task Imprimir_ConUnaFila_EnviaLaListaALaImpresoraEInforma()
    {
        await _sut.InicializarAsync();

        await _sut.ImprimirCommand.ExecuteAsync(_sut.Listas.Single());

        await _listaService.Received(1).ImprimirAsync(IdLista, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Lista impresa", Arg.Is<string>(m => m.Contains("3.º grado")));
    }

    [Fact]
    public async Task ExportarPdf_ElUsuarioCancelaLaRuta_NoLlamaAlServicio()
    {
        await _sut.InicializarAsync();
        _dialogService.ElegirRutaPdf(Arg.Any<string>()).Returns((string?)null);

        await _sut.ExportarPdfCommand.ExecuteAsync(_sut.Listas.Single());

        _dialogService.Received(1).ElegirRutaPdf("Lista-3.º grado — Colegio San Martín.pdf");
        await _listaService.DidNotReceiveWithAnyArgs().ExportarPdfAsync(default, default!, default);
    }

    [Theory]
    [InlineData("1.º año / Escuela: Técnica", "Lista-1.º año - Escuela- Técnica.pdf")]
    [InlineData("  Jardín sala de 5  ", "Lista-Jardín sala de 5.pdf")]
    public void NombreSugerido_ConCaracteresInvalidos_LosReemplazaPorGuion(string nombre, string esperado)
    {
        FlujoExportarPdfLista.NombreSugerido(nombre).Should().Be(esperado);
    }
}
