using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.UnitTests.Snapshots;
using Retail.App.ViewModels.Listas;
using Retail.App.ViewModels.Presupuestos;
using Retail.App.ViewModels.Ventas;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Listas;
using Retail.Application.DTOs.Presupuestos;
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
    private readonly ICajaDialogService _cajaDialogServiceMock;
    private readonly IPresupuestoService _presupuestoServiceMock;
    private readonly IPresupuestoDialogService _presupuestoDialogMock;
    private readonly IClienteService _clienteServiceMock;
    private readonly IListaService _listaServiceMock;
    private readonly IListaDialogService _listaDialogMock;

    public PosViewModelTests()
    {
        _listaServiceMock = Substitute.For<IListaService>();
        _listaDialogMock = Substitute.For<IListaDialogService>();
        _presupuestoServiceMock = Substitute.For<IPresupuestoService>();
        _presupuestoDialogMock = Substitute.For<IPresupuestoDialogService>();
        _clienteServiceMock = Substitute.For<IClienteService>();
        _ventaServiceMock = Substitute.For<IVentaService>();
        _cajaServiceMock = Substitute.For<ICajaService>();
        _inventarioServiceMock = Substitute.For<IInventarioService>();
        _sessionMock = Substitute.For<ICurrentUserSession>();
        _dialogServiceMock = Substitute.For<IVentaDialogService>();
        _cajaDialogServiceMock = Substitute.For<ICajaDialogService>();

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
        var viewModel = CrearViewModel();
        await viewModel.CargarEstadoCajaAsync().WaitAsync(TimeSpan.FromSeconds(5));
        return viewModel;
    }

    private PosViewModel CrearViewModel()
    {
        return new PosViewModel(
            _ventaServiceMock,
            _cajaServiceMock,
            _inventarioServiceMock,
            _sessionMock,
            _dialogServiceMock,
            _cajaDialogServiceMock,
            _presupuestoServiceMock,
            _presupuestoDialogMock,
            _clienteServiceMock,
            _listaServiceMock,
            _listaDialogMock);
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

        // Act: lo mismo que hace el SelectorCantidad de la celda (al tipear o con − y +)
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
    public async Task ProcesarEnterAsync_BuscadorVacioConItems_NoAbreElCobro()
    {
        // Decisión: Enter es la tecla del lector de códigos y no cobra; el cobro es solo F12
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Hoja A4", 50m, stock: 500));
        ConfigurarVerificacion(VerificacionSinNovedades());

        await viewModel.ProcesarEnterAsync();

        await _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarCobroModalAsync(default, default);
        _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarAlerta(default!, default!);
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

    private static TurnoCajaDto TurnoAbierto(int idTurno, int idUsuario, decimal saldoInicial)
    {
        return new TurnoCajaDto
        {
            IdTurno = idTurno,
            IdUsuario = idUsuario,
            NombreUsuario = "cajero",
            FechaApertura = DateTime.UtcNow,
            SaldoInicial = saldoInicial,
            SaldoTeoricoEfectivo = saldoInicial,
            Estado = EstadoTurnoEnum.Abierto
        };
    }

    private void ConfigurarApertura(bool? resultado, decimal saldoInicial)
    {
        _cajaDialogServiceMock.MostrarDialogoApertura(out _).ReturnsForAnyArgs(x =>
        {
            x[0] = saldoInicial;
            return resultado;
        });
    }

    [Fact]
    public async Task MostrarAvisoCajaCerrada_SinTurnoAbierto_EsVerdadero()
    {
        _cajaServiceMock.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns((TurnoCajaDto?)null);

        var viewModel = await CrearViewModelAsync();

        viewModel.CajaAbierta.Should().BeFalse();
        viewModel.MostrarAvisoCajaCerrada.Should().BeTrue();
    }

    [Fact]
    public async Task MostrarAvisoCajaCerrada_ConTurnoAbierto_EsFalso()
    {
        var viewModel = await CrearViewModelAsync();

        viewModel.CajaAbierta.Should().BeTrue();
        viewModel.MostrarAvisoCajaCerrada.Should().BeFalse();
    }

    [Fact]
    public void MostrarAvisoCajaCerrada_MientrasSeConsultaLaCaja_EsFalso()
    {
        // Hasta que responde la consulta no se sabe si la caja está cerrada: el aviso no debe aparecer y desaparecer.
        var consulta = new TaskCompletionSource<TurnoCajaDto?>();
        _cajaServiceMock.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns(consulta.Task);

        var viewModel = CrearViewModel();

        viewModel.EstadoCajaVerificado.Should().BeFalse();
        viewModel.MostrarAvisoCajaCerrada.Should().BeFalse();
        viewModel.Dispose();
    }

    [Fact]
    public async Task AbrirCajaAsync_ConAperturaAceptada_AbreElTurnoDelUsuarioDeLaSesionYHabilitaElCobro()
    {
        _cajaServiceMock.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns((TurnoCajaDto?)null);
        _cajaServiceMock.AbrirTurnoAsync(Arg.Any<AperturaTurnoDto>(), Arg.Any<CancellationToken>()).Returns(TurnoAbierto(7, 2, 3000m));
        ConfigurarApertura(true, 3000m);
        var viewModel = await CrearViewModelAsync();

        await viewModel.AbrirCajaAsync();

        await _cajaServiceMock.Received(1).AbrirTurnoAsync(
            Arg.Is<AperturaTurnoDto>(d => d.IdUsuario == 2 && d.SaldoInicial == 3000m),
            Arg.Any<CancellationToken>());
        viewModel.CajaAbierta.Should().BeTrue();
        viewModel.TurnoActivo!.IdTurno.Should().Be(7);
        viewModel.MostrarAvisoCajaCerrada.Should().BeFalse();
    }

    [Fact]
    public async Task AbrirCajaAsync_ConAperturaCancelada_NoAbreNingunTurno()
    {
        _cajaServiceMock.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns((TurnoCajaDto?)null);
        ConfigurarApertura(false, 0m);
        var viewModel = await CrearViewModelAsync();

        await viewModel.AbrirCajaAsync();

        await _cajaServiceMock.DidNotReceive().AbrirTurnoAsync(Arg.Any<AperturaTurnoDto>(), Arg.Any<CancellationToken>());
        viewModel.MostrarAvisoCajaCerrada.Should().BeTrue();
    }

    [Fact]
    public async Task AbrirCajaAsync_TurnoYaAbiertoEnOtraTerminal_MuestraAlertaYLaCajaSigueCerrada()
    {
        _cajaServiceMock.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns((TurnoCajaDto?)null);
        _cajaServiceMock.AbrirTurnoAsync(Arg.Any<AperturaTurnoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TurnoYaAbiertoException("Ya existe un turno de caja abierto en el sistema."));
        ConfigurarApertura(true, 1000m);
        var viewModel = await CrearViewModelAsync();

        await viewModel.AbrirCajaAsync();

        _dialogServiceMock.Received(1).MostrarAlerta("Caja no abierta", Arg.Any<string>());
        _dialogServiceMock.DidNotReceive().MostrarError(Arg.Any<string>(), Arg.Any<string>());
        viewModel.CajaAbierta.Should().BeFalse();
    }

    [Fact]
    public async Task AbrirCajaAsync_SinUsuarioEnSesion_NoMuestraElDialogoNiAbreElTurno()
    {
        _cajaServiceMock.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns((TurnoCajaDto?)null);
        _sessionMock.IdUsuario.Returns((int?)null);
        var viewModel = await CrearViewModelAsync();

        await viewModel.AbrirCajaAsync();

        _cajaDialogServiceMock.DidNotReceiveWithAnyArgs().MostrarDialogoApertura(out _);
        await _cajaServiceMock.DidNotReceive().AbrirTurnoAsync(Arg.Any<AperturaTurnoDto>(), Arg.Any<CancellationToken>());
        _dialogServiceMock.Received(1).MostrarAlerta("Sesión incompleta", Arg.Any<string>());
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

    // ------------------------------------------------------------------ //
    // Presupuestos (RF-11, RF-12): guardar (F3) y recuperar (F7)          //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task GuardarComoPresupuesto_SinTurnoAbierto_EmiteConLosDatosDelTicketYLimpiaElTicket()
    {
        // Arrange: PS-17, el presupuesto no exige caja abierta
        _cajaServiceMock.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>()).Returns((TurnoCajaDto?)null);
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(Articulo(1, "Libro", 5000m));
        viewModel.AgregarArticuloAlTicket(Articulo(2, "Lápiz", 300m));
        viewModel.Items[1].Cantidad = 4;
        viewModel.Cliente = ClienteDeMostrador(7, "Escuela N.º 12");
        viewModel.Descuento = 200m;

        _presupuestoServiceMock.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .Returns(PresupuestoEmitido());
        _presupuestoDialogMock.MostrarGuardarPresupuesto(Arg.Any<GuardarPresupuestoViewModel>())
            .Returns(llamada =>
            {
                // Hace lo que haría el cajero en el diálogo: escribe un título, elige 30 días y guarda
                var dialogo = llamada.Arg<GuardarPresupuestoViewModel>();
                dialogo.Titulo = "Lista del colegio";
                dialogo.VigenciaDias = 30;
                dialogo.GuardarCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                return dialogo.Guardado;
            });

        // Act
        viewModel.CajaAbierta.Should().BeFalse();
        viewModel.GuardarComoPresupuestoCommand.CanExecute(null).Should().BeTrue();
        viewModel.GuardarComoPresupuestoCommand.Execute(null);

        // Assert
        await _presupuestoServiceMock.Received(1).EmitirAsync(
            Arg.Is<CrearPresupuestoDto>(dto =>
                dto.IdUsuario == 2
                && dto.IdCliente == 7
                && dto.Titulo == "Lista del colegio"
                && dto.VigenciaDias == 30
                && dto.Descuento == 200m
                && dto.Items.Count == 2
                && dto.Items[0].IdArticulo == 1 && dto.Items[0].Cantidad == 1 && dto.Items[0].PrecioUnitarioPactado == 5000m
                && dto.Items[1].IdArticulo == 2 && dto.Items[1].Cantidad == 4 && dto.Items[1].PrecioUnitarioPactado == 300m),
            Arg.Any<CancellationToken>());
        viewModel.Items.Should().BeEmpty("el presupuesto guardado ya tiene el contenido: el ticket se limpia sin confirmar");
        viewModel.Cliente.Should().BeNull();
        _dialogServiceMock.DidNotReceiveWithAnyArgs().Confirmar(default!, default!);
    }

    [Fact]
    public async Task GuardarComoPresupuesto_CanceladoEnElDialogo_DejaElTicketComoEstaba()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Libro", 5000m));
        _presupuestoDialogMock.MostrarGuardarPresupuesto(Arg.Any<GuardarPresupuestoViewModel>()).Returns(false);

        viewModel.GuardarComoPresupuestoCommand.Execute(null);

        viewModel.Items.Should().ContainSingle();
        await _presupuestoServiceMock.DidNotReceiveWithAnyArgs().EmitirAsync(default!, default);
    }

    [Fact]
    public async Task GuardarComoPresupuesto_SinUsuarioEnSesion_MuestraAlertaYNoAbreElDialogo()
    {
        _sessionMock.IdUsuario.Returns((int?)null);
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Libro", 5000m));

        viewModel.GuardarComoPresupuestoCommand.Execute(null);

        _dialogServiceMock.Received(1).MostrarAlerta("Sesión incompleta", Arg.Any<string>());
        _presupuestoDialogMock.DidNotReceiveWithAnyArgs().MostrarGuardarPresupuesto(default!);
    }

    [Fact]
    public async Task GuardarComoPresupuesto_ConTicketVacio_EstaDeshabilitado()
    {
        var viewModel = await CrearViewModelAsync();

        viewModel.GuardarComoPresupuestoCommand.CanExecute(null).Should().BeFalse();

        // Al agregar el primer artículo se habilita, y al quitarlo se vuelve a deshabilitar
        viewModel.AgregarArticuloAlTicket(Articulo(1, "Libro", 5000m));
        viewModel.GuardarComoPresupuestoCommand.CanExecute(null).Should().BeTrue();
        viewModel.EliminarItem(viewModel.Items[0]);
        viewModel.GuardarComoPresupuestoCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task GuardarComoPresupuesto_ConUnPresupuestoCargado_EstaHabilitado()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);

        viewModel.Items.Should().NotBeEmpty();
        viewModel.GuardarComoPresupuestoCommand.CanExecute(null).Should().BeTrue("PE-15: emite uno nuevo e independiente");
    }

    [Fact]
    public async Task GuardarComoPresupuesto_ConUnPresupuestoCargado_EmiteUnoNuevoConPreciosDeCatalogoYElTituloDelBase()
    {
        // Arrange: el base vigente trae los precios pactados (2450 y 480); el catálogo de hoy tiene 2700 y 450
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        ConfigurarVerificacion(new VerificacionTicketDto
        {
            PreciosActualizados =
            [
                new PrecioActualizadoDto { IdArticulo = 1, Descripcion = "Cuaderno", PrecioAnterior = 2450m, PrecioActual = 2700m },
                new PrecioActualizadoDto { IdArticulo = 2, Descripcion = "Lapicera", PrecioAnterior = 480m, PrecioActual = 450m }
            ],
            Faltantes = [],
            ArticulosNoDisponibles = []
        });
        _presupuestoServiceMock.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>()).Returns(PresupuestoEmitido());

        GuardarPresupuestoViewModel? recibido = null;
        _presupuestoDialogMock.MostrarGuardarPresupuesto(Arg.Any<GuardarPresupuestoViewModel>())
            .Returns(llamada =>
            {
                recibido = llamada.Arg<GuardarPresupuestoViewModel>();
                recibido.GuardarCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                return recibido.Guardado;
            });

        // Act
        await viewModel.GuardarComoPresupuestoCommand.ExecuteAsync(null);

        // Assert: precios de hoy, título del base precargado, aviso con el número del base y la cantidad de precios
        recibido.Should().NotBeNull();
        recibido!.Titulo.Should().Be("Lista escolar 3.º grado — Escuela N.º 12");
        recibido.AvisoBase.Should().Contain("00000123").And.Contain("no cambia");
        recibido.AvisoPrecios.Should().Be("2 precios pasan al catálogo de hoy.");
        recibido.TotalFormateado.Should().Contain("9.850");

        await _presupuestoServiceMock.Received(1).EmitirAsync(
            Arg.Is<CrearPresupuestoDto>(dto =>
                dto.Items.Count == 2
                && dto.Items[0].PrecioUnitarioPactado == 2700m
                && dto.Items[1].PrecioUnitarioPactado == 450m
                && dto.Descuento == 500m
                && dto.Titulo == "Lista escolar 3.º grado — Escuela N.º 12"),
            Arg.Any<CancellationToken>());
        await _presupuestoServiceMock.DidNotReceiveWithAnyArgs().ReemplazarAsync(default, default!, default);
        viewModel.Items.Should().BeEmpty("PE-17: el ticket se vacía como en cualquier presupuesto");
        viewModel.PresupuestoCargado.Should().BeNull();
    }

    [Fact]
    public async Task GuardarComoPresupuesto_ConUnPresupuestoCargadoYSinCambiosDePrecio_UsaLosPreciosDelTicketYNoAvisaDePrecios()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: false);
        ConfigurarVerificacion(VerificacionSinNovedades());
        GuardarPresupuestoViewModel? recibido = null;
        _presupuestoDialogMock.MostrarGuardarPresupuesto(Arg.Any<GuardarPresupuestoViewModel>())
            .Returns(llamada =>
            {
                recibido = llamada.Arg<GuardarPresupuestoViewModel>();
                return false;
            });

        await viewModel.GuardarComoPresupuestoCommand.ExecuteAsync(null);

        recibido.Should().NotBeNull();
        recibido!.AvisoBase.Should().NotBeNull();
        recibido.AvisoPrecios.Should().BeNull();
        recibido.TotalFormateado.Should().Contain("9.850", "el ticket vencido ya estaba a precios de catálogo (2700 y 450)");
        viewModel.Items.Should().HaveCount(2, "cancelar deja el ticket como estaba");
        viewModel.PresupuestoCargado.Should().NotBeNull();
        await _presupuestoServiceMock.DidNotReceiveWithAnyArgs().EmitirAsync(default!, default);
    }

    [Fact]
    public async Task GuardarComoPresupuesto_ConUnPresupuestoCargadoYArticulosDadosDeBaja_MuestraAlertaYNoAbreElDialogo()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        ConfigurarVerificacion(new VerificacionTicketDto
        {
            PreciosActualizados = [],
            Faltantes = [],
            ArticulosNoDisponibles = ["Lapicera azul Cristal"]
        });

        await viewModel.GuardarComoPresupuestoCommand.ExecuteAsync(null);

        _dialogServiceMock.Received(1).MostrarAlerta("Artículos dados de baja", Arg.Is<string>(m => m.Contains("Lapicera azul Cristal")));
        _presupuestoDialogMock.DidNotReceiveWithAnyArgs().MostrarGuardarPresupuesto(default!);
        viewModel.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GuardarComoPresupuesto_SinPresupuestoCargado_NoVerificaPreciosNiMuestraAvisoDeBase()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Libro", 5000m));
        GuardarPresupuestoViewModel? recibido = null;
        _presupuestoDialogMock.MostrarGuardarPresupuesto(Arg.Any<GuardarPresupuestoViewModel>())
            .Returns(llamada =>
            {
                recibido = llamada.Arg<GuardarPresupuestoViewModel>();
                return false;
            });

        await viewModel.GuardarComoPresupuestoCommand.ExecuteAsync(null);

        recibido.Should().NotBeNull();
        recibido!.AvisoBase.Should().BeNull();
        recibido.Titulo.Should().BeEmpty();
        await _ventaServiceMock.DidNotReceiveWithAnyArgs().VerificarTicketAsync(default!, default, default);
    }

    [Fact]
    public async Task RecuperarPresupuesto_Vigente_CargaPreciosPactadosTopesYDescuento()
    {
        // Arrange
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);

        // Assert (PS-02, PS-04, PS-15)
        viewModel.Items.Should().HaveCount(2);
        viewModel.Items[0].IdArticulo.Should().Be(1);
        viewModel.Items[0].PrecioUnitario.Should().Be(2450m, "vigente: se cobra el precio pactado aunque el catálogo subió");
        viewModel.Items[0].Cantidad.Should().Be(3);
        viewModel.Items[0].CantidadMaxima.Should().Be(3);
        viewModel.Items[0].EsDePresupuesto.Should().BeTrue();
        viewModel.Items[1].PrecioUnitario.Should().Be(480m);
        viewModel.Items[1].CantidadMaxima.Should().Be(5);
        viewModel.Descuento.Should().Be(500m);
        viewModel.Total.Should().Be(3 * 2450m + 5 * 480m - 500m);

        viewModel.PresupuestoCargado.Should().NotBeNull();
        viewModel.PresupuestoCargado!.Id.Should().Be(123);
        viewModel.PresupuestoCargado.Vigente.Should().BeTrue();
        viewModel.PresupuestoCargado.Etiqueta.Should().Be("Presupuesto N.º 00000123 · vigente hasta 29/11");
        viewModel.PresupuestoCargado.Detalle.Should().Contain("Lista escolar 3.º grado");
        _presupuestoDialogMock.DidNotReceiveWithAnyArgs().MostrarAlertaPreciosPresupuesto(default!);
    }

    [Fact]
    public async Task RecuperarPresupuesto_Vencido_CargaPreciosDeCatalogoSinTopes()
    {
        // Arrange: vencido con diferencias de precio, así que primero se muestra la alerta y el cajero continúa
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: false);

        // Assert (PS-03)
        viewModel.Items[0].PrecioUnitario.Should().Be(2700m, "vencido: todo a precio de catálogo");
        viewModel.Items[1].PrecioUnitario.Should().Be(450m);
        viewModel.Items.Should().OnlyContain(i => i.CantidadMaxima == null && i.EsDePresupuesto);
        viewModel.PresupuestoCargado!.Vigente.Should().BeFalse();
        viewModel.PresupuestoCargado.Etiqueta.Should().Be("Presupuesto N.º 00000123 · vencido: precios de catálogo");
        _presupuestoDialogMock.Received(1).MostrarAlertaPreciosPresupuesto(Arg.Any<AlertaPreciosPresupuestoViewModel>());
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConAlertaCancelada_NoCargaNadaYDejaElTicketComoEstaba()
    {
        // Arrange: ya hay un artículo en el ticket y el cajero acepta reemplazarlo, pero cancela la alerta
        var viewModel = await CrearViewModelConTicketAsync(Articulo(9, "Regla", 700m));
        _dialogServiceMock.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente: false));
        _presupuestoDialogMock.MostrarAlertaPreciosPresupuesto(Arg.Any<AlertaPreciosPresupuestoViewModel>()).Returns(false);

        // Act
        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        // Assert
        viewModel.Items.Should().ContainSingle().Which.IdArticulo.Should().Be(9);
        viewModel.PresupuestoCargado.Should().BeNull();
        viewModel.Descuento.Should().Be(0m);
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConTicketConArticulos_PideConfirmacionYSiSeRechazaNoReemplaza()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(9, "Regla", 700m));
        _dialogServiceMock.Confirmar("Recuperar presupuesto", Arg.Is<string>(m => m.Contains("se reemplazará"))).Returns(false);
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente: true));

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        viewModel.Items.Should().ContainSingle().Which.IdArticulo.Should().Be(9);
        viewModel.PresupuestoCargado.Should().BeNull();
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConTicketConArticulos_ConfirmadoLoReemplaza()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(9, "Regla", 700m));
        _dialogServiceMock.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente: true));

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        viewModel.Items.Should().HaveCount(2).And.NotContain(i => i.IdArticulo == 9);
        viewModel.PresupuestoCargado.Should().NotBeNull();
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConClienteEnElTicket_SeLoPasaAlDialogoComoPrefiltro()
    {
        var viewModel = await CrearViewModelAsync();
        viewModel.Cliente = ClienteDeMostrador(7, "Escuela N.º 12");
        RecuperarPresupuestoViewModel? recibido = null;
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Do<RecuperarPresupuestoViewModel>(vm => recibido = vm))
            .Returns((PresupuestoParaVentaDto?)null);

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        recibido.Should().NotBeNull();
        recibido!.HayClienteEnElTicket.Should().BeTrue();
        recibido.SoloDelCliente.Should().BeTrue();
        recibido.TextoSoloDelCliente.Should().Be("Solo de Escuela N.º 12");
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConsumidorFinal_NoSeLePasaClienteAlDialogo()
    {
        var viewModel = await CrearViewModelAsync();
        RecuperarPresupuestoViewModel? recibido = null;
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Do<RecuperarPresupuestoViewModel>(vm => recibido = vm))
            .Returns((PresupuestoParaVentaDto?)null);

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        recibido.Should().NotBeNull();
        recibido!.HayClienteEnElTicket.Should().BeFalse();
        recibido.SoloDelCliente.Should().BeFalse();
    }

    [Fact]
    public async Task RecuperarPresupuesto_CanceladoEnElDialogo_NoCambiaNada()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(9, "Regla", 700m));
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns((PresupuestoParaVentaDto?)null);

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        viewModel.Items.Should().ContainSingle().Which.IdArticulo.Should().Be(9);
        _dialogServiceMock.DidNotReceiveWithAnyArgs().Confirmar(default!, default!);
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConClienteDelPresupuesto_LoBuscaYLoCarga()
    {
        var viewModel = await CrearViewModelAsync();
        _clienteServiceMock.ObtenerClientePorIdAsync(7, Arg.Any<CancellationToken>()).Returns(ClienteDeMostrador(7, "Escuela N.º 12"));
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente: true) with { IdCliente = 7, ClienteNombre = "Escuela N.º 12" });

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        viewModel.Cliente.Should().NotBeNull();
        viewModel.Cliente!.IdCliente.Should().Be(7);
        viewModel.ClienteNombre.Should().Be("Escuela N.º 12");
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConClienteQueNoSeEncuentra_CargaConsumidorFinalYAvisa()
    {
        var viewModel = await CrearViewModelAsync();
        _clienteServiceMock.ObtenerClientePorIdAsync(7, Arg.Any<CancellationToken>()).Returns((ClienteDto?)null);
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente: true) with { IdCliente = 7, ClienteNombre = "Escuela N.º 12" });

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        viewModel.Cliente.Should().BeNull();
        viewModel.Items.Should().HaveCount(2);
        _dialogServiceMock.Received(1).MostrarAlerta("Cliente no disponible", Arg.Any<string>());
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConDescuentoMayorAlSubtotal_LoLimitaAlSubtotal()
    {
        // El descuento era de todo el presupuesto; si se omitieron artículos dados de baja, el subtotal es menor
        var viewModel = await CrearViewModelAsync();
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente: true) with { Descuento = 999999m });

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        viewModel.Descuento.Should().Be(viewModel.Subtotal);
        viewModel.Total.Should().Be(0m);
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConTodosLosArticulosDadosDeBaja_NoCargaElTicket()
    {
        var viewModel = await CrearViewModelAsync();
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente: true) with
            {
                Renglones = [],
                ArticulosNoDisponibles = ["Libro"]
            });
        _presupuestoDialogMock.MostrarAlertaPreciosPresupuesto(Arg.Any<AlertaPreciosPresupuestoViewModel>()).Returns(true);

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        viewModel.PresupuestoCargado.Should().BeNull();
        _dialogServiceMock.Received(1).MostrarAlerta("Presupuesto sin artículos disponibles", Arg.Any<string>());
    }

    [Fact]
    public async Task CantidadDeUnaLineaConTope_NoPuedeSuperarLoPresupuestado_PeroSiBajar()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        var linea = viewModel.Items[0];

        // Act + Assert: igual que un valor menor a 1, uno mayor al tope se descarta y vuelve el anterior (PS-04)
        linea.Cantidad = 4;
        linea.Cantidad.Should().Be(3);
        linea.Cantidad = 2;
        linea.Cantidad.Should().Be(2);
        linea.Cantidad = 3;
        linea.Cantidad.Should().Be(3);
        linea.LimiteCantidad.Should().Be(3);
        linea.TextoTope.Should().Be("Cantidad presupuestada: 3; se puede bajar");
        viewModel.Total.Should().Be(3 * 2450m + 5 * 480m - 500m);
    }

    [Fact]
    public async Task AgregarArticuloAlTicket_ConLineaConTopeQueSePasaria_NoSumaYAvisa()
    {
        // Arrange: el artículo 1 está en el tope (3 de 3)
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);

        // Act: el cajero escanea de nuevo el mismo artículo
        viewModel.AgregarArticuloAlTicket(Articulo(1, "Cuaderno tapa dura 48 hojas rayado", 2700m));

        // Assert
        viewModel.Items.Should().HaveCount(2);
        viewModel.Items[0].Cantidad.Should().Be(3);
        viewModel.Items[0].PrecioUnitario.Should().Be(2450m);
        _dialogServiceMock.Received(1).MostrarAlerta("Cantidad presupuestada", Arg.Is<string>(m => m.Contains('3')));
    }

    [Fact]
    public async Task AgregarArticuloAlTicket_ConLineaConTopeQueAunNoLoAlcanza_Suma()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        viewModel.Items[0].Cantidad = 2;

        viewModel.AgregarArticuloAlTicket(Articulo(1, "Cuaderno tapa dura 48 hojas rayado", 2700m));

        viewModel.Items[0].Cantidad.Should().Be(3);
        _dialogServiceMock.DidNotReceiveWithAnyArgs().MostrarAlerta(default!, default!);
    }

    [Fact]
    public async Task AgregarArticuloAlTicket_ConUnPresupuestoCargado_UnArticuloNuevoEntraAPrecioDeCatalogoSinTope()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);

        viewModel.AgregarArticuloAlTicket(Articulo(9, "Regla", 700m));

        var nueva = viewModel.Items.Should().HaveCount(3).And.Subject.Last();
        nueva.PrecioUnitario.Should().Be(700m);
        nueva.CantidadMaxima.Should().BeNull();
        nueva.EsDePresupuesto.Should().BeFalse();
        nueva.Cantidad = 5;
        nueva.Cantidad.Should().Be(5);
    }

    [Fact]
    public async Task LimpiarVenta_ConUnPresupuestoCargado_LoDesvincula()
    {
        // Arrange
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        _dialogServiceMock.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        // Act: F8
        viewModel.LimpiarVenta();

        // Assert
        viewModel.PresupuestoCargado.Should().BeNull();
        viewModel.TienePresupuestoCargado.Should().BeFalse();
        viewModel.Items.Should().BeEmpty();
        viewModel.Descuento.Should().Be(0m);
        viewModel.GuardarComoPresupuestoCommand.CanExecute(null).Should().BeFalse("el ticket quedó vacío");
    }

    [Fact]
    public async Task LimpiarVenta_ConPresupuestoCargadoYTicketVacio_TambienLoDesvincula()
    {
        // Arrange: el cajero quitó todas las líneas pero el presupuesto sigue vinculado
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        foreach (var linea in viewModel.Items.ToList())
        {
            viewModel.EliminarItem(linea);
        }

        _dialogServiceMock.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        viewModel.PuedeLimpiar.Should().BeTrue();

        // Act
        viewModel.LimpiarVenta();

        // Assert
        viewModel.PresupuestoCargado.Should().BeNull();
        viewModel.PuedeLimpiar.Should().BeFalse();
    }

    [Fact]
    public async Task CobrarVentaAsync_ConPresupuestoCargado_EnviaIdPresupuestoOrigenYLoPasaALaVerificacion()
    {
        // Arrange
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        decimal total = viewModel.Total;
        _dialogServiceMock.MostrarCobroModalAsync(total, Arg.Any<ClienteDto?>())
            .Returns(new List<PagoVentaDto> { new() { MedioPago = MedioPagoEnum.Efectivo, Monto = total } });
        _ventaServiceMock.RegistrarVentaAsync(Arg.Any<CrearVentaDto>(), Arg.Any<CancellationToken>())
            .Returns(new VentaResponseDto
            {
                IdVenta = 1,
                IdTurno = 1,
                IdUsuario = 2,
                FechaHora = DateTime.UtcNow,
                Subtotal = total,
                Descuento = 0m,
                Total = total,
                EstadoFiscal = EstadoFiscalEnum.NoAplica,
                Items = new List<DetalleVentaDto>(),
                Pagos = new List<PagoVentaDto>()
            });

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert
        await _ventaServiceMock.Received(1).VerificarTicketAsync(
            Arg.Any<IReadOnlyList<DetalleVentaDto>>(),
            123,
            Arg.Any<CancellationToken>());
        await _ventaServiceMock.Received(1).RegistrarVentaAsync(
            Arg.Is<CrearVentaDto>(dto => dto.IdPresupuestoOrigen == 123 && dto.Descuento == 500m && dto.Items.Count == 2),
            Arg.Any<CancellationToken>());
        viewModel.Items.Should().BeEmpty();
        viewModel.PresupuestoCargado.Should().BeNull("al registrarse la venta, el ticket y el presupuesto vinculado se limpian juntos");
    }

    [Fact]
    public async Task CobrarVentaAsync_SinPresupuestoCargado_NoEnviaIdPresupuestoOrigen()
    {
        var viewModel = await CrearViewModelConTicketYPagoAsync();
        _ventaServiceMock.RegistrarVentaAsync(Arg.Any<CrearVentaDto>(), Arg.Any<CancellationToken>())
            .Returns(new VentaResponseDto
            {
                IdVenta = 1,
                IdTurno = 1,
                IdUsuario = 2,
                FechaHora = DateTime.UtcNow,
                Subtotal = 5000m,
                Descuento = 0m,
                Total = 5000m,
                EstadoFiscal = EstadoFiscalEnum.NoAplica,
                Items = new List<DetalleVentaDto>(),
                Pagos = new List<PagoVentaDto>()
            });

        await viewModel.CobrarVentaAsync();

        await _ventaServiceMock.Received(1).VerificarTicketAsync(Arg.Any<IReadOnlyList<DetalleVentaDto>>(), null, Arg.Any<CancellationToken>());
        await _ventaServiceMock.Received(1).RegistrarVentaAsync(Arg.Is<CrearVentaDto>(dto => dto.IdPresupuestoOrigen == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CobrarVentaAsync_ConPresupuestoQueVencioEntreLaCargaYElCobro_ActualizaLosPreciosYMantieneElTicket()
    {
        // Arrange: el servidor, con su reloj, ya ve el presupuesto vencido y devuelve los precios de catálogo
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        ConfigurarVerificacion(VerificacionSinNovedades() with
        {
            PreciosActualizados =
            [
                new PrecioActualizadoDto { IdArticulo = 1, Descripcion = "Cuaderno", PrecioAnterior = 2450m, PrecioActual = 2700m }
            ]
        });

        // Act
        await viewModel.CobrarVentaAsync();

        // Assert: el flujo de "precio actualizado" sigue funcionando y el cobro espera la confirmación del cajero
        viewModel.Items[0].PrecioUnitario.Should().Be(2700m);
        _dialogServiceMock.Received(1).MostrarAlerta("Precios actualizados", Arg.Is<string>(m => m.Contains("Cuaderno")));
        viewModel.PresupuestoCargado.Should().NotBeNull();
    }

    [Fact]
    public async Task QuitarPresupuesto_ConArticuloAgregadoAparte_QuitaSoloLosRenglonesDelPresupuestoYSuDescuento()
    {
        // Arrange
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 90, Descripcion = "Regla 30 cm", PrecioVenta = 700m, StockActual = 10, EsServicio = false });

        // Act
        viewModel.QuitarPresupuestoCommand.Execute(null);

        // Assert
        viewModel.PresupuestoCargado.Should().BeNull();
        var restante = viewModel.Items.Should().ContainSingle().Subject;
        restante.IdArticulo.Should().Be(90, "lo agregado aparte no venía del presupuesto");
        restante.NumeroItem.Should().Be(1);
        viewModel.Descuento.Should().Be(0m, "el descuento era parte de lo pactado (PS-15)");
        viewModel.Total.Should().Be(700m);
        viewModel.GuardarComoPresupuestoCommand.CanExecute(null).Should().BeTrue("el ticket ya no viene de un presupuesto");
    }

    [Fact]
    public async Task QuitarPresupuesto_ConClienteDelPresupuesto_ConservaElCliente()
    {
        // Arrange
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        var cliente = ClienteDeMostrador(7, "Escuela N.º 12");
        viewModel.Cliente = cliente;

        // Act
        viewModel.QuitarPresupuestoCommand.Execute(null);

        // Assert
        viewModel.Cliente.Should().Be(cliente);
        viewModel.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task MoverSeleccionTicket_ConFlechas_RecorreLasFilasSinDarLaVuelta()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 1, Descripcion = "Hoja A4", PrecioVenta = 50m, StockActual = 500, EsServicio = false });
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 2, Descripcion = "Regla 30 cm", PrecioVenta = 700m, StockActual = 10, EsServicio = false });
        viewModel.ItemSeleccionado = null;

        // Act y Assert: sin selección, ↓ elige la primera; en el extremo inferior se queda
        viewModel.MoverSeleccionTicket(1);
        viewModel.ItemSeleccionado!.IdArticulo.Should().Be(1);
        viewModel.MoverSeleccionTicket(1);
        viewModel.MoverSeleccionTicket(1);
        viewModel.ItemSeleccionado!.IdArticulo.Should().Be(2, "como en una grilla, no vuelve a la primera");
        viewModel.MoverSeleccionTicket(-1);
        viewModel.ItemSeleccionado!.IdArticulo.Should().Be(1);
    }

    [Fact]
    public async Task MoverSeleccionTicket_SinSeleccionConFlechaArriba_EligeLaUltimaFila()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 1, Descripcion = "Hoja A4", PrecioVenta = 50m, StockActual = 500, EsServicio = false });
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 2, Descripcion = "Regla 30 cm", PrecioVenta = 700m, StockActual = 10, EsServicio = false });
        viewModel.ItemSeleccionado = null;

        // Act
        viewModel.MoverSeleccionTicket(-1);

        // Assert
        viewModel.ItemSeleccionado!.IdArticulo.Should().Be(2);
    }

    [Fact]
    public async Task QuitarPresupuesto_SinPresupuestoCargado_NoCambiaElTicket()
    {
        // Arrange
        var viewModel = await CrearViewModelAsync();
        viewModel.AgregarArticuloAlTicket(new ArticuloVentaDto { IdArticulo = 1, Descripcion = "Hoja A4", PrecioVenta = 50m, StockActual = 500, EsServicio = false });

        // Act
        viewModel.QuitarPresupuestoCommand.Execute(null);

        // Assert
        viewModel.Items.Should().ContainSingle();
    }

    // ------------------------------------------------------------------ //
    // Listas de artículos (RF-30, L-05 a L-21)                            //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task CargarLista_ConTicketVacio_CargaLosRenglonesAPrecioDeCatalogoYVinculaLaLista()
    {
        var viewModel = await CrearViewModelAsync();
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        viewModel.Items.Should().HaveCount(3);
        viewModel.Items[0].IdArticulo.Should().Be(1);
        viewModel.Items[0].Cantidad.Should().Be(3);
        viewModel.Items[0].PrecioUnitario.Should().Be(4900m, "la lista siempre se vende al precio de catálogo (L-08)");
        viewModel.Items[0].EsDePresupuesto.Should().BeFalse();
        viewModel.Items[0].CantidadMaxima.Should().BeNull();
        viewModel.Items[1].StockActual.Should().Be(8);
        viewModel.Total.Should().Be((3 * 4900m) + (12 * 450m) + 7200m);
        viewModel.ListaCargada.Should().Be(new ListaCargada(12, "3.º grado — Colegio San Martín", DatosDeMuestra.ListaParaVenta().FechaModificacion));
        viewModel.ListaCargada!.Etiqueta.Should().Be("Lista «3.º grado — Colegio San Martín»");
        viewModel.TieneListaCargada.Should().BeTrue();
        _listaDialogMock.DidNotReceiveWithAnyArgs().PreguntarReemplazarOAgregar(default!, default);
    }

    [Fact]
    public async Task CargarLista_ConTicketNoVacioYReemplazar_VaciaLosArticulosPrevios()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(99, "Regla", 700m));
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        _listaDialogMock.PreguntarReemplazarOAgregar(Arg.Any<string>(), true).Returns(RespuestaCargaLista.Reemplazar);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        viewModel.Items.Select(i => i.IdArticulo).Should().Equal(1, 2, 3);
        viewModel.ListaCargada.Should().NotBeNull();
    }

    [Fact]
    public async Task CargarLista_ConAgregar_SumaLaCantidadDelArticuloExistenteYAgregaLosNuevos()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Cuaderno ABC", 4900m), Articulo(99, "Regla", 700m));
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        _listaDialogMock.PreguntarReemplazarOAgregar(Arg.Any<string>(), true).Returns(RespuestaCargaLista.Agregar);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        viewModel.Items.Select(i => i.IdArticulo).Should().Equal(1, 99, 2, 3);
        viewModel.Items[0].Cantidad.Should().Be(4, "1 unidad del ticket + 3 de la lista");
        viewModel.Items[1].Cantidad.Should().Be(1);
        viewModel.Items.Select(i => i.NumeroItem).Should().Equal(1, 2, 3, 4);
        viewModel.ListaCargada.Should().NotBeNull("agregar una lista a un ticket armado a mano la establece (L-20)");
        viewModel.ListaCargada!.Id.Should().Be(12);
    }

    [Fact]
    public async Task CargarLista_AgregarConOtraListaYaCargada_ConservaLaPrimera()
    {
        var viewModel = await CrearViewModelAsync();
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        await viewModel.CargarListaCommand.ExecuteAsync(null);

        var otra = DatosDeMuestra.ListaParaVenta() with { IdLista = 15, Nombre = "Jardín sala de 5", ArticulosNoDisponibles = [], Faltantes = [] };
        ConfigurarListaElegida(otra, confirmaAviso: true);
        _listaDialogMock.PreguntarReemplazarOAgregar(Arg.Any<string>(), true).Returns(RespuestaCargaLista.Agregar);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        viewModel.ListaCargada!.Id.Should().Be(12, "la venta conserva la primera lista cargada (L-20)");
        viewModel.Items[0].Cantidad.Should().Be(6);
    }

    [Fact]
    public async Task CargarLista_ConUnPresupuestoCargado_NoOfreceAgregarYReemplazarLoDesvincula()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        _listaDialogMock.PreguntarReemplazarOAgregar(Arg.Any<string>(), false).Returns(RespuestaCargaLista.Reemplazar);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        _listaDialogMock.Received(1).PreguntarReemplazarOAgregar("3.º grado — Colegio San Martín", false);
        viewModel.PresupuestoCargado.Should().BeNull("la lista reemplaza el ticket y lo desvincula del presupuesto (L-15)");
        viewModel.Descuento.Should().Be(0m);
        viewModel.Items.Should().HaveCount(3);
        viewModel.Items.Should().OnlyContain(i => !i.EsDePresupuesto && i.CantidadMaxima == null);
        viewModel.ListaCargada.Should().NotBeNull();
    }

    [Fact]
    public async Task CargarLista_ConUnPresupuestoCargadoYRespuestaCancelar_NoCambiaNada()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        _listaDialogMock.PreguntarReemplazarOAgregar(Arg.Any<string>(), false).Returns(RespuestaCargaLista.Cancelar);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        viewModel.PresupuestoCargado.Should().NotBeNull();
        viewModel.ListaCargada.Should().BeNull();
        viewModel.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CargarLista_ConArticulosNoDisponiblesYFaltantes_PideConfirmacionConElDetalleYLosOmite()
    {
        var viewModel = await CrearViewModelAsync();
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        _listaDialogMock.Received(1).Confirmar(
            "Cargar lista",
            Arg.Is<string>(m => m.Contains("Se omitirán") && m.Contains("Block de dibujo") && m.Contains("Stock insuficiente") && m.Contains("Lápiz negro HB Faber")));
        viewModel.Items.Should().NotContain(i => i.Descripcion.Contains("Block de dibujo"));
        viewModel.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task CargarLista_ConAvisoRechazado_NoCambiaNada()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(99, "Regla", 700m));
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: false);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        viewModel.Items.Should().ContainSingle().Which.IdArticulo.Should().Be(99);
        viewModel.ListaCargada.Should().BeNull();
        _listaDialogMock.DidNotReceiveWithAnyArgs().PreguntarReemplazarOAgregar(default!, default);
    }

    [Fact]
    public async Task CargarLista_ConPreguntaCancelada_NoCambiaNada()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(99, "Regla", 700m));
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        _listaDialogMock.PreguntarReemplazarOAgregar(Arg.Any<string>(), true).Returns(RespuestaCargaLista.Cancelar);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        viewModel.Items.Should().ContainSingle().Which.IdArticulo.Should().Be(99);
        viewModel.ListaCargada.Should().BeNull();
    }

    [Fact]
    public async Task CargarLista_CanceladaEnElSelector_NoConsultaNiCambiaNada()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(99, "Regla", 700m));
        _listaDialogMock.MostrarSeleccionarLista(Arg.Any<SeleccionarListaViewModel>()).Returns((int?)null);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        await _listaServiceMock.DidNotReceiveWithAnyArgs().ObtenerParaVentaAsync(default, default);
        viewModel.Items.Should().ContainSingle();
        viewModel.ListaCargada.Should().BeNull();
    }

    [Fact]
    public async Task CargarLista_ConTodosLosArticulosDadosDeBaja_InformaYNoCarga()
    {
        var viewModel = await CrearViewModelAsync();
        var sinDisponibles = DatosDeMuestra.ListaParaVenta() with { Renglones = [], Faltantes = [] };
        ConfigurarListaElegida(sinDisponibles, confirmaAviso: true);

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        _listaDialogMock.Received(1).MostrarInformacion(
            "Lista sin artículos disponibles",
            Arg.Is<string>(m => m.Contains("Block de dibujo")));
        viewModel.Items.Should().BeEmpty();
        viewModel.ListaCargada.Should().BeNull();
    }

    [Fact]
    public async Task CargarLista_QueYaNoExiste_MuestraAlertaYNoCambiaElTicket()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(99, "Regla", 700m));
        _listaDialogMock.MostrarSeleccionarLista(Arg.Any<SeleccionarListaViewModel>()).Returns(12);
        _listaServiceMock.ObtenerParaVentaAsync(12, Arg.Any<CancellationToken>()).Throws(new DomainException("No se encontró la lista 12."));

        await viewModel.CargarListaCommand.ExecuteAsync(null);

        _listaDialogMock.Received(1).MostrarAlerta("Lista no disponible", "No se encontró la lista 12.");
        viewModel.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task RecuperarPresupuesto_ConUnaListaCargada_LaDesvincula()
    {
        var viewModel = await CrearViewModelAsync();
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        await viewModel.CargarListaCommand.ExecuteAsync(null);
        viewModel.ListaCargada.Should().NotBeNull();

        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente: true));
        _presupuestoDialogMock.MostrarAlertaPreciosPresupuesto(Arg.Any<AlertaPreciosPresupuestoViewModel>()).Returns(true);
        _dialogServiceMock.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);

        viewModel.PresupuestoCargado.Should().NotBeNull();
        viewModel.ListaCargada.Should().BeNull("un ticket nunca queda vinculado a la vez a un presupuesto y a una lista (L-21)");
    }

    [Fact]
    public async Task LimpiarVenta_ConUnaListaCargada_LaDesvincula()
    {
        var viewModel = await CrearViewModelAsync();
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        await viewModel.CargarListaCommand.ExecuteAsync(null);
        _dialogServiceMock.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        viewModel.LimpiarVenta();

        viewModel.Items.Should().BeEmpty();
        viewModel.ListaCargada.Should().BeNull();
    }

    [Fact]
    public async Task CobrarVentaAsync_ConUnaListaCargada_EnviaIdListaOrigenYLaDesvinculaAlCobrar()
    {
        var viewModel = await CrearViewModelAsync();
        viewModel.CajaAbierta = true;
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        await viewModel.CargarListaCommand.ExecuteAsync(null);
        decimal total = viewModel.Total;
        _dialogServiceMock.MostrarCobroModalAsync(total, Arg.Any<ClienteDto?>())
            .Returns(new List<PagoVentaDto> { new() { MedioPago = MedioPagoEnum.Efectivo, Monto = total } });
        _ventaServiceMock.RegistrarVentaAsync(Arg.Any<CrearVentaDto>(), Arg.Any<CancellationToken>())
            .Returns(new VentaResponseDto
            {
                IdVenta = 1,
                IdTurno = 1,
                IdUsuario = 2,
                FechaHora = DateTime.UtcNow,
                Subtotal = total,
                Descuento = 0m,
                Total = total,
                EstadoFiscal = EstadoFiscalEnum.NoAplica,
                Items = new List<DetalleVentaDto>(),
                Pagos = new List<PagoVentaDto>()
            });

        await viewModel.CobrarVentaAsync();

        await _ventaServiceMock.Received(1).RegistrarVentaAsync(
            Arg.Is<CrearVentaDto>(dto => dto.IdListaOrigen == 12 && dto.IdPresupuestoOrigen == null && dto.Items.Count == 3),
            Arg.Any<CancellationToken>());
        viewModel.ListaCargada.Should().BeNull();
        viewModel.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CobrarVentaAsync_SinListaCargada_NoEnviaIdListaOrigen()
    {
        var viewModel = await CrearViewModelConTicketYPagoAsync();
        _ventaServiceMock.RegistrarVentaAsync(Arg.Any<CrearVentaDto>(), Arg.Any<CancellationToken>())
            .Returns(new VentaResponseDto
            {
                IdVenta = 1,
                IdTurno = 1,
                IdUsuario = 2,
                FechaHora = DateTime.UtcNow,
                Subtotal = 5000m,
                Descuento = 0m,
                Total = 5000m,
                EstadoFiscal = EstadoFiscalEnum.NoAplica,
                Items = new List<DetalleVentaDto>(),
                Pagos = new List<PagoVentaDto>()
            });

        await viewModel.CobrarVentaAsync();

        await _ventaServiceMock.Received(1).RegistrarVentaAsync(Arg.Is<CrearVentaDto>(dto => dto.IdListaOrigen == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuitarLista_DesvinculaElTicketPeroConservaLosArticulos()
    {
        var viewModel = await CrearViewModelAsync();
        ConfigurarListaElegida(DatosDeMuestra.ListaParaVenta(), confirmaAviso: true);
        await viewModel.CargarListaCommand.ExecuteAsync(null);

        viewModel.QuitarListaCommand.Execute(null);

        viewModel.ListaCargada.Should().BeNull();
        viewModel.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GuardarComoLista_ConTicketVacio_EstaDeshabilitado()
    {
        var viewModel = await CrearViewModelAsync();

        viewModel.GuardarComoListaCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task GuardarComoLista_ConUnPresupuestoCargado_EstaHabilitadoYNoVinculaElTicketALaLista()
    {
        var viewModel = await CrearViewModelConPresupuestoRecuperadoAsync(vigente: true);
        ConfigurarGuardadoDeLista(nombre: "Útiles", resultado: ListaGuardada(30, "Útiles"));

        viewModel.GuardarComoListaCommand.CanExecute(null).Should().BeTrue();
        viewModel.GuardarComoListaCommand.Execute(null);

        viewModel.ListaCargada.Should().BeNull("un ticket con presupuesto cargado no se vincula también a una lista");
        viewModel.PresupuestoCargado.Should().NotBeNull();
    }

    [Fact]
    public async Task GuardarComoLista_ListaNueva_LlamaACrearConLosRenglonesYVinculaElTicket()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Cuaderno", 4900m), Articulo(2, "Lápiz", 450m));
        viewModel.Items[1].Cantidad = 4;
        ConfigurarGuardadoDeLista(nombre: "  3.º grado  ", resultado: ListaGuardada(30, "3.º grado"));

        viewModel.GuardarComoListaCommand.Execute(null);

        await _listaServiceMock.Received(1).CrearAsync(
            Arg.Is<CrearListaDto>(dto => dto.IdUsuario == 2
                && dto.Nombre == "3.º grado"
                && dto.Renglones.Count == 2
                && dto.Renglones[1].IdArticulo == 2
                && dto.Renglones[1].Cantidad == 4),
            Arg.Any<CancellationToken>());
        viewModel.ListaCargada.Should().Be(new ListaCargada(30, "3.º grado", ListaGuardada(30, "3.º grado").FechaModificacion));
        viewModel.Items.Should().HaveCount(2, "el ticket no se limpia al guardar la lista");
        _listaDialogMock.Received(1).MostrarInformacion("Lista guardada", Arg.Is<string>(m => m.Contains("3.º grado")));
    }

    [Fact]
    public async Task GuardarComoLista_ActualizarLaCargada_LlamaAActualizarConLaFechaLeidaAlCargarlaYRefrescaLaFicha()
    {
        var viewModel = await CrearViewModelAsync();
        var lista = DatosDeMuestra.ListaParaVenta();
        ConfigurarListaElegida(lista, confirmaAviso: true);
        await viewModel.CargarListaCommand.ExecuteAsync(null);
        var nuevaFecha = new DateTime(2026, 11, 19, 16, 0, 0, DateTimeKind.Utc);
        _listaServiceMock.ActualizarAsync(12, Arg.Any<ActualizarListaDto>(), Arg.Any<CancellationToken>())
            .Returns(ListaGuardada(12, lista.Nombre) with { FechaModificacion = nuevaFecha });
        _listaDialogMock.MostrarGuardarLista(Arg.Any<GuardarListaViewModel>()).Returns(ci =>
        {
            var vm = ci.Arg<GuardarListaViewModel>();
            vm.ActualizarVinculada.Should().BeTrue("con una lista cargada, Actualizar es la opción por defecto (L-16)");
            vm.GuardarCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return vm.Guardada;
        });

        viewModel.GuardarComoListaCommand.Execute(null);

        await _listaServiceMock.Received(1).ActualizarAsync(
            12,
            Arg.Is<ActualizarListaDto>(dto => dto.Nombre == lista.Nombre
                && dto.FechaModificacionLeida == lista.FechaModificacion
                && dto.Renglones.Count == 3),
            Arg.Any<CancellationToken>());
        await _listaServiceMock.DidNotReceiveWithAnyArgs().CrearAsync(default!, default);
        viewModel.ListaCargada!.FechaModificacion.Should().Be(nuevaFecha, "el siguiente 'Actualizar' debe mandar la fecha nueva");
    }

    [Fact]
    public async Task GuardarComoLista_CanceladoEnElDialogo_NoCambiaNada()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Cuaderno", 4900m));
        _listaDialogMock.MostrarGuardarLista(Arg.Any<GuardarListaViewModel>()).Returns(false);

        viewModel.GuardarComoListaCommand.Execute(null);

        viewModel.ListaCargada.Should().BeNull();
        _listaDialogMock.DidNotReceiveWithAnyArgs().MostrarInformacion(default!, default!);
    }

    [Fact]
    public async Task GuardarComoLista_SinUsuarioEnSesion_MuestraAlertaYNoAbreElDialogo()
    {
        var viewModel = await CrearViewModelConTicketAsync(Articulo(1, "Cuaderno", 4900m));
        _sessionMock.IdUsuario.Returns((int?)null);

        viewModel.GuardarComoListaCommand.Execute(null);

        _listaDialogMock.Received(1).MostrarAlerta("Sesión incompleta", Arg.Any<string>());
        _listaDialogMock.DidNotReceiveWithAnyArgs().MostrarGuardarLista(default!);
    }

    private void ConfigurarListaElegida(ListaParaVentaDto lista, bool confirmaAviso)
    {
        _listaDialogMock.MostrarSeleccionarLista(Arg.Any<SeleccionarListaViewModel>()).Returns(lista.IdLista);
        _listaServiceMock.ObtenerParaVentaAsync(lista.IdLista, Arg.Any<CancellationToken>()).Returns(lista);
        _listaDialogMock.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(confirmaAviso);
    }

    /// <summary>
    /// Simula el diálogo de guardado: escribe el nombre y ejecuta el comando Guardar del ViewModel del diálogo, que es
    /// el que llama al servicio, igual que lo haría el botón.
    /// </summary>
    private void ConfigurarGuardadoDeLista(string nombre, ListaDto resultado)
    {
        _listaServiceMock.CrearAsync(Arg.Any<CrearListaDto>(), Arg.Any<CancellationToken>()).Returns(resultado);
        _listaDialogMock.MostrarGuardarLista(Arg.Any<GuardarListaViewModel>()).Returns(ci =>
        {
            var vm = ci.Arg<GuardarListaViewModel>();
            vm.Nombre = nombre;
            vm.GuardarCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return vm.Guardada;
        });
    }

    private static ListaDto ListaGuardada(int id, string nombre)
    {
        return new ListaDto
        {
            IdLista = id,
            Nombre = nombre,
            FechaModificacion = new DateTime(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc),
            Renglones = [],
            TotalEstimado = 0m
        };
    }

    private async Task<PosViewModel> CrearViewModelConPresupuestoRecuperadoAsync(bool vigente)
    {
        var viewModel = await CrearViewModelAsync();
        _presupuestoDialogMock.MostrarRecuperarPresupuesto(Arg.Any<RecuperarPresupuestoViewModel>())
            .Returns(DatosDeMuestra.PresupuestoParaVenta(vigente));
        _presupuestoDialogMock.MostrarAlertaPreciosPresupuesto(Arg.Any<AlertaPreciosPresupuestoViewModel>()).Returns(true);

        await viewModel.RecuperarPresupuestoCommand.ExecuteAsync(null);
        return viewModel;
    }

    private static ClienteDto ClienteDeMostrador(int id, string nombre)
    {
        return new ClienteDto
        {
            IdCliente = id,
            RazonSocialONombre = nombre,
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30711111114",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m,
            SaldoCuentaCorriente = 0m
        };
    }

    private static PresupuestoDto PresupuestoEmitido()
    {
        return new PresupuestoDto
        {
            IdPresupuesto = 123,
            IdUsuario = 2,
            FechaEmision = DateTime.UtcNow,
            FechaVencimiento = new DateOnly(2026, 12, 17),
            Subtotal = 6200m,
            Descuento = 200m,
            Total = 6000m,
            Estado = EstadoPresupuestoEnum.Pendiente,
            EstaVigente = true,
            Items = []
        };
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
            .VerificarTicketAsync(Arg.Any<IReadOnlyList<DetalleVentaDto>>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(verificacion);
    }
}
