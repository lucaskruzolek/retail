using System.IO;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Presupuestos;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class GuardarPresupuestoViewModelTests : IDisposable
{
    private readonly IPresupuestoService _presupuestoService = Substitute.For<IPresupuestoService>();
    private readonly IPresupuestoDialogService _dialogService = Substitute.For<IPresupuestoDialogService>();
    private readonly CargaSerializada _acceso = new();

    public void Dispose()
    {
        _acceso.Dispose();
        GC.SuppressFinalize(this);
    }

    private GuardarPresupuestoViewModel CrearViewModel(string? clienteNombre = null, int? idCliente = null, decimal descuento = 0m)
    {
        return new GuardarPresupuestoViewModel(
            idUsuario: 2,
            idCliente,
            clienteNombre,
            descuento,
            total: 6000m,
            items:
            [
                new DetallePresupuestoDto { IdArticulo = 1, Descripcion = "Libro", Cantidad = 2, PrecioUnitarioPactado = 3000m }
            ],
            _presupuestoService,
            _dialogService,
            _acceso,
            NullLogger.Instance);
    }

    private static PresupuestoDto Emitido(int id = 123)
    {
        return new PresupuestoDto
        {
            IdPresupuesto = id,
            IdUsuario = 2,
            FechaEmision = new DateTime(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc),
            FechaVencimiento = new DateOnly(2026, 12, 2),
            Subtotal = 6000m,
            Descuento = 0m,
            Total = 6000m,
            Estado = EstadoPresupuestoEnum.Pendiente,
            EstaVigente = true,
            Items = []
        };
    }

    [Fact]
    public void Constructor_ValoresIniciales_SonLosDelPlan()
    {
        var sut = CrearViewModel();

        sut.ClienteNombre.Should().Be("Consumidor Final");
        sut.TotalFormateado.Should().Contain("6.000");
        sut.Titulo.Should().BeEmpty();
        sut.VigenciaDias.Should().Be(Presupuesto.VigenciaPorDefectoDias);
        GuardarPresupuestoViewModel.VigenciaMinimaDias.Should().Be(1);
        GuardarPresupuestoViewModel.VigenciaMaximaDias.Should().Be(90);
        GuardarPresupuestoViewModel.LongitudMaximaTitulo.Should().Be(100);
        sut.EstaEditando.Should().BeTrue();
        sut.Guardado.Should().BeFalse();
        sut.ImprimirCommand.CanExecute(null).Should().BeFalse("todavía no hay un presupuesto guardado");
        sut.ExportarPdfCommand.CanExecute(null).Should().BeFalse();
    }

    private GuardarPresupuestoViewModel CrearViewModelDesdeBase(int? numeroBase, string? tituloBase, int preciosActualizados)
    {
        return new GuardarPresupuestoViewModel(
            idUsuario: 2,
            idCliente: null,
            clienteNombre: null,
            descuento: 0m,
            total: 6000m,
            items: [new DetallePresupuestoDto { IdArticulo = 1, Descripcion = "Libro", Cantidad = 2, PrecioUnitarioPactado = 3000m }],
            _presupuestoService,
            _dialogService,
            _acceso,
            NullLogger.Instance,
            numeroBase,
            tituloBase,
            preciosActualizados);
    }

    [Fact]
    public void Constructor_SinBase_NoMuestraAvisos()
    {
        var sut = CrearViewModel();

        sut.AvisoBase.Should().BeNull();
        sut.AvisoPrecios.Should().BeNull();
        sut.TieneAvisoBase.Should().BeFalse();
        sut.TieneAvisoPrecios.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ConBaseYPreciosCambiados_AvisaElNumeroDelBaseLaCantidadYPrecargaElTitulo()
    {
        var sut = CrearViewModelDesdeBase(123, "Lista escolar", 3);

        sut.AvisoBase.Should().Be("Se emite un presupuesto nuevo; el N.º 00000123 no cambia.");
        sut.AvisoPrecios.Should().Be("3 precios pasan al catálogo de hoy.");
        sut.TieneAvisoBase.Should().BeTrue();
        sut.TieneAvisoPrecios.Should().BeTrue();
        sut.Titulo.Should().Be("Lista escolar");
    }

    [Fact]
    public void Constructor_ConBaseYUnSoloPrecioCambiado_UsaElSingular()
    {
        CrearViewModelDesdeBase(5, null, 1).AvisoPrecios.Should().Be("1 precio pasa al catálogo de hoy.");
    }

    [Fact]
    public void Constructor_ConBaseSinPreciosCambiados_NoMuestraElAvisoDePrecios()
    {
        var sut = CrearViewModelDesdeBase(5, null, 0);

        sut.TieneAvisoBase.Should().BeTrue();
        sut.TieneAvisoPrecios.Should().BeFalse();
        sut.Titulo.Should().BeEmpty();
    }

    [Fact]
    public async Task Guardar_ConBase_EmiteUnoNuevoYNoUsaReemplazar()
    {
        var sut = CrearViewModelDesdeBase(123, "Base", 0);
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>()).Returns(Emitido(124));

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.Guardado.Should().BeTrue();
        await _presupuestoService.DidNotReceiveWithAnyArgs().ReemplazarAsync(default, default!, default);
    }

    [Fact]
    public void Constructor_ConCliente_MuestraSuNombre()
    {
        CrearViewModel("Escuela N.º 12", 7).ClienteNombre.Should().Be("Escuela N.º 12");
    }

    [Fact]
    public async Task Guardar_ConDatosValidos_EmiteElDtoYPasaAlEstadoGuardado()
    {
        // Arrange
        var sut = CrearViewModel(idCliente: 7, descuento: 250m);
        sut.Titulo = "  Lista del colegio  ";
        sut.VigenciaDias = 30;
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>()).Returns(Emitido());

        // Act
        await sut.GuardarCommand.ExecuteAsync(null);

        // Assert
        await _presupuestoService.Received(1).EmitirAsync(
            Arg.Is<CrearPresupuestoDto>(dto =>
                dto.IdUsuario == 2
                && dto.IdCliente == 7
                && dto.Titulo == "Lista del colegio"
                && dto.VigenciaDias == 30
                && dto.Descuento == 250m
                && dto.Items.Count == 1
                && dto.Items[0].PrecioUnitarioPactado == 3000m),
            Arg.Any<CancellationToken>());
        sut.Guardado.Should().BeTrue();
        sut.EstaEditando.Should().BeFalse();
        sut.TextoGuardado.Should().Be("Presupuesto N.º 00000123 guardado · válido hasta 02/12/2026");
        sut.TieneMensajeError.Should().BeFalse();
        sut.IsBusy.Should().BeFalse();
        sut.GuardarCommand.CanExecute(null).Should().BeFalse("no se puede guardar dos veces el mismo presupuesto");
        sut.ImprimirCommand.CanExecute(null).Should().BeTrue();
        sut.ExportarPdfCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Guardar_SinTitulo_EnviaTituloNulo()
    {
        var sut = CrearViewModel();
        sut.Titulo = "   ";
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>()).Returns(Emitido());

        await sut.GuardarCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).EmitirAsync(
            Arg.Is<CrearPresupuestoDto>(dto => dto.Titulo == null && dto.VigenciaDias == Presupuesto.VigenciaPorDefectoDias),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_SinVigencia_PideCompletarlaYNoLlamaAlServicio()
    {
        var sut = CrearViewModel();
        sut.VigenciaDias = null;

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.TieneMensajeError.Should().BeTrue();
        sut.Guardado.Should().BeFalse();
        await _presupuestoService.DidNotReceiveWithAnyArgs().EmitirAsync(default!, default);
    }

    [Fact]
    public async Task Guardar_ElPrecioCambio_MuestraElErrorDentroDelDialogoYPermiteReintentar()
    {
        // Arrange: regla de dominio, no una falla del sistema (D-11, D-14)
        var sut = CrearViewModel();
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El precio de 'Libro' cambió: ahora es $3.200,00."));

        // Act
        await sut.GuardarCommand.ExecuteAsync(null);

        // Assert
        sut.MensajeError.Should().Be("El precio de 'Libro' cambió: ahora es $3.200,00.");
        sut.Guardado.Should().BeFalse("el diálogo no se cierra ni pasa al estado guardado");
        sut.IsBusy.Should().BeFalse();
        sut.GuardarCommand.CanExecute(null).Should().BeTrue();
        _dialogService.DidNotReceiveWithAnyArgs().MostrarError(default!, default!);
    }

    [Fact]
    public async Task Guardar_ValidacionDeFrontera_MuestraLosMensajes()
    {
        var sut = CrearViewModel();
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException([new ValidationFailure("VigenciaDias", "La vigencia debe estar entre 1 y 90 días.")]));

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Be("La vigencia debe estar entre 1 y 90 días.");
        sut.Guardado.Should().BeFalse();
    }

    [Fact]
    public async Task Guardar_ErrorInesperado_MuestraUnMensajeGenericoSinElDetalleTecnico()
    {
        var sut = CrearViewModel();
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Timeout de SQL"));

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.TieneMensajeError.Should().BeTrue();
        sut.MensajeError.Should().NotContain("Timeout");
        sut.Guardado.Should().BeFalse();
    }

    [Fact]
    public async Task Imprimir_ConElPresupuestoGuardado_ImprimeYAvisa()
    {
        var sut = CrearViewModel();
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>()).Returns(Emitido());
        await sut.GuardarCommand.ExecuteAsync(null);

        await sut.ImprimirCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).ImprimirAsync(123, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Presupuesto impreso", Arg.Is<string>(m => m.Contains("00000123")));
    }

    [Fact]
    public async Task Imprimir_FallaLaImpresora_AvisaSinPerderElEstadoGuardado()
    {
        var sut = CrearViewModel();
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>()).Returns(Emitido());
        await sut.GuardarCommand.ExecuteAsync(null);
        _presupuestoService.ImprimirAsync(123, Arg.Any<CancellationToken>()).ThrowsAsync(new DomainException("La impresora no responde."));

        await sut.ImprimirCommand.ExecuteAsync(null);

        _dialogService.Received(1).MostrarAlerta("No se pudo completar la operación", "La impresora no responde.");
        sut.Guardado.Should().BeTrue();
        sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ExportarPdf_ConElPresupuestoGuardado_EscribeElArchivoEnLaRutaElegida()
    {
        var sut = CrearViewModel();
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>()).Returns(Emitido());
        await sut.GuardarCommand.ExecuteAsync(null);
        var ruta = Path.Combine(Path.GetTempPath(), $"guardar-presupuesto-{Guid.NewGuid():N}.pdf");
        _dialogService.ElegirRutaPdf("Presupuesto-00000123.pdf").Returns(ruta);
        _presupuestoService
            .When(s => s.ExportarPdfAsync(123, Arg.Any<Stream>(), Arg.Any<CancellationToken>()))
            .Do(ci => ci.Arg<Stream>().Write("%PDF-1.7"u8));

        try
        {
            await sut.ExportarPdfCommand.ExecuteAsync(null);

            File.ReadAllText(ruta).Should().Be("%PDF-1.7");
            _dialogService.Received(1).MostrarInformacion("PDF guardado", Arg.Any<string>());
        }
        finally
        {
            File.Delete(ruta);
        }
    }
}
