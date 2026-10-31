using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Comun;
using Retail.App.ViewModels.Pedidos;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class PedidosViewModelTests : IDisposable
{
    private const int IdPedido = 12;
    private const int IdCatalogo = 40;

    private readonly IPedidoService _pedidoService = Substitute.For<IPedidoService>();
    private readonly IProveedorService _proveedorService = Substitute.For<IProveedorService>();
    private readonly IPedidoDialogService _dialogService = Substitute.For<IPedidoDialogService>();
    private readonly PedidosViewModel _sut;

    public PedidosViewModelTests()
    {
        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>()).Returns(new List<ProveedorDto>
        {
            new() { IdProveedor = 2, RazonSocial = "Zeta Papelera", Cuit = "30-22222222-2" },
            new() { IdProveedor = 1, RazonSocial = "Alfa Distribuidora", Cuit = "30-11111111-1" }
        });
        PrepararListado(EstadoPedidoEnum.Borrador);
        PrepararDetalle(EstadoPedidoEnum.Borrador);

        _sut = new PedidosViewModel(
            _pedidoService,
            _proveedorService,
            _dialogService,
            new RelojDePrueba(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<PedidosViewModel>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    private void PrepararListado(EstadoPedidoEnum estado)
    {
        _pedidoService.ListarPedidosAsync(Arg.Any<ConsultaPedidosDto>(), Arg.Any<CancellationToken>()).Returns(new PedidosPaginadosDto
        {
            Items = new[]
            {
                new PedidoResumenDto
                {
                    IdPedido = IdPedido,
                    IdProveedor = 1,
                    RazonSocialProveedor = "Alfa Distribuidora",
                    Estado = estado,
                    FechaCreacion = new DateTime(2026, 10, 27, 15, 0, 0),
                    CantidadRenglones = 1,
                    TotalEstimado = 3000m
                }
            },
            TotalRegistros = 1,
            PaginaActual = 1,
            TamanoPagina = 20
        });
    }

    private void PrepararDetalle(EstadoPedidoEnum estado, int renglones = 1)
    {
        _pedidoService.ObtenerDetalleAsync(IdPedido, Arg.Any<CancellationToken>()).Returns(new PedidoDetalleDto
        {
            IdPedido = IdPedido,
            IdProveedor = 1,
            RazonSocialProveedor = "Alfa Distribuidora",
            Estado = estado,
            FechaCreacion = new DateTime(2026, 10, 27, 15, 0, 0),
            CantidadRenglones = renglones,
            TotalEstimado = 3000m,
            MotivoCancelacion = estado == EstadoPedidoEnum.Cancelado ? "Sin stock en el proveedor" : null,
            Renglones = Enumerable.Range(0, renglones).Select(i => new RenglonPedidoDto
            {
                IdCatalogoProveedor = IdCatalogo + i,
                IdArticulo = 100 + i,
                CodigoProveedor = $"COD-{i}",
                Descripcion = $"Cuaderno {i}",
                Cantidad = 2,
                CostoPactado = 1500m,
                Subtotal = 3000m
            }).ToList()
        });
    }

    /// <summary>Inicializa la pantalla y selecciona el único pedido, esperando a que se muestre su detalle.</summary>
    private async Task InicializarYSeleccionarAsync()
    {
        await _sut.InicializarAsync();
        _sut.PedidoSeleccionado = _sut.Pedidos.Single();
        await EsperarAsync(() => _sut.Detalle is not null);
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        for (int i = 0; i < 100 && !condicion(); i++)
        {
            await Task.Delay(20);
        }

        condicion().Should().BeTrue("la condición debía cumplirse tras la carga asíncrona");
    }

    private int LlamadasAlListado => _pedidoService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IPedidoService.ListarPedidosAsync));

    /// <summary>Marca las opciones indicadas del filtro y lo aplica, como hace el usuario con las casillas y "Aplicar".</summary>
    private static void AplicarFiltro(FiltroColumnaViewModel filtro, params int[] ids)
    {
        foreach (var opcion in filtro.Grupos.SelectMany(g => g.Opciones))
        {
            opcion.Seleccionada = ids.Contains(opcion.Id);
        }

        filtro.AplicarCommand.Execute(null);
    }

    [Fact]
    public async Task InicializarAsync_CargaOpcionesDeProveedorOrdenadasYPrimeraPaginaSinFiltros()
    {
        await _sut.InicializarAsync();

        _sut.FiltroProveedor.Grupos.SelectMany(g => g.Opciones).Select(o => o.Nombre)
            .Should().Equal("Alfa Distribuidora", "Zeta Papelera");
        _sut.FiltroEstado.Grupos.SelectMany(g => g.Opciones).Select(o => o.Id)
            .Should().Equal((int)EstadoPedidoEnum.Borrador, (int)EstadoPedidoEnum.Emitido, (int)EstadoPedidoEnum.Recibido, (int)EstadoPedidoEnum.Cancelado);
        _sut.Pedidos.Should().ContainSingle().Which.IdPedido.Should().Be(IdPedido);
        await _pedidoService.Received(1).ListarPedidosAsync(
            Arg.Is<ConsultaPedidosDto>(c => c.Estados.Count == 0 && c.IdsProveedor.Count == 0 && c.Pagina == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FiltroEstado_AplicaVariosEstados_ConsultaPorEstadosDesdeLaPrimeraPagina()
    {
        await _sut.InicializarAsync();
        _sut.PaginaActual = 3;

        AplicarFiltro(_sut.FiltroEstado, (int)EstadoPedidoEnum.Borrador, (int)EstadoPedidoEnum.Emitido);

        await EsperarAsync(() => LlamadasAlListado == 2);
        _sut.PaginaActual.Should().Be(1);
        await _pedidoService.Received(1).ListarPedidosAsync(
            Arg.Is<ConsultaPedidosDto>(c => c.Estados.SequenceEqual(new[] { EstadoPedidoEnum.Borrador, EstadoPedidoEnum.Emitido }) && c.Pagina == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FiltroProveedor_AplicaUnProveedor_ConsultaPorEseProveedor()
    {
        await _sut.InicializarAsync();

        AplicarFiltro(_sut.FiltroProveedor, 2);

        await EsperarAsync(() => LlamadasAlListado == 2);
        await _pedidoService.Received(1).ListarPedidosAsync(
            Arg.Is<ConsultaPedidosDto>(c => c.IdsProveedor.Count == 1 && c.IdsProveedor[0] == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TextoBusqueda_Cambia_ConsultaConElTermino()
    {
        await _sut.InicializarAsync();

        _sut.TextoBusqueda = "alfa";

        await EsperarAsync(() => LlamadasAlListado == 2);
        await _pedidoService.Received(1).ListarPedidosAsync(
            Arg.Is<ConsultaPedidosDto>(c => c.TerminoBusqueda == "alfa" && c.Pagina == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HayFiltrosActivos_ConFiltroDeColumna_HabilitaLimpiarFiltros()
    {
        await _sut.InicializarAsync();
        _sut.LimpiarFiltrosCommand.CanExecute(null).Should().BeFalse();

        AplicarFiltro(_sut.FiltroEstado, (int)EstadoPedidoEnum.Cancelado);

        _sut.HayFiltrosActivos.Should().BeTrue();
        _sut.LimpiarFiltrosCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task LimpiarFiltros_ConBusquedaYFiltros_QuitaTodoYConsultaUnaSolaVez()
    {
        await _sut.InicializarAsync();
        AplicarFiltro(_sut.FiltroProveedor, 1);
        await EsperarAsync(() => LlamadasAlListado == 2);
        _sut.TextoBusqueda = "alfa";
        await EsperarAsync(() => LlamadasAlListado == 3);

        _sut.LimpiarFiltrosCommand.Execute(null);

        await EsperarAsync(() => LlamadasAlListado == 4);
        _sut.TextoBusqueda.Should().BeEmpty();
        _sut.FiltroProveedor.HayFiltroActivo.Should().BeFalse();
        _sut.HayFiltrosActivos.Should().BeFalse();
        LlamadasAlListado.Should().Be(4, "limpiar todo consulta una sola vez");
        // Sin filtros: la carga inicial y la de limpiar
        await _pedidoService.Received(2).ListarPedidosAsync(
            Arg.Is<ConsultaPedidosDto>(c => string.IsNullOrEmpty(c.TerminoBusqueda) && c.Estados.Count == 0 && c.IdsProveedor.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PedidoSeleccionado_Borrador_MuestraDetalleConRenglonesEditablesYPermiteEmitir()
    {
        await InicializarYSeleccionarAsync();

        _sut.EsBorrador.Should().BeTrue();
        _sut.Renglones.Should().ContainSingle().Which.EsEditable.Should().BeTrue();
        _sut.EmitirCommand.CanExecute(null).Should().BeTrue();
        _sut.PuedeCancelarse.Should().BeTrue();
    }

    [Theory]
    [InlineData(EstadoPedidoEnum.Emitido, false, true)]
    [InlineData(EstadoPedidoEnum.Recibido, false, false)]
    [InlineData(EstadoPedidoEnum.Cancelado, false, false)]
    public async Task PedidoSeleccionado_SegunEstado_HabilitaSoloLasAccionesPermitidas(
        EstadoPedidoEnum estado, bool puedeEmitir, bool puedeCancelarse)
    {
        PrepararListado(estado);
        PrepararDetalle(estado);

        await InicializarYSeleccionarAsync();

        _sut.EmitirCommand.CanExecute(null).Should().Be(puedeEmitir);
        _sut.PuedeCancelarse.Should().Be(puedeCancelarse);
        _sut.Renglones.Should().OnlyContain(r => !r.EsEditable, "solo un borrador permite editar renglones");
    }

    [Fact]
    public async Task PedidoSeleccionado_BorradorSinRenglones_NoPermiteEmitir()
    {
        PrepararDetalle(EstadoPedidoEnum.Borrador, renglones: 0);

        await InicializarYSeleccionarAsync();

        _sut.EmitirCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task EmitirCommand_UsuarioNoConfirma_NoLlamaAlServicio()
    {
        await InicializarYSeleccionarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.EmitirCommand.ExecuteAsync(null);

        await _pedidoService.DidNotReceive().EmitirAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirCommand_Confirmado_EmiteYRecarga()
    {
        await InicializarYSeleccionarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _pedidoService.ClearReceivedCalls();

        await _sut.EmitirCommand.ExecuteAsync(null);

        await _pedidoService.Received(1).EmitirAsync(IdPedido, Arg.Any<CancellationToken>());
        await _pedidoService.Received(1).ListarPedidosAsync(Arg.Any<ConsultaPedidosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelarCommand_SinMotivo_EstaDeshabilitado()
    {
        await InicializarYSeleccionarAsync();

        _sut.MotivoParaCancelar = "   ";

        _sut.CancelarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task CancelarCommand_ConMotivoYConfirmado_CancelaConElMotivoSinEspacios()
    {
        await InicializarYSeleccionarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _sut.MotivoParaCancelar = "  El proveedor no tiene stock  ";

        await _sut.CancelarCommand.ExecuteAsync(null);

        await _pedidoService.Received(1).CancelarAsync(
            Arg.Is<CancelarPedidoDto>(d => d.IdPedido == IdPedido && d.Motivo == "El proveedor no tiene stock"),
            Arg.Any<CancellationToken>());
        _dialogService.Received(1).Confirmar("Cancelar pedido", Arg.Is<string>(m => m.Contains("El proveedor no tiene stock")));
    }

    [Fact]
    public async Task EmitirCommand_TransicionInvalida_MuestraAlertaYRecarga()
    {
        await InicializarYSeleccionarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _pedidoService.EmitirAsync(IdPedido, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TransicionPedidoInvalidaException(EstadoPedidoEnum.Cancelado, "emitir"));
        _pedidoService.ClearReceivedCalls();

        await _sut.EmitirCommand.ExecuteAsync(null);

        _dialogService.Received(1).MostrarAlerta("El pedido cambió de estado", Arg.Any<string>());
        await _pedidoService.Received(1).ListarPedidosAsync(Arg.Any<ConsultaPedidosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmitirCommand_ConflictoDeConcurrencia_MuestraAlertaYRecarga()
    {
        await InicializarYSeleccionarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _pedidoService.EmitirAsync(IdPedido, Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());
        _pedidoService.ClearReceivedCalls();

        await _sut.EmitirCommand.ExecuteAsync(null);

        _dialogService.Received(1).MostrarAlerta("Pedido modificado en otra terminal", Arg.Any<string>());
        await _pedidoService.Received(1).ListarPedidosAsync(Arg.Any<ConsultaPedidosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelarCommand_ValidacionRechazada_MuestraElMensajeEnLinea()
    {
        await InicializarYSeleccionarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _sut.MotivoParaCancelar = "x";
        _pedidoService.CancelarAsync(Arg.Any<CancelarPedidoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("Motivo", "El motivo es demasiado corto.") }));

        await _sut.CancelarCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("El motivo es demasiado corto.");
        _dialogService.DidNotReceive().MostrarAlerta(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EmitirCommand_ErrorInesperado_MuestraErrorGenericoSinDetallesTecnicos()
    {
        await InicializarYSeleccionarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _pedidoService.EmitirAsync(IdPedido, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("detalle interno"));

        await _sut.EmitirCommand.ExecuteAsync(null);

        _dialogService.Received(1).MostrarError("Error inesperado", Arg.Is<string>(m => !m.Contains("detalle interno")));
    }

    [Fact]
    public async Task AplicarCantidadCommand_RenglonConCambios_GuardaLaNuevaCantidad()
    {
        await InicializarYSeleccionarAsync();
        var renglon = _sut.Renglones.Single();
        renglon.Cantidad = 7;

        await _sut.AplicarCantidadCommand.ExecuteAsync(renglon);

        await _pedidoService.Received(1).CambiarCantidadAsync(
            Arg.Is<CambiarCantidadRenglonDto>(d => d.IdPedido == IdPedido && d.IdCatalogoProveedor == IdCatalogo && d.Cantidad == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AplicarCantidadCommand_CantidadInvalida_NoLlamaAlServicio()
    {
        await InicializarYSeleccionarAsync();
        var renglon = _sut.Renglones.Single();
        renglon.Cantidad = 0;

        await _sut.AplicarCantidadCommand.ExecuteAsync(renglon);

        renglon.TieneCambios.Should().BeFalse();
        await _pedidoService.DidNotReceive().CambiarCantidadAsync(Arg.Any<CambiarCantidadRenglonDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuitarRenglonCommand_Confirmado_QuitaElRenglon()
    {
        await InicializarYSeleccionarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _sut.QuitarRenglonCommand.ExecuteAsync(_sut.Renglones.Single());

        await _pedidoService.Received(1).QuitarRenglonAsync(IdPedido, IdCatalogo, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CargarPedidos_PedidoSeleccionadoSigueEnLaPagina_ConservaLaSeleccionYRecargaElDetalle()
    {
        await InicializarYSeleccionarAsync();
        PrepararDetalle(EstadoPedidoEnum.Emitido);

        await _sut.CargarPedidosAsync();

        _sut.PedidoSeleccionado.Should().NotBeNull();
        _sut.PedidoSeleccionado!.IdPedido.Should().Be(IdPedido);
        await EsperarAsync(() => _sut.Detalle?.Estado == EstadoPedidoEnum.Emitido);
    }
}
