using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Compras;
using Retail.Application.DTOs.Compras;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Tests de C-5: ComprasViewModel clasifica correctamente DomainException,
/// ConflictoDeConcurrenciaException y errores genéricos, y usa CargaSerializada (H-19).
/// </summary>
public sealed class ComprasViewModelTests : IDisposable
{
    private const int IdProveedor = 1;

    private readonly IProveedorService _proveedorService;
    private readonly ICompraService _compraService;
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
        _session = Substitute.For<ICurrentUserSession>();
        _logger = Substitute.For<ILogger<ComprasViewModel>>();

        _session.IdUsuario.Returns(2);

        _sut = new ComprasViewModel(_proveedorService, _compraService, _session, _logger);
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

