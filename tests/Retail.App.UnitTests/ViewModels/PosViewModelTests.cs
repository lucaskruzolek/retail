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

        _cajaServiceMock.ObtenerTurnoActivoAsync().Returns(new TurnoCajaDto
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

    private PosViewModel CrearViewModel()
    {
        return new PosViewModel(_ventaServiceMock, _cajaServiceMock, _sessionMock, _dialogServiceMock);
    }

    [Fact]
    public async Task ProcesarEnterAsync_ConArticuloResaltadoEnPopup_DebeAgregarAlTicketYCerrarPopup()
    {
        // Arrange
        var viewModel = CrearViewModel();
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
        var viewModel = CrearViewModel();
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
    public void IncrementarYDecrementarCantidad_DebeActualizarSubtotalesYTotales()
    {
        // Arrange
        var viewModel = CrearViewModel();
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
    public void EliminarItem_DebeQuitarFilaYRecalcularTotales()
    {
        // Arrange
        var viewModel = CrearViewModel();
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
    public void LimpiarVenta_ConConfirmacionPositiva_DebeVaciarTicket()
    {
        // Arrange
        var viewModel = CrearViewModel();
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
        var viewModel = CrearViewModel();
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
        var viewModel = CrearViewModel();
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
        var viewModel = CrearViewModel();
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
