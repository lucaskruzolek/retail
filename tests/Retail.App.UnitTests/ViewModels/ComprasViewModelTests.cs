using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Compras;
using Retail.Application.DTOs.Compras;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Tests de ComprasViewModel:
/// - C-5: Clasifica correctamente DomainException, ConflictoDeConcurrenciaException y errores genéricos, usando CargaSerializada (H-19).
/// - P2.4: Importación de pedidos emitidos, confirmación de reemplazo, desvinculación y envío de IdPedidoOrigen en el DTO.
/// </summary>
public sealed class ComprasViewModelTests : IDisposable
{
    private const int IdProveedor = 1;

    private readonly IProveedorService _proveedorService;
    private readonly ICompraService _compraService;
    private readonly IPedidoService _pedidoService;
    private readonly ICompraDialogService _dialogService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<ComprasViewModel> _logger;
    private readonly ComprasViewModel _sut;

    // Proveedor de prueba seleccionado por defecto.
    private static readonly ProveedorDto Proveedor = new()
    {
        IdProveedor = IdProveedor,
        RazonSocial = "Editorial Norte S.A.",
        Cuit = "20123456786",
    };

    public ComprasViewModelTests()
    {
        _proveedorService = Substitute.For<IProveedorService>();
        _compraService = Substitute.For<ICompraService>();
        _pedidoService = Substitute.For<IPedidoService>();
        _dialogService = Substitute.For<ICompraDialogService>();
        _session = Substitute.For<ICurrentUserSession>();
        _logger = Substitute.For<ILogger<ComprasViewModel>>();

        _session.IdUsuario.Returns(2);

        _sut = new ComprasViewModel(_proveedorService, _compraService, _pedidoService, _dialogService, _session, _logger);
    }

    // ── C-5: Regla de negocio (DomainException) ───────────────────────────────

    [Fact]
    public async Task RegistrarCompraAsync_DomainException_MuestraMensajeYConservaGrilla()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _compraService
            .RegistrarCompraAsync(Arg.Any<CrearCompraDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El comprobante ya fue registrado para este proveedor."));

        // Act
        await _sut.RegistrarCompraCommand.ExecuteAsync(null);

        // Assert — mensaje de error claro, grilla conservada, no se logueó un error inesperado.
        _sut.MensajeError.Should().Contain("ya fue registrado");
        _sut.DetalleCompraItems.Should().HaveCount(1, "la grilla se conserva para reintentar");
        _sut.IsBusy.Should().BeFalse();
        _logger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── C-5: Conflicto de concurrencia (otra terminal modificó el artículo) ───

    [Fact]
    public async Task RegistrarCompraAsync_ConflictoConcurrencia_MuestraMensajeYConservaGrilla()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _compraService
            .RegistrarCompraAsync(Arg.Any<CrearCompraDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictoDeConcurrenciaException("Otro usuario modificó el artículo. Intente de nuevo."));

        // Act
        await _sut.RegistrarCompraCommand.ExecuteAsync(null);

        // Assert — mensaje de alerta, grilla conservada, no se logueó como error inesperado.
        _sut.MensajeError.Should().NotBeNullOrEmpty();
        _sut.DetalleCompraItems.Should().HaveCount(1, "la grilla se conserva para reintentar");
        _sut.IsBusy.Should().BeFalse();
        _logger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── C-5: Error inesperado (falla de red, base de datos, etc.) ────────────

    [Fact]
    public async Task RegistrarCompraAsync_ErrorInesperado_RegistraEnLogYMuestraMensajeGenerico()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _compraService
            .RegistrarCompraAsync(Arg.Any<CrearCompraDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Falla de conexión de SQL Server."));

        // Act
        await _sut.RegistrarCompraCommand.ExecuteAsync(null);

        // Assert — mensaje genérico (sin stack trace), grilla conservada, se registró en el log de error.
        _sut.MensajeError.Should().NotContain("SQL Server", "el mensaje no debe exponer detalles técnicos");
        _sut.MensajeError.Should().NotBeNullOrEmpty();
        _sut.DetalleCompraItems.Should().HaveCount(1, "la grilla se conserva para reintentar");
        _sut.IsBusy.Should().BeFalse();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── P2.4: Importar Pedidos ────────────────────────────────────────────────

    [Fact]
    public void PuedeImportarPedido_SinProveedor_EsFalso()
    {
        _sut.ProveedorSeleccionado = null;
        _sut.PuedeImportarPedido.Should().BeFalse();
        _sut.ImportarPedidoCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void PuedeImportarPedido_ConProveedor_EsVerdadero()
    {
        _sut.ProveedorSeleccionado = Proveedor;
        _sut.PuedeImportarPedido.Should().BeTrue();
        _sut.ImportarPedidoCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task ImportarPedidoAsync_GrillaConItemsYUsuarioCancela_NoAbreModalNiModificaGrilla()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ImportarPedidoCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).Confirmar(Arg.Any<string>(), Arg.Any<string>());
        await _dialogService.DidNotReceive().MostrarSeleccionarPedidoModalAsync(Arg.Any<int>());
        _sut.DetalleCompraItems.Should().HaveCount(1);
        _sut.TienePedidoOrigen.Should().BeFalse();
    }

    [Fact]
    public async Task ImportarPedidoAsync_PedidoNoDisponible_MuestraAlertaYNoModificaGrilla()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var pedidoEmitido = new PedidoEmitidoDto
        {
            IdPedido = 15,
            FechaEmision = DateTime.UtcNow,
            CantidadRenglones = 1,
            TotalEstimado = 1200m,
        };

        _dialogService.MostrarSeleccionarPedidoModalAsync(IdProveedor).Returns(pedidoEmitido);
        _pedidoService.ObtenerParaCompraAsync(15, Arg.Any<CancellationToken>()).Returns((PedidoParaCompraDto?)null);

        // Act
        await _sut.ImportarPedidoCommand.ExecuteAsync(null);

        // Assert
        _dialogService.Received(1).MostrarAlerta(Arg.Any<string>(), Arg.Any<string>());
        _sut.MensajeError.Should().Contain("no está disponible");
        _sut.DetalleCompraItems.Should().HaveCount(1, "la grilla no se vacía si el pedido no estuvo disponible");
        _sut.TienePedidoOrigen.Should().BeFalse();
    }

    [Fact]
    public async Task ImportarPedidoAsync_CaminoFeliz_PrecargaRenglonesYVinculaPedido()
    {
        // Arrange
        _sut.ProveedorSeleccionado = Proveedor;

        var pedidoEmitido = new PedidoEmitidoDto
        {
            IdPedido = 25,
            FechaEmision = DateTime.UtcNow,
            CantidadRenglones = 2,
            TotalEstimado = 4500m,
        };

        var pedidoDetalle = new PedidoParaCompraDto
        {
            IdPedido = 25,
            IdProveedor = IdProveedor,
            Renglones = new List<RenglonPedidoParaCompraDto>
            {
                new()
                {
                    IdArticulo = 101,
                    CodigoProveedor = "ART-101",
                    DescripcionArticulo = "Cuaderno Rivadavia",
                    Cantidad = 5,
                    CostoPactado = 500m,
                },
                new()
                {
                    IdArticulo = 102,
                    CodigoProveedor = "ART-102",
                    DescripcionArticulo = "Regla 30cm",
                    Cantidad = 10,
                    CostoPactado = 200m,
                },
            },
        };

        _dialogService.MostrarSeleccionarPedidoModalAsync(IdProveedor).Returns(pedidoEmitido);
        _pedidoService.ObtenerParaCompraAsync(25, Arg.Any<CancellationToken>()).Returns(pedidoDetalle);

        // Act
        await _sut.ImportarPedidoCommand.ExecuteAsync(null);

        // Assert
        _sut.DetalleCompraItems.Should().HaveCount(2);
        _sut.DetalleCompraItems[0].IdArticulo.Should().Be(101);
        _sut.DetalleCompraItems[0].Cantidad.Should().Be(5);
        _sut.DetalleCompraItems[0].CostoUnitario.Should().Be(500m);
        _sut.DetalleCompraItems[1].IdArticulo.Should().Be(102);
        _sut.DetalleCompraItems[1].Cantidad.Should().Be(10);
        _sut.DetalleCompraItems[1].CostoUnitario.Should().Be(200m);

        _sut.IdPedidoOrigen.Should().Be(25);
        _sut.InformacionPedidoOrigen.Should().Be("Pedido #25");
        _sut.TienePedidoOrigen.Should().BeTrue();
        _sut.TotalCompra.Should().Be(5 * 500m + 10 * 200m);
    }

    [Fact]
    public void DesvincularPedido_LimpiaIdPedidoOrigenYConservaRenglones()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _sut.IdPedidoOrigen = 99;
        _sut.InformacionPedidoOrigen = "Pedido #99";
        _sut.TienePedidoOrigen = true;

        // Act
        _sut.DesvincularPedidoCommand.Execute(null);

        // Assert
        _sut.IdPedidoOrigen.Should().BeNull();
        _sut.InformacionPedidoOrigen.Should().BeNull();
        _sut.TienePedidoOrigen.Should().BeFalse();
        _sut.DetalleCompraItems.Should().HaveCount(1, "los renglones deben conservarse al desvincular");
    }

