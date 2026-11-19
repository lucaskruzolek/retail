using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Presupuestos;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class DetallePresupuestoViewModelTests
{
    private const int IdPresupuesto = 123;

    private readonly IPresupuestoService _presupuestoService = Substitute.For<IPresupuestoService>();
    private readonly IPresupuestoDialogService _dialogService = Substitute.For<IPresupuestoDialogService>();
    private readonly DetallePresupuestoViewModel _sut;

    public DetallePresupuestoViewModelTests()
    {
        _sut = new DetallePresupuestoViewModel(
            IdPresupuesto,
            _presupuestoService,
            _dialogService,
            new RelojDePrueba(new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero)),
            NullLogger<DetallePresupuestoViewModel>.Instance);
    }

    private static PresupuestoDto Presupuesto(
        string? titulo = "Lista escolar",
        string? cliente = "Escuela 12",
        decimal descuento = 500m,
        EstadoPresupuestoEnum estado = EstadoPresupuestoEnum.Pendiente,
        bool vigente = true)
    {
        return new PresupuestoDto
        {
            IdPresupuesto = IdPresupuesto,
            IdUsuario = 2,
            UsuarioNombre = "Lucía",
            ClienteNombre = cliente,
            Titulo = titulo,
            FechaEmision = new DateTime(2026, 11, 14, 13, 20, 0, DateTimeKind.Utc),
            FechaVencimiento = new DateOnly(2026, 11, 29),
            Subtotal = 10500m,
            Descuento = descuento,
            Total = 10500m - descuento,
            Estado = estado,
            EstaVigente = vigente,
            Items =
            [
                new DetallePresupuestoDto { IdArticulo = 1, Descripcion = "Cuaderno", Cantidad = 3, PrecioUnitarioPactado = 2500m },
                new DetallePresupuestoDto { IdArticulo = 2, Descripcion = "Lapicera", Cantidad = 6, PrecioUnitarioPactado = 500m }
            ]
        };
    }

    [Fact]
    public async Task InicializarAsync_PresupuestoExistente_MuestraCabeceraRenglonesYTotales()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());

        await _sut.InicializarAsync();

        _sut.Cargado.Should().BeTrue();
        _sut.IsBusy.Should().BeFalse();
        _sut.Numero.Should().Be("00000123");
        _sut.TituloVentana.Should().Be("Presupuesto N.º 00000123");
        _sut.Titulo.Should().Be("Lista escolar");
        _sut.TieneTitulo.Should().BeTrue();
        _sut.ClienteTexto.Should().Be("Escuela 12");
        _sut.UsuarioNombre.Should().Be("Lucía");
        _sut.EmisionLocal.Should().Be(new DateTime(2026, 11, 14, 10, 20, 0));
        _sut.Vencimiento.Should().Be(new DateTime(2026, 11, 29));
        _sut.EstadoVisual.Should().Be(EstadoVisualPresupuesto.Vigente);
        _sut.EstadoTexto.Should().Be("Vigente");
        _sut.Renglones.Should().HaveCount(2);
        _sut.Renglones[0].SubtotalItem.Should().Be(7500m);
        _sut.Subtotal.Should().Be(10500m);
        _sut.Descuento.Should().Be(500m);
        _sut.TieneDescuento.Should().BeTrue();
        _sut.Total.Should().Be(10000m);
    }

    [Fact]
    public async Task InicializarAsync_SinTituloNiClienteNiDescuento_OcultaElTituloYUsaConsumidorFinal()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>())
            .Returns(Presupuesto(titulo: null, cliente: null, descuento: 0m, estado: EstadoPresupuestoEnum.Pendiente, vigente: false));

        await _sut.InicializarAsync();

        _sut.TieneTitulo.Should().BeFalse();
        _sut.ClienteTexto.Should().Be("Consumidor Final");
        _sut.TieneDescuento.Should().BeFalse("el descuento solo se muestra si es mayor a cero");
        _sut.EstadoTexto.Should().Be("Vencido");
    }

    [Fact]
    public async Task InicializarAsync_PresupuestoInexistente_InformaYNoHabilitaLasAcciones()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns((PresupuestoDto?)null);

        await _sut.InicializarAsync();

        _sut.Cargado.Should().BeFalse();
        _sut.TieneMensajeError.Should().BeTrue();
        _sut.ImprimirCommand.CanExecute(null).Should().BeFalse();
        _sut.ExportarPdfCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task InicializarAsync_ElServicioFalla_MuestraUnErrorSinCerrarElDialogo()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("sin conexión"));

        await _sut.InicializarAsync();

        _sut.TieneMensajeError.Should().BeTrue();
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Imprimir_ConElDetalleCargado_ImprimeYAvisa()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());
        await _sut.InicializarAsync();

        await _sut.ImprimirCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).ImprimirAsync(IdPresupuesto, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Presupuesto impreso", Arg.Is<string>(m => m.Contains("00000123")));
    }

    [Fact]
    public async Task ExportarPdf_ElArchivoEstaBloqueado_ReintentaConLaMismaRuta()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());
        await _sut.InicializarAsync();
        var ruta = Path.Combine(Path.GetTempPath(), $"detalle-presupuesto-{Guid.NewGuid():N}.pdf");
        _dialogService.ElegirRutaPdf("Presupuesto-00000123.pdf").Returns(ruta);
        _presupuestoService
            .When(s => s.ExportarPdfAsync(IdPresupuesto, Arg.Any<Stream>(), Arg.Any<CancellationToken>()))
            .Do(ci => ci.Arg<Stream>().Write("%PDF-1.7"u8));
        var bloqueo = new FileStream(ruta, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        _dialogService.Confirmar("No se pudo guardar el PDF", Arg.Any<string>()).Returns(_ =>
        {
            bloqueo.Dispose();
            return true;
        });

        try
        {
            await _sut.ExportarPdfCommand.ExecuteAsync(null);

            File.ReadAllText(ruta).Should().Be("%PDF-1.7");
            _dialogService.Received(1).ElegirRutaPdf(Arg.Any<string>());
            _dialogService.Received(1).MostrarInformacion("PDF guardado", Arg.Any<string>());
        }
        finally
        {
            bloqueo.Dispose();
            File.Delete(ruta);
        }
    }

    [Fact]
    public async Task ExportarPdf_ElUsuarioCancelaLaRuta_NoLlamaAlServicio()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());
        await _sut.InicializarAsync();
        _dialogService.ElegirRutaPdf(Arg.Any<string>()).Returns((string?)null);

        await _sut.ExportarPdfCommand.ExecuteAsync(null);

        await _presupuestoService.DidNotReceiveWithAnyArgs().ExportarPdfAsync(default, default!, default);
    }

    // ------------------------------------------------------------------ //
    // Título editable y cadena de correcciones (módulo 5.4)               //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task GuardarTitulo_SinCambios_NoSePuedeEjecutar()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());
        await _sut.InicializarAsync();

        _sut.GuardarTituloCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task GuardarTitulo_ConElTituloModificado_LlamaAlServicioYMarcaElCambio()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());
        await _sut.InicializarAsync();
        _sut.Titulo = "  Lista escolar 4.º grado  ";

        _sut.GuardarTituloCommand.CanExecute(null).Should().BeTrue();
        await _sut.GuardarTituloCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).CambiarTituloAsync(IdPresupuesto, "Lista escolar 4.º grado", Arg.Any<CancellationToken>());
        _sut.Titulo.Should().Be("Lista escolar 4.º grado");
        _sut.TituloCambiado.Should().BeTrue();
        _sut.TieneMensajeError.Should().BeFalse();
        _sut.GuardarTituloCommand.CanExecute(null).Should().BeFalse("ya no hay cambios pendientes");
    }

    [Fact]
    public async Task GuardarTitulo_TituloVacio_LoBorra()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());
        await _sut.InicializarAsync();
        _sut.Titulo = "   ";

        await _sut.GuardarTituloCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).CambiarTituloAsync(IdPresupuesto, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GuardarTitulo_PresupuestoYaConvertido_FuncionaEnCualquierEstado()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>())
            .Returns(Presupuesto(estado: EstadoPresupuestoEnum.Convertido));
        await _sut.InicializarAsync();
        _sut.Titulo = "Pedido de la oficina";

        await _sut.GuardarTituloCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).CambiarTituloAsync(IdPresupuesto, "Pedido de la oficina", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GuardarTitulo_ElDominioRechaza_MuestraElErrorEnElDialogoYNoMarcaElCambio()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());
        _presupuestoService.CambiarTituloAsync(IdPresupuesto, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El título del presupuesto supera los 100 caracteres."));
        await _sut.InicializarAsync();
        _sut.Titulo = "Otro";

        await _sut.GuardarTituloCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Contain("supera los 100");
        _sut.TituloCambiado.Should().BeFalse();
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task InicializarAsync_PresupuestoQueReemplazaYFueReemplazado_MuestraLaCadena()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>())
            .Returns(Presupuesto(estado: EstadoPresupuestoEnum.Reemplazado) with { IdPresupuestoReemplazado = 41, IdPresupuestoReemplazante = 45 });

        await _sut.InicializarAsync();

        _sut.EstadoTexto.Should().Be("Reemplazado");
        _sut.TieneReemplazo.Should().BeTrue();
        _sut.TextoReemplazo.Should().Be("Reemplaza al N.º 00000041 · Reemplazado por N.º 00000045");
    }

    [Fact]
    public async Task InicializarAsync_PresupuestoSinCorreccion_NoMuestraLaCadena()
    {
        _presupuestoService.ObtenerAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(Presupuesto());

        await _sut.InicializarAsync();

        _sut.TieneReemplazo.Should().BeFalse();
    }
}
