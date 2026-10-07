using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Ventas;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class PosViewModelTests
{
    private readonly IVentaService _ventaServiceMock;
    private readonly ICajaService _cajaServiceMock;
    private readonly ICurrentUserSession _sessionMock;
    private readonly IVentaDialogService _dialogServiceMock;

    public PosViewModelTests()
    {
        _ventaServiceMock = Substitute.For<IVentaService>();
        _cajaServiceMock = Substitute.For<ICajaService>();
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
    }

    /// <summary>
    /// Crea el ViewModel y espera la lectura inicial del estado de la caja, que el constructor lanza en segundo
    /// plano: así ningún test puede ser pisado por ella después de asignar CajaAbierta.
    /// </summary>
    private async Task<PosViewModel> CrearViewModelAsync()
    {
        var viewModel = new PosViewModel(_ventaServiceMock, _cajaServiceMock, _sessionMock, _dialogServiceMock);
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

        _ventaServiceMock.RegistrarVentaAsync(Arg.Any<CrearVentaDto>())
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
            dto.Items.Count == 1));
        viewModel.Items.Should().BeEmpty();
        viewModel.Total.Should().Be(0m);
    }
}