    [Fact]
    public async Task RegistrarCompraAsync_ConPedidoOrigen_EnviaIdPedidoOrigenEnDto()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _sut.IdPedidoOrigen = 77;
        _sut.TienePedidoOrigen = true;

        CrearCompraDto? dtoEnviado = null;
        _compraService
            .RegistrarCompraAsync(Arg.Do<CrearCompraDto>(d => dtoEnviado = d), Arg.Any<CancellationToken>())
            .Returns(new CompraResponseDto
            {
                IdCompra = 1,
                IdProveedor = IdProveedor,
                TipoComprobante = "FACTURA",
                NumeroComprobante = "0001-00000099",
                FechaEmision = DateTime.UtcNow,
                Subtotal = 300m,
                Total = 300m,
                ArticulosActualizados = 1,
                IdPedidoOrigen = 77,
                Items = [],
            });

        // Act
        await _sut.RegistrarCompraCommand.ExecuteAsync(null);

        // Assert
        dtoEnviado.Should().NotBeNull();
        dtoEnviado!.IdPedidoOrigen.Should().Be(77);
        _sut.IdPedidoOrigen.Should().BeNull("LimpiarFormulario() debe resetear IdPedidoOrigen tras el registro");
        _sut.TienePedidoOrigen.Should().BeFalse();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ConfigurarFormularioConUnItem()
    {
        _sut.ProveedorSeleccionado = Proveedor;
        _sut.TipoComprobante = "FACTURA";
        _sut.NumeroComprobante = "0001-00000099";
        _sut.FechaEmision = DateTime.Today.AddDays(-1);
        _sut.DetalleCompraItems.Add(new DetalleCompraItemViewModel
        {
            IdArticulo = 10,
            DescripcionArticulo = "Lapicera",
            Cantidad = 2,
            CostoUnitario = 150m,
        });
    }
}
