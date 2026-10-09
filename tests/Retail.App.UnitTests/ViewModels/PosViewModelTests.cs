using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Ventas;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class PosViewModelTests
{
    private readonly IVentaService _ventaServiceMock;
    private readonly ICajaService _cajaServiceMock;
    private readonly IInventarioService _inventarioServiceMock;
    private readonly ICurrentUserSession _sessionMock;
    private readonly IVentaDialogService _dialogServiceMock;

    public PosViewModelTests()
    {
        _ventaServiceMock = Substitute.For<IVentaService>();
        _cajaServiceMock = Substitute.For<ICajaService>();
        _inventarioServiceMock = Substitute.For<IInventarioService>();
        _sessionMock = Substitute.For<ICurrentUserSession>();
        _dialogServiceMock = Substitute.For<IVentaDialogService>();

        _sessionMock.NombreCompleto.Returns("Juan Cajero");
        _sessionMock.IdUsuario.Returns(2);

        _cajaServiceMock.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(new TurnoCajaDto
        {
            IdTurno = 1,
            IdUsuario = 2,
            NombreUsuario = "cajero",
            FechaApertura = DateTime.UtcNow,
            SaldoInicial = 5000m,
            SaldoTeoricoEfectivo = 5000m,
            TotalVentasEfectivo = 0m,
            TotalIngresosEfectivo = 0m,
            TotalEgresosEfectivo = 0m,
            TotalVentasElectronicas = 0m,
            Estado = EstadoTurnoEnum.Abierto
        });

        ConfigurarVerificacion(VerificacionSinNovedades());
    }

    /// <summary>
    /// Crea el ViewModel y espera la lectura inicial del estado de la caja, que el constructor lanza en segundo
    /// plano: así ningún test puede ser pisado por ella después de asignar CajaAbierta.
    /// </summary>
    private async Task<PosViewModel> CrearViewModelAsync()
    {
        var viewModel = new PosViewModel(_ventaServiceMock, _cajaServiceMock, _inventarioServiceMock, _sessionMock, _dialogServiceMock);
        await viewModel.CargarEstadoCajaAsync().WaitAsync(TimeSpan.FromSeconds(5));
        return viewModel;
    }

    [Fact]
    public async Task ProcesarEnterAsync_ConArticuloResaltadoEnPopup_DebeAgregarAlTicketYCerrarPopup()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        var articulo = new ArticuloVentaDto
        {
            IdArticulo = 10,
            CodigoBarras = "7791234567011",
            Descripcion = "Cuaderno Rivadavia 48h",
            PrecioVenta = 4800m,
            StockActual = 20,
            EsServicio = false
        };

        viewModel.ResultadosBusqueda.Add(articulo);
        viewModel.IndiceResultadoSeleccionado = 0;
        viewModel.MostrarPopupBusqueda = true;
        viewModel.TextoBusqueda = "cuad";

        // Act
        await viewModel.ProcesarEnterAsync();

        // Assert
        viewModel.Items.Should().HaveCount(1);
        viewModel.Items[0].IdArticulo.Should().Be(10);
        viewModel.Items[0].Cantidad.Should().Be(1);
        viewModel.Items[0].SubtotalItem.Should().Be(4800m);
        viewModel.Total.Should().Be(4800m);
        viewModel.MostrarPopupBusqueda.Should().BeFalse();
        viewModel.TextoBusqueda.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcesarEnterAsync_TrasBajarConLaFlecha_AgregaElSegundoResultado()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.ResultadosBusqueda.Add(new ArticuloVentaDto { IdArticulo = 10, Descripcion = "Lápiz HB", PrecioVenta = 300m, StockActual = 5, EsServicio = false });
        viewModel.ResultadosBusqueda.Add(new ArticuloVentaDto { IdArticulo = 11, Descripcion = "Lápiz 2B", PrecioVenta = 350m, StockActual = 5, EsServicio = false });
        viewModel.IndiceResultadoSeleccionado = 0;
        viewModel.MostrarPopupBusqueda = true;

        // Act
        viewModel.MoverSeleccionPopupAbajo();
        await viewModel.ProcesarEnterAsync();

        // Assert
        viewModel.Items.Should().ContainSingle().Which.IdArticulo.Should().Be(11);
    }

    [Fact]
    public async Task CantidadTipeadaEnLaGrilla_RecalculaLosTotales()
    {
        // Arrange: 100 hojas sueltas, sin 100 clics en +
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 1, Descripcion = "Hoja A4", PrecioVenta = 50m, StockActual = 500, EsServicio = false });

        // Act: lo mismo que hace el binding del TextBox de la celda
        viewModel.Items[0].Cantidad = 100;

        // Assert
        viewModel.Total.Should().Be(5000m);
        viewModel.CantidadTotalArticulos.Should().Be(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CantidadMenorAUno_VuelveAlValorAnteriorSinQuitarLaFila(int cantidadInvalida)
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 1, Descripcion = "Hoja A4", PrecioVenta = 50m, StockActual = 500, EsServicio = false });
        viewModel.Items[0].Cantidad = 3;

        // Act
        viewModel.Items[0].Cantidad = cantidadInvalida;

        // Assert (D-25)
        viewModel.Items.Should().ContainSingle();
        viewModel.Items[0].Cantidad.Should().Be(3);
        viewModel.Total.Should().Be(150m);
    }

    [Fact]
    public async Task ItemQuitadoDelTicket_YaNoAfectaLosTotales()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 1, Descripcion = "Hoja A4", PrecioVenta = 50m, StockActual = 500, EsServicio = false });
        var quitado = viewModel.Items[0];
        viewModel.EliminarItem(quitado);

        // Act: el ViewModel se desuscribió del ítem al quitarlo
        quitado.Cantidad = 10;

        // Assert
        viewModel.Total.Should().Be(0m);
    }

    [Fact]
    public async Task TieneItems_ReflejaSiElTicketTieneArticulos()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        var cambios = new List<string?>();
        viewModel.PropertyChanged += (_, e) => cambios.Add(e.PropertyName);

        // Act + Assert: habilita y deshabilita el botón Limpiar venta
        viewModel.TieneItems.Should().BeFalse();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 1, Descripcion = "Hoja A4", PrecioVenta = 50m, StockActual = 500, EsServicio = false });
        viewModel.TieneItems.Should().BeTrue();
        cambios.Should().Contain(nameof(PosViewModel.TieneItems));
    }

    [Fact]
    public async Task MoverSeleccionPopup_EnLosExtremos_DaLaVuelta()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.ResultadosBusqueda.Add(new ArticuloVentaDto { IdArticulo = 10, Descripcion = "Lápiz HB", PrecioVenta = 300m, StockActual = 5, EsServicio = false });
        viewModel.ResultadosBusqueda.Add(new ArticuloVentaDto { IdArticulo = 11, Descripcion = "Lápiz 2B", PrecioVenta = 350m, StockActual = 5, EsServicio = false });
        viewModel.IndiceResultadoSeleccionado = 0;

        // Act + Assert
        viewModel.MoverSeleccionPopupArriba();
        viewModel.IndiceResultadoSeleccionado.Should().Be(1);
        viewModel.MoverSeleccionPopupAbajo();
        viewModel.IndiceResultadoSeleccionado.Should().Be(0);
    }

    [Fact]
    public async Task ProcesarEnterAsync_ConArticuloExistente_DebeIncrementarCantidadSinDuplicarFila()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        var articulo = new ArticuloVentaDto
        {
            IdArticulo = 10,
            CodigoBarras = "7791234567011",
            Descripcion = "Cuaderno Rivadavia 48h",
            PrecioVenta = 4800m,
            StockActual = 20,
            EsServicio = false
        };

        viewModel.AgregarArticuloAlTicket(articulo);

        viewModel.ResultadosBusqueda.Add(articulo);
        viewModel.IndiceResultadoSeleccionado = 0;
        viewModel.MostrarPopupBusqueda = true;

        // Act
        await viewModel.ProcesarEnterAsync();

        // Assert
        viewModel.Items.Should().HaveCount(1);
        viewModel.Items[0].Cantidad.Should().Be(2);
        viewModel.Items[0].SubtotalItem.Should().Be(9600m);
        viewModel.Total.Should().Be(9600m);
    }

    [Fact]
    public async Task ProcesarEnterAsync_ConLectorDeCodigosYBusquedaPredictivaEnCurso_EsperaSuTurnoYAgregaElArticulo()
    {
        // Arrange: el lector tipea el código (dispara la búsqueda predictiva) y manda Enter mientras ella consulta
        const string codigo = "7791234567011";
        var viewModel = await CrearViewModelAsync();
        var consultoSugerencias = new TaskCompletionSource();
        var sugerencias = new TaskCompletionSource<IReadOnlyList<ArticuloVentaDto>>();
        var buscoArticulo = false;

        _ventaServiceMock.BuscarPorCodigoBarrasAsync(codigo, Arg.Any<CancellationToken>()).Returns((ArticuloVentaDto?)null);
        _ventaServiceMock.BuscarPorTextoAsync(codigo, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                consultoSugerencias.TrySetResult();
                return sugerencias.Task;
            });
        _ventaServiceMock.BuscarArticuloParaVentaAsync(codigo, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                buscoArticulo = true;
                return new ArticuloVentaDto
                {
                    IdArticulo = 10,
                    CodigoBarras = codigo,
                    Descripcion = "Cuaderno Rivadavia 48h",
                    PrecioVenta = 4800m,
                    StockActual = 20,
                    EsServicio = false
                };
            });

        viewModel.TextoBusqueda = codigo;
        await consultoSugerencias.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        var enter = viewModel.ProcesarEnterAsync();
        await Task.Delay(100);
        var buscoAntesDeLiberar = buscoArticulo;
        sugerencias.SetException(new InvalidOperationException("Operation cancelled by user"));
        await enter.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        buscoAntesDeLiberar.Should().BeFalse("la búsqueda del artículo espera a que la predictiva libere el DbContext (H-19)");
        viewModel.Items.Should().ContainSingle().Which.IdArticulo.Should().Be(10);
        _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarError(default!, default!);
    }

    [Fact]
    public async Task IncrementarYDecrementarCantidad_DebeActualizarSubtotalesYTotales()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        var articulo = new ArticuloVentaDto
        {
            IdArticulo = 5,
            CodigoBarras = "7790001",
            Descripcion = "Regla 30cm",
            PrecioVenta = 1000m,
            StockActual = 10,
            EsServicio = false
        };
        viewModel.AgregarArticuloAlTicket(articulo);
        var item = viewModel.Items[0];

        // Act
        viewModel.IncrementarCantidad(item);

        // Assert
        item.Cantidad.Should().Be(2);
        viewModel.Total.Should().Be(2000m);

        // Act 2
        viewModel.DecrementarCantidad(item);

        // Assert 2
        item.Cantidad.Should().Be(1);
        viewModel.Total.Should().Be(1000m);
    }

    [Fact]
    public async Task EliminarItem_DebeQuitarFilaYRecalcularTotales()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
        {
            IdArticulo = 1,
            Descripcion = "Articulo 1",
            PrecioVenta = 1500m,
            StockActual = 10,
            EsServicio = false
        });
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
        {
            IdArticulo = 2,
            Descripcion = "Articulo 2",
            PrecioVenta = 2500m,
            StockActual = 10,
            EsServicio = false
        });

        viewModel.Items.Should().HaveCount(2);
        viewModel.Total.Should().Be(4000m);

        // Act
        viewModel.EliminarItem(viewModel.Items[0]);

        // Assert
        viewModel.Items.Should().HaveCount(1);
        viewModel.Items[0].IdArticulo.Should().Be(2);
        viewModel.Items[0].NumeroItem.Should().Be(1);
        viewModel.Total.Should().Be(2500m);
    }

    [Fact]
    public async Task LimpiarVenta_ConConfirmacionPositiva_DebeVaciarTicket()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
        {
            IdArticulo = 1,
            Descripcion = "Articulo 1",
            PrecioVenta = 3000m,
            StockActual = 5,
            EsServicio = false
        });

        _dialogServiceMock.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        // Act
        viewModel.LimpiarVenta();

        // Assert
        viewModel.Items.Should().BeEmpty();
        viewModel.Total.Should().Be(0m);
    }

    [Fact]
    public async Task CobrarVentaAsync_ConTicketVacio_NoDebeAbrirModalDeCobro()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.CajaAbierta = true;

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        await _dialogServiceMock.DidNotReceive().MostrarCobroModalAsync(Arg.Any<decimal>(), Arg.Any<ClienteDto?>());
    }

    [Fact]
    public async Task CobrarVentaAsync_ConCajaCerrada_DebeMostrarAlertaYNoCobrar()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.CajaAbierta = false;
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
        {
            IdArticulo = 1,
            Descripcion = "Libro",
            PrecioVenta = 5000m,
            StockActual = 10,
            EsServicio = false
        });

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        _dialogServiceMock.Received(1).MostrarAlerta(Arg.Is<string>(s => s.Contains("Caja")), Arg.Any<string>());
        await _dialogServiceMock.DidNotReceive().MostrarCobroModalAsync(Arg.Any<decimal>(), Arg.Any<ClienteDto?>());
    }

    [Fact]
    public async Task CobrarVentaAsync_ConCobroExitoso_DebeRegistrarVentaYVaciarTicket()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.CajaAbierta = true;
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto
        {
            IdArticulo = 1,
            Descripcion = "Libro",
            PrecioVenta = 5000m,
            StockActual = 10,
            EsServicio = false
        });

        var pagos = new List<PagoVentaDto>
        {
            new()
            {
                MedioPago = MedioPagoEnum.Efectivo,
                Monto = 5000m,
                MontoRecibido = 5000m,
                Vuelto = 0m
            }
        };

        _dialogServiceMock.MostrarCobroModalAsync(5000m, Arg.Any<ClienteDto?>())
            .Returns(pagos);

        _ventaServiceMock.RegistrarVentaAsync(Arg.Any<CrearVentaDto>(), Arg.Any<CancellationToken>())
            .Returns(new VentaResponseDto
            {
                IdVenta = 123,
                IdTurno = 1,
                IdUsuario = 2,
                FechaHora = DateTime.UtcNow,
                Subtotal = 5000m,
                Descuento = 0m,
                Total = 5000m,
                EstadoFiscal = EstadoFiscalEnum.NoAplica,
                Items = new List<DetalleVentaDto>(),
                Pagos = pagos
            });

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        await _ventaServiceMock.Received(1).RegistrarVentaAsync(Arg.Is<CrearVentaDto>(dto =>
            dto.Items.Count == 1 && dto.IdTurno == 1 && dto.IdUsuario == 2), Arg.Any<CancellationToken>());
        viewModel.Items.Should().BeEmpty();
        viewModel.Total.Should().Be(0m);
    }

    [Fact]
    public async Task CobrarVentaAsync_SinUsuarioEnSesion_MuestraAlertaYNoRegistraConUnUsuarioInventado()
    {
        // Arrange
        _sessionMock.IdUsuario.Returns((int?)null);
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Libro", 5000m));

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        _dialogServiceMock.Received(1).MostrarAlerta("Sesión incompleta", Arg.Any<string>());
        await _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarCobroModalAsync(default, default);
        await _ventaServiceMock.DidNotReceiveWithAnyArgs().RegistrarVentaAsync(default!, default);
    }

    [Fact]
    public async Task CobrarVentaAsync_FaltaStockDeUnArticuloDeCompra_MuestraAlertaYNoAbreElCobro()
    {
        // Arrange
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Libro", 5000m));
        ConfigurarVerificacion(VerificacionSinNovedades() with
        {
            Faltantes = [new FaltanteStockDto { IdArticulo = 1, Descripcion = "Libro", CantidadSolicitada = 1, StockActual = 0 }]
        });

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        _dialogServiceMock.Received(1).MostrarAlerta("Stock insuficiente", Arg.Is<string>(m => m.Contains("Libro")));
        _dialogServiceMock.DidNotReceiveWithAnyArgs().Confirmar(default!, default!);
        await _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarCobroModalAsync(default, default);
        viewModel.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task CobrarVentaAsync_FaltanSueltosYElCajeroAceptaFraccionar_FraccionaElMinimoYAbreElCobro()
    {
        // Arrange
        var viewModel = await CrearViewModelConTicketAsync(Articulo(20, "Sobre (unidad)", 50m, stock: 2));
        viewModel.Items[0].Cantidad = 5;
        ConfigurarVerificacion(VerificacionSinNovedades() with { Faltantes = [FaltanteDeSobres(stockOrigen: 4)] });
        _dialogServiceMock.Confirmar("Fraccionar presentación", Arg.Any<string>()).Returns(true);
        _inventarioServiceMock.FraccionarAsync(Arg.Any<FraccionarDto>(), Arg.Any<CancellationToken>()).Returns(100);

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        await _inventarioServiceMock.Received(1).FraccionarAsync(
            Arg.Is<FraccionarDto>(f => f.IdArticuloDerivado == 20 && f.CantidadOrigen == 1),
            Arg.Any<CancellationToken>());
        viewModel.Items[0].StockActual.Should().Be(102);
        await _dialogServiceMock.Received(1).MostrarCobroModalAsync(250m, Arg.Any<ClienteDto?>());
    }

    [Fact]
    public async Task CobrarVentaAsync_FaltanSueltosYElCajeroRechazaFraccionar_NoFraccionaNiAbreElCobro()
    {
        // Arrange
        var viewModel = await CrearViewModelConTicketAsync(Articulo(20, "Sobre (unidad)", 50m, stock: 2));
        viewModel.Items[0].Cantidad = 5;
        ConfigurarVerificacion(VerificacionSinNovedades() with { Faltantes = [FaltanteDeSobres(stockOrigen: 4)] });
        _dialogServiceMock.Confirmar("Fraccionar presentación", Arg.Any<string>()).Returns(false);

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        await _inventarioServiceMock.DidNotReceiveWithAnyArgs().FraccionarAsync(default!, default);
        await _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarCobroModalAsync(default, default);
    }

    [Fact]
    public async Task CobrarVentaAsync_PresentacionYArticuloSinSolucion_NoOfreceFraccionarNada()
    {
        // Arrange: si una venta no se puede cobrar igual, abrir un pack moverá stock sin motivo
        var viewModel = await CrearViewModelConTicketAsync(
            Articulo(20, "Sobre (unidad)", 50m, stock: 2),
            Articulo(1, "Libro", 5000m));
        ConfigurarVerificacion(VerificacionSinNovedades() with
        {
            Faltantes =
            [
                FaltanteDeSobres(stockOrigen: 4),
                new FaltanteStockDto { IdArticulo = 1, Descripcion = "Libro", CantidadSolicitada = 1, StockActual = 0 }
            ]
        });

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        _dialogServiceMock.Received(1).MostrarAlerta("Stock insuficiente", Arg.Any<string>());
        _dialogServiceMock.DidNotReceiveWithAnyArgs().Confirmar(default!, default!);
        await _inventarioServiceMock.DidNotReceiveWithAnyArgs().FraccionarAsync(default!, default);
    }

    [Fact]
    public async Task CobrarVentaAsync_PrecioCambiadoEnElCatalogo_ActualizaElTicketYCobraElNuevoTotal()
    {
        // Arrange
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Libro", 5000m));
        ConfigurarVerificacion(VerificacionSinNovedades() with
        {
            PreciosActualizados =
            [
                new PrecioActualizadoDto { IdArticulo = 1, Descripcion = "Libro", PrecioAnterior = 5000m, PrecioActual = 5500m }
            ]
        });

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        viewModel.Items[0].PrecioUnitario.Should().Be(5500m);
        _dialogServiceMock.Received(1).MostrarAlerta("Precios actualizados", Arg.Is<string>(m => m.Contains("Libro")));
        await _dialogServiceMock.Received(1).MostrarCobroModalAsync(5500m, Arg.Any<ClienteDto?>());
    }

    [Fact]
    public async Task CobrarVentaAsync_ArticuloDadoDeBaja_MuestraAlertaYNoAbreElCobro()
    {
        // Arrange
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Libro", 5000m));
        ConfigurarVerificacion(VerificacionSinNovedades() with { ArticulosNoDisponibles = ["Libro"] });

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        _dialogServiceMock.Received(1).MostrarAlerta("Artículos dados de baja", Arg.Is<string>(m => m.Contains("Libro")));
        await _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarCobroModalAsync(default, default);
    }

    [Fact]
    public async Task CobrarVentaAsync_ConflictoDeConcurrencia_MuestraAlertaYConservaElTicket()
    {
        // Arrange
        var viewModel = await CrearViewModelConTicketYPagoAsync();
        _ventaServiceMock
            .RegistrarVentaAsync(Arg.Any<CrearVentaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictoDeConcurrenciaException());

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        _dialogServiceMock.Received(1).MostrarAlerta("Datos actualizados por otra terminal", Arg.Any<string>());
        _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarError(default!, default!);
        viewModel.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task CobrarVentaAsync_ReglaDeNegocioRechazada_MuestraAlertaYNoUnErrorDelSistema()
    {
        // Arrange
        var viewModel = await CrearViewModelConTicketYPagoAsync();
        _ventaServiceMock
            .RegistrarVentaAsync(Arg.Any<CrearVentaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new LimiteCreditoExcedidoException("Límite de crédito excedido."));

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        _dialogServiceMock.Received(1).MostrarAlerta("Venta no registrada", "Límite de crédito excedido.");
        _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarError(default!, default!);
        viewModel.Items.Should().ContainSingle();
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task CobrarVentaAsync_ErrorInesperado_MuestraErrorYConservaElTicket()
    {
        // Arrange
        var viewModel = await CrearViewModelConTicketYPagoAsync();
        _ventaServiceMock
            .RegistrarVentaAsync(Arg.Any<CrearVentaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Se perdió la conexión"));

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        _dialogServiceMock.Received(1).MostrarError("Error de Cobro", "Se perdió la conexión");
        viewModel.Items.Should().ContainSingle();
    }

    private async Task<PosViewModel> CrearViewModelConTicketAsync(params ArticuloVentaDto[] articulos)
    {
        var viewModel = await CrearViewModelAsync();
        viewModel.CajaAbierta = true;
        foreach (var articulo in articulos)
        {
            viewModel.AgregarArticuloAlTicket(articulo);
        }

        return viewModel;
    }

    private async Task<PosViewModel> CrearViewModelConTicketYPagoAsync()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Libro", 5000m));
        _dialogServiceMock.MostrarCobroModalAsync(5000m, Arg.Any<ClienteDto?>())
            .Returns(new List<PagoVentaDto> { new() { MedioPago = MedioPagoEnum.Efectivo, Monto = 5000m } });
        return viewModel;
    }

    private static ArticuloVentaDto Articulo(int id, string descripcion, decimal precio, int stock = 10)
    {
        return new ArticuloVentaDto
        {
            IdArticulo = id,
            Descripcion = descripcion,
            PrecioVenta = precio,
            StockActual = stock,
            EsServicio = false
        };
    }

    private static FaltanteStockDto FaltanteDeSobres(int stockOrigen)
    {
        return new FaltanteStockDto
        {
            IdArticulo = 20,
            Descripcion = "Sobre (unidad)",
            CantidadSolicitada = 5,
            StockActual = 2,
            DescripcionOrigen = "Sobre (pack x100)",
            UnidadesPorOrigen = 100,
            StockOrigen = stockOrigen,
            OrigenesAFraccionar = 1
        };
    }

    private static VerificacionTicketDto VerificacionSinNovedades()
    {
        return new VerificacionTicketDto
        {
            PreciosActualizados = [],
            Faltantes = [],
            ArticulosNoDisponibles = []
        };
    }

    private void ConfigurarVerificacion(VerificacionTicketDto verificacion)
    {
        _ventaServiceMock
            .VerificarTicketAsync(Arg.Any<IReadOnlyList<DetalleVentaDto>>(), Arg.Any<CancellationToken>())
            .Returns(verificacion);
    }
}
