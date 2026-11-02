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
using Retail.Domain.Entities;
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
        await _dialogService.DidNotReceive().MostrarSeleccionarPedidoModalAsync(Arg.Any<int>(), Arg.Any<string>());
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

        _dialogService.MostrarSeleccionarPedidoModalAsync(IdProveedor, Proveedor.RazonSocial).Returns(pedidoEmitido);
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
                    CostoReposicionActual = 500m,
                    PorcentajeGanancia = 40m,
                    PrecioVentaActual = 500m * 1.4m,
                },
                new()
                {
                    IdArticulo = 102,
                    CodigoProveedor = "ART-102",
                    DescripcionArticulo = "Regla 30cm",
                    Cantidad = 10,
                    CostoPactado = 200m,
                    CostoReposicionActual = 200m,
                    PorcentajeGanancia = 40m,
                    PrecioVentaActual = 200m * 1.4m,
                },
            },
        };

        _dialogService.MostrarSeleccionarPedidoModalAsync(IdProveedor, Proveedor.RazonSocial).Returns(pedidoEmitido);
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

    // ── Total y habilitación reactivos ────────────────────────────────────────

    [Fact]
    public void TotalCompra_EditarCostoDeUnRenglon_SeRecalculaYNotifica()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        var notificadas = new List<string?>();
        _sut.PropertyChanged += (_, e) => notificadas.Add(e.PropertyName);

        // Act
        _sut.DetalleCompraItems[0].CostoUnitario = 200m;

        // Assert
        _sut.TotalCompra.Should().Be(2 * 200m);
        notificadas.Should().Contain(nameof(ComprasViewModel.TotalCompra));
    }

    [Fact]
    public void TotalCompra_AlLimpiarLaGrilla_VuelveACero()
    {
        // Arrange
        ConfigurarFormularioConUnItem();

        // Act
        _sut.DetalleCompraItems.Clear();

        // Assert
        _sut.TotalCompra.Should().Be(0m);
        _sut.GrillaVacia.Should().BeTrue();
    }

    [Fact]
    public void RegistrarCompraCommand_ConFaltantes_NoSePuedeEjecutar()
    {
        // Arrange: solo hay proveedor, falta el número y los renglones.
        _sut.ProveedorSeleccionado = Proveedor;

        // Assert
        _sut.RegistrarCompraCommand.CanExecute(null).Should().BeFalse();
        _sut.TieneFaltantes.Should().BeTrue();
        _sut.Faltantes.Should().Contain("número de comprobante").And.Contain("al menos un renglón");
    }

    [Fact]
    public void RegistrarCompraCommand_FormularioCompleto_SeHabilita()
    {
        // Arrange
        ConfigurarFormularioConUnItem();

        // Assert
        _sut.RegistrarCompraCommand.CanExecute(null).Should().BeTrue();
        _sut.PuedeRegistrar.Should().BeTrue();
        _sut.Faltantes.Should().BeEmpty();
    }

    [Fact]
    public void RegistrarCompraCommand_RenglonConCostoCero_SeDeshabilitaYSeHabilitaAlCorregirlo()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        var renglon = _sut.DetalleCompraItems[0];

        // Act + Assert: el cambio de un renglón actualiza la habilitación sin tocar el encabezado.
        renglon.CostoUnitario = 0m;
        _sut.RegistrarCompraCommand.CanExecute(null).Should().BeFalse();
        _sut.Faltantes.Should().Contain("costo");

        renglon.CostoUnitario = 90m;
        _sut.RegistrarCompraCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void RegistrarCompraCommand_RenglonConCantidadCero_SeDeshabilita()
    {
        // Arrange
        ConfigurarFormularioConUnItem();

        // Act
        _sut.DetalleCompraItems[0].Cantidad = 0;

        // Assert
        _sut.RegistrarCompraCommand.CanExecute(null).Should().BeFalse();
        _sut.Faltantes.Should().Contain("cantidad");
    }

    // ── Renglones ─────────────────────────────────────────────────────────────

    [Fact]
    public void QuitarRenglon_RenglonExistente_LoQuitaYActualizaElTotal()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        var renglon = _sut.DetalleCompraItems[0];

        // Act
        _sut.QuitarRenglonCommand.Execute(renglon);

        // Assert
        _sut.DetalleCompraItems.Should().BeEmpty();
        _sut.TotalCompra.Should().Be(0m);
        _sut.RegistrarCompraCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void AgregarArticulo_ArticuloNuevo_AgregaRenglonConSuCostoYMarkup()
    {
        // Arrange
        _sut.ProveedorSeleccionado = Proveedor;

        // Act
        _sut.AgregarArticuloCommand.Execute(ArticuloDeBusqueda(7, "Lapicera Bic", costo: 100m, markup: 50m, precio: 150m));

        // Assert
        _sut.DetalleCompraItems.Should().ContainSingle();
        var renglon = _sut.DetalleCompraItems[0];
        renglon.IdArticulo.Should().Be(7);
        renglon.Cantidad.Should().Be(1);
        renglon.CostoUnitario.Should().Be(100m);
        renglon.PorcentajeGanancia.Should().Be(50m);
        renglon.CantidadPedida.Should().BeNull();
        _sut.RenglonSeleccionado.Should().BeSameAs(renglon);
    }

    [Fact]
    public void AgregarArticulo_ArticuloYaEnLaGrilla_NoDuplicaLoSeleccionaYAvisa()
    {
        // Arrange
        _sut.ProveedorSeleccionado = Proveedor;
        var articulo = ArticuloDeBusqueda(7, "Lapicera Bic", costo: 100m, markup: 50m, precio: 150m);
        _sut.AgregarArticuloCommand.Execute(articulo);
        _sut.RenglonSeleccionado = null;

        // Act
        _sut.AgregarArticuloCommand.Execute(articulo);

        // Assert
        _sut.DetalleCompraItems.Should().ContainSingle();
        _sut.RenglonSeleccionado.Should().BeSameAs(_sut.DetalleCompraItems[0]);
        _sut.MensajeEstado.Should().Contain("ya está en la compra");
    }

    // ── Cambio de proveedor ───────────────────────────────────────────────────

    [Fact]
    public void ProveedorSeleccionado_CambiarConRenglonesYConfirmar_LimpiaRenglonesYPedido()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _sut.IdPedidoOrigen = 5;
        _sut.TienePedidoOrigen = true;
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var otro = new ProveedorDto { IdProveedor = 2, RazonSocial = "Distribuidora Sur", Cuit = "30712345678" };

        // Act
        _sut.ProveedorSeleccionado = otro;

        // Assert
        _sut.ProveedorSeleccionado.Should().Be(otro);
        _sut.DetalleCompraItems.Should().BeEmpty();
        _sut.TienePedidoOrigen.Should().BeFalse();
        _sut.IdPedidoOrigen.Should().BeNull();
    }

    [Fact]
    public void ProveedorSeleccionado_CambiarConRenglonesYRechazar_RevierteAlProveedorAnterior()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var otro = new ProveedorDto { IdProveedor = 2, RazonSocial = "Distribuidora Sur", Cuit = "30712345678" };

        // Act
        _sut.ProveedorSeleccionado = otro;

        // Assert: vuelve al anterior, conserva los renglones y pregunta una sola vez (sin reentrada).
        _sut.ProveedorSeleccionado.Should().Be(Proveedor);
        _sut.DetalleCompraItems.Should().HaveCount(1);
        _dialogService.Received(1).Confirmar(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void ProveedorSeleccionado_PrimeraEleccionSinRenglones_NoPideConfirmacion()
    {
        // Act
        _sut.ProveedorSeleccionado = Proveedor;

        // Assert
        _dialogService.DidNotReceive().Confirmar(Arg.Any<string>(), Arg.Any<string>());
        _sut.ProveedorSeleccionado.Should().Be(Proveedor);
    }

    // ── Errores y datos del registro ──────────────────────────────────────────

    [Fact]
    public async Task RegistrarCompraAsync_RestriccionDeUnicidad_MuestraMensajeDeComprobanteDuplicado()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _compraService
            .RegistrarCompraAsync(Arg.Any<CrearCompraDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RestriccionDeUnicidadException());

        // Act
        await _sut.RegistrarCompraCommand.ExecuteAsync(null);

        // Assert
        _sut.MensajeError.Should().Be("Ese comprobante ya fue registrado para este proveedor (posiblemente desde otra terminal).");
        _sut.TieneMensajeError.Should().BeTrue();
        _sut.DetalleCompraItems.Should().HaveCount(1, "la grilla se conserva para reintentar");
        _logger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RegistrarCompraAsync_TipoYFechaElegidos_ViajanEnElDto()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _sut.TipoComprobante = Compra.TipoRemito;
        var fecha = new DateTime(2026, 10, 15);
        _sut.FechaEmision = fecha;

        CrearCompraDto? dtoEnviado = null;
        _compraService
            .RegistrarCompraAsync(Arg.Do<CrearCompraDto>(d => dtoEnviado = d), Arg.Any<CancellationToken>())
            .Returns(RespuestaDeCompra());

        // Act
        await _sut.RegistrarCompraCommand.ExecuteAsync(null);

        // Assert
        dtoEnviado.Should().NotBeNull();
        dtoEnviado!.TipoComprobante.Should().Be("REMITO");
        dtoEnviado.FechaEmision.Should().Be(fecha.ToUniversalTime());
    }

    [Fact]
    public async Task RegistrarCompraAsync_Exito_LimpiaElFormularioIncluidoElProveedorYAvisa()
    {
        // Arrange
        ConfigurarFormularioConUnItem();
        _compraService
            .RegistrarCompraAsync(Arg.Any<CrearCompraDto>(), Arg.Any<CancellationToken>())
            .Returns(RespuestaDeCompra());

        // Act
        await _sut.RegistrarCompraCommand.ExecuteAsync(null);

        // Assert
        _sut.ProveedorSeleccionado.Should().BeNull();
        _sut.NumeroComprobante.Should().BeEmpty();
        _sut.DetalleCompraItems.Should().BeEmpty();
        _sut.TipoComprobante.Should().Be(Compra.TipoFactura);
        _sut.TieneMensajeEstado.Should().BeTrue();
        _sut.MensajeEstado.Should().Contain("registrada");
        _dialogService.DidNotReceive().Confirmar(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ImportarPedidoAsync_ErrorAlLeerElPedido_MuestraErrorSinAlertaDeNoDisponible()
    {
        // Arrange
        _sut.ProveedorSeleccionado = Proveedor;
        _dialogService.MostrarSeleccionarPedidoModalAsync(IdProveedor, Proveedor.RazonSocial).Returns(new PedidoEmitidoDto
        {
            IdPedido = 15,
            FechaEmision = DateTime.UtcNow,
            CantidadRenglones = 1,
            TotalEstimado = 1200m,
        });
        _pedidoService
            .ObtenerParaCompraAsync(15, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Conexión perdida."));

        // Act
        await _sut.ImportarPedidoCommand.ExecuteAsync(null);

        // Assert
        _dialogService.DidNotReceive().MostrarAlerta(Arg.Any<string>(), Arg.Any<string>());
        _sut.MensajeError.Should().Contain("Error al obtener");
        _sut.MensajeError.Should().NotContain("no está disponible");
        _sut.IsBusy.Should().BeFalse();
    }

    // ── Buscador de artículos ─────────────────────────────────────────────────

    [Fact]
    public async Task BuscarArticulosAsync_ConProveedorYTermino_ConsultaElCatalogoYMuestraResultados()
    {
        // Arrange
        _sut.ProveedorSeleccionado = Proveedor;
        var resultados = new List<ArticuloParaCompraDto>
        {
            ArticuloDeBusqueda(7, "Lapicera Bic", costo: 100m, markup: 50m, precio: 150m),
            ArticuloDeBusqueda(8, "Lapicera Faber", costo: 120m, markup: 40m, precio: 168m),
        };
        _compraService
            .BuscarArticulosAsync("lap", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(resultados);
        _sut.TextoBusqueda = "lap";

        // Act
        await _sut.BuscarArticulosCommand.ExecuteAsync(null);

        // Assert
        await _compraService.Received().BuscarArticulosAsync("lap", Arg.Any<int>(), Arg.Any<CancellationToken>());
        _sut.ResultadosBusqueda.Should().HaveCount(2);
        _sut.ResultadoSeleccionado.Should().Be(resultados[0]);
        _sut.MostrarResultados.Should().BeTrue();
        _sut.SinResultados.Should().BeFalse();
    }

    [Fact]
    public async Task BuscarArticulosAsync_SinProveedor_NoConsultaElCatalogo()
    {
        // Arrange
        _sut.TextoBusqueda = "lap";

        // Act
        await _sut.BuscarArticulosCommand.ExecuteAsync(null);

        // Assert
        await _compraService.DidNotReceive().BuscarArticulosAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        _sut.ResultadosBusqueda.Should().BeEmpty();
        _sut.MostrarResultados.Should().BeFalse();
    }

    [Fact]
    public async Task BuscarArticulosAsync_SinCoincidencias_MarcaSinResultados()
    {
        // Arrange
        _sut.ProveedorSeleccionado = Proveedor;
        _compraService
            .BuscarArticulosAsync("zzz", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ArticuloParaCompraDto>());
        _sut.TextoBusqueda = "zzz";

        // Act
        await _sut.BuscarArticulosCommand.ExecuteAsync(null);

        // Assert
        _sut.SinResultados.Should().BeTrue();
        _sut.MostrarResultados.Should().BeTrue();
    }

    [Fact]
    public async Task AgregarResultadoSeleccionado_TrasBuscar_AgregaElRenglonYLimpiaElBuscador()
    {
        // Arrange
        _sut.ProveedorSeleccionado = Proveedor;
        _compraService
            .BuscarArticulosAsync("lap", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ArticuloParaCompraDto> { ArticuloDeBusqueda(7, "Lapicera Bic", costo: 100m, markup: 50m, precio: 150m) });
        _sut.TextoBusqueda = "lap";
        await _sut.BuscarArticulosCommand.ExecuteAsync(null);

        // Act
        _sut.AgregarResultadoSeleccionadoCommand.Execute(null);

        // Assert
        _sut.DetalleCompraItems.Should().ContainSingle(r => r.IdArticulo == 7);
        _sut.TextoBusqueda.Should().BeEmpty();
        _sut.ResultadosBusqueda.Should().BeEmpty();
        _sut.MostrarResultados.Should().BeFalse();
    }

    [Fact]
    public async Task MoverSeleccionBusqueda_ConVariosResultados_AvanzaYRetrocedeSinSalirseDeLosLimites()
    {
        // Arrange
        _sut.ProveedorSeleccionado = Proveedor;
        var resultados = new List<ArticuloParaCompraDto>
        {
            ArticuloDeBusqueda(7, "Lapicera Bic", costo: 100m, markup: 50m, precio: 150m),
            ArticuloDeBusqueda(8, "Lapicera Faber", costo: 120m, markup: 40m, precio: 168m),
        };
        _compraService
            .BuscarArticulosAsync("lap", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(resultados);
        _sut.TextoBusqueda = "lap";
        await _sut.BuscarArticulosCommand.ExecuteAsync(null);

        // Act + Assert
        _sut.MoverSeleccionBusquedaArribaCommand.Execute(null);
        _sut.ResultadoSeleccionado.Should().Be(resultados[0]);

        _sut.MoverSeleccionBusquedaAbajoCommand.Execute(null);
        _sut.MoverSeleccionBusquedaAbajoCommand.Execute(null);
        _sut.ResultadoSeleccionado.Should().Be(resultados[1]);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ArticuloParaCompraDto ArticuloDeBusqueda(int id, string descripcion, decimal costo, decimal markup, decimal precio)
    {
        return new ArticuloParaCompraDto
        {
            IdArticulo = id,
            CodigoBarras = "779000" + id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Descripcion = descripcion,
            CostoReposicion = costo,
            PorcentajeGanancia = markup,
            PrecioVenta = precio,
        };
    }

    private static CompraResponseDto RespuestaDeCompra()
    {
        return new CompraResponseDto
        {
            IdCompra = 1,
            IdProveedor = IdProveedor,
            TipoComprobante = "FACTURA",
            NumeroComprobante = "0001-00000099",
            FechaEmision = DateTime.UtcNow,
            Subtotal = 300m,
            Total = 300m,
            ArticulosActualizados = 1,
            Items = [],
        };
    }

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
