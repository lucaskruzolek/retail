using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.ViewModels.Compras;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public sealed class SeleccionarPedidoModalViewModelTests : IDisposable
{
    private const int IdProveedor = 42;

    private readonly IPedidoService _pedidoService;
    private readonly ILogger<SeleccionarPedidoModalViewModel> _logger;
    private readonly SeleccionarPedidoModalViewModel _sut;

    public SeleccionarPedidoModalViewModelTests()
    {
        _pedidoService = Substitute.For<IPedidoService>();
        _logger = Substitute.For<ILogger<SeleccionarPedidoModalViewModel>>();
        _sut = new SeleccionarPedidoModalViewModel(IdProveedor, "Editorial Norte S.A.", _pedidoService, _logger);
    }

    [Fact]
    public async Task CargarPedidosAsync_ConPedidosEmitidos_PopulaColeccionYFinalizaBusy()
    {
        // Arrange
        var pedidos = new List<PedidoEmitidoDto>
        {
            new()
            {
                IdPedido = 101,
                FechaEmision = DateTime.UtcNow.AddDays(-1),
                CantidadRenglones = 3,
                TotalEstimado = 15000m,
            },
            new()
            {
                IdPedido = 102,
                FechaEmision = DateTime.UtcNow,
                CantidadRenglones = 1,
                TotalEstimado = 5000m,
            },
        };

        _pedidoService
            .ListarEmitidosPorProveedorAsync(IdProveedor, Arg.Any<CancellationToken>())
            .Returns(pedidos);

        // Act
        await _sut.CargarPedidosCommand.ExecuteAsync(null);

        // Assert
        _sut.Pedidos.Should().HaveCount(2);
        _sut.Pedidos[0].IdPedido.Should().Be(101);
        _sut.Pedidos[1].IdPedido.Should().Be(102);
        _sut.IsBusy.Should().BeFalse();
        _sut.MensajeError.Should().BeNull();
    }

    [Fact]
    public async Task CargarPedidosAsync_FallaServicio_RegistraErrorYMensajeUsuario()
    {
        // Arrange
        _pedidoService
            .ListarEmitidosPorProveedorAsync(IdProveedor, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Conexión perdida con la base de datos"));

        // Act
        await _sut.CargarPedidosCommand.ExecuteAsync(null);

        // Assert
        _sut.Pedidos.Should().BeEmpty();
        _sut.MensajeError.Should().NotBeNullOrEmpty();
        _sut.IsBusy.Should().BeFalse();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void SeleccionarPedido_PedidoValido_FijaPedidoElegidoYConfirmaOperacion()
    {
        // Arrange
        var pedido = new PedidoEmitidoDto
        {
            IdPedido = 50,
            FechaEmision = DateTime.UtcNow,
            CantidadRenglones = 2,
            TotalEstimado = 8900m,
        };

        // Act
        _sut.SeleccionarPedido(pedido);

        // Assert
        _sut.PedidoElegido.Should().Be(pedido);
        _sut.OperacionConfirmada.Should().BeTrue();
    }

    [Fact]
    public void EstaVacio_AntesDeLaPrimeraCarga_EsFalso()
    {
        // Evita mostrar "no tiene pedidos" mientras la carga inicial todavía no empezó.
        _sut.EstaVacio.Should().BeFalse();
    }

    [Fact]
    public async Task EstaVacio_ProveedorSinPedidosEmitidos_EsVerdadero()
    {
        // Arrange
        _pedidoService
            .ListarEmitidosPorProveedorAsync(IdProveedor, Arg.Any<CancellationToken>())
            .Returns(new List<PedidoEmitidoDto>());

        // Act
        await _sut.CargarPedidosCommand.ExecuteAsync(null);

        // Assert
        _sut.EstaVacio.Should().BeTrue();
        _sut.TienePedidoSeleccionado.Should().BeFalse();
    }

    [Fact]
    public async Task EstaVacio_ConPedidosOConError_EsFalso()
    {
        // Arrange: con pedidos.
        _pedidoService
            .ListarEmitidosPorProveedorAsync(IdProveedor, Arg.Any<CancellationToken>())
            .Returns(new List<PedidoEmitidoDto>
            {
                new() { IdPedido = 1, FechaEmision = DateTime.UtcNow, CantidadRenglones = 1, TotalEstimado = 100m },
            });
        await _sut.CargarPedidosCommand.ExecuteAsync(null);
        _sut.EstaVacio.Should().BeFalse();

        // Arrange: con error de carga (la lista queda vacía pero no se afirma que el proveedor no tenga pedidos).
        _pedidoService
            .ListarEmitidosPorProveedorAsync(IdProveedor, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Conexión perdida."));
        _sut.Pedidos.Clear();
        await _sut.CargarPedidosCommand.ExecuteAsync(null);

        // Assert
        _sut.MensajeError.Should().NotBeNullOrEmpty();
        _sut.TieneMensajeError.Should().BeTrue();
        _sut.EstaVacio.Should().BeFalse();
    }

    [Fact]
    public void RazonSocialProveedor_AlConstruir_ExponeElNombreParaElEncabezado()
    {
        _sut.RazonSocialProveedor.Should().Be("Editorial Norte S.A.");
    }

    [Fact]
    public void SeleccionarPedido_PedidoNulo_NoModificaEstado()
    {
        // Act
        _sut.SeleccionarPedido(null);

        // Assert
        _sut.PedidoElegido.Should().BeNull();
        _sut.OperacionConfirmada.Should().BeFalse();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}

