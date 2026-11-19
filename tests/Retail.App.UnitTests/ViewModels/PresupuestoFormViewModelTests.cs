using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Comun;
using Retail.App.ViewModels.Presupuestos;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class PresupuestoFormViewModelTests : IDisposable
{
    private const int IdUsuario = 3;
    private const int IdOriginal = 41;
    private const int IdNuevo = 45;

    // Hoy (hora local de la prueba, UTC-3): 19/11/2026
    private static readonly DateTimeOffset Ahora = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    private readonly IPresupuestoService _presupuestoService = Substitute.For<IPresupuestoService>();
    private readonly IVentaService _ventaService = Substitute.For<IVentaService>();
    private readonly IClienteService _clienteService = Substitute.For<IClienteService>();
    private readonly IVentaDialogService _ventaDialogService = Substitute.For<IVentaDialogService>();
    private readonly IPresupuestoDialogService _dialogService = Substitute.For<IPresupuestoDialogService>();
    private readonly ICurrentUserSession _sesion = Substitute.For<ICurrentUserSession>();
    private readonly CargaSerializada _acceso = new();

    public PresupuestoFormViewModelTests()
    {
        _sesion.IdUsuario.Returns(IdUsuario);
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .Returns(Emitido(IdNuevo));
        _presupuestoService.ReemplazarAsync(Arg.Any<int>(), Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .Returns(Emitido(IdNuevo));
    }

    public void Dispose()
    {
        _acceso.Dispose();
        GC.SuppressFinalize(this);
    }

    private PresupuestoFormViewModel CrearNuevo()
    {
        return Crear(null);
    }

    private PresupuestoFormViewModel Crear(PresupuestoParaVentaDto? aCorregir)
    {
        return new PresupuestoFormViewModel(
            aCorregir,
            _presupuestoService,
            _ventaService,
            _clienteService,
            _ventaDialogService,
            _dialogService,
            _sesion,
            _acceso,
            new RelojDePrueba(Ahora),
            NullLogger.Instance);
    }

    private static PresupuestoDto Emitido(int id)
    {
        return new PresupuestoDto
        {
            IdPresupuesto = id,
            IdUsuario = IdUsuario,
            FechaEmision = new DateTime(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc),
            FechaVencimiento = new DateOnly(2026, 12, 4),
            Subtotal = 7500m,
            Descuento = 0m,
            Total = 7500m,
            Estado = EstadoPresupuestoEnum.Pendiente,
            EstaVigente = true,
            Items = []
        };
    }

    private static RenglonEditableFila Fila(int idArticulo, decimal precio, int cantidad, bool disponible = true)
    {
        return new RenglonEditableFila(idArticulo, $"779000{idArticulo}", $"Artículo {idArticulo}", precio, cantidad, disponible);
    }

    private static ClienteDto Cliente(int id, string nombre)
    {
        return new ClienteDto
        {
            IdCliente = id,
            RazonSocialONombre = nombre,
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "30111222",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m,
            SaldoCuentaCorriente = 0m
        };
    }

    private static RenglonPresupuestoParaVentaDto RenglonOriginal(int idArticulo, decimal pactado, decimal actual, int cantidad)
    {
        return new RenglonPresupuestoParaVentaDto
        {
            IdArticulo = idArticulo,
            CodigoBarras = $"779000{idArticulo}",
            Descripcion = $"Artículo {idArticulo}",
            Cantidad = cantidad,
            PrecioPactado = pactado,
            PrecioActual = actual,
            PrecioACobrar = pactado,
            EsServicio = false,
            StockActual = 50
        };
    }

    private static PresupuestoParaVentaDto Original(
        IReadOnlyList<RenglonPresupuestoParaVentaDto> renglones,
        IReadOnlyList<string>? noDisponibles = null,
        int? idCliente = null)
    {
        return new PresupuestoParaVentaDto
        {
            IdPresupuesto = IdOriginal,
            Titulo = "Lista escolar",
            IdCliente = idCliente,
            ClienteNombre = idCliente is null ? null : "Escuela 12",
            EstaVigente = true,
            FechaVencimiento = new DateOnly(2026, 11, 25),
            Descuento = 500m,
            Renglones = renglones,
            Discrepancias = [],
            Faltantes = [],
            ArticulosNoDisponibles = noDisponibles ?? []
        };
    }

    private void PrepararVerificacion(IReadOnlyList<PrecioActualizadoDto> cambios, IReadOnlyList<string>? noDisponibles = null)
    {
        _ventaService.VerificarTicketAsync(Arg.Any<IReadOnlyList<DetalleVentaDto>>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new VerificacionTicketDto
            {
                PreciosActualizados = cambios,
                Faltantes = [],
                ArticulosNoDisponibles = noDisponibles ?? []
            });
    }

    // ------------------------------------------------------------------ //
    // Nuevo presupuesto                                                   //
    // ------------------------------------------------------------------ //

    [Fact]
    public void Nuevo_AlAbrir_EmpiezaVacioConLosValoresPorOmision()
    {
        var sut = CrearNuevo();

        sut.EsCorreccion.Should().BeFalse();
        sut.TituloVentana.Should().Be("Nuevo presupuesto");
        sut.ClienteNombre.Should().Be("Consumidor Final");
        sut.TieneCliente.Should().BeFalse();
        sut.VigenciaDias.Should().Be(15d);
        sut.Descuento.Should().Be(0d);
        sut.Editor.Renglones.Should().BeEmpty();
        sut.GuardarCommand.CanExecute(null).Should().BeFalse("no hay ningún artículo");
    }

    [Fact]
    public void TextoValidoHasta_SeCalculaDesdeHoyConLaVigencia()
    {
        var sut = CrearNuevo();

        sut.TextoValidoHasta.Should().Be("Válido hasta 04/12/2026");

        sut.VigenciaDias = 1d;

        sut.TextoValidoHasta.Should().Be("Válido hasta 20/11/2026");
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(91d)]
    [InlineData(null)]
    public void Vigencia_FueraDeRango_EsInvalidaYDeshabilitaGuardar(double? dias)
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        sut.VigenciaDias = dias;

        sut.VigenciaEsValida.Should().BeFalse();
        sut.TieneErrorVigencia.Should().BeTrue();
        sut.TextoValidoHasta.Should().BeEmpty();
        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(90d)]
    public void Vigencia_EnElLimite_EsValida(double dias)
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        sut.VigenciaDias = dias;

        sut.VigenciaEsValida.Should().BeTrue();
        sut.GuardarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Descuento_MayorAlSubtotal_EsInvalidoYDeshabilitaGuardar()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        sut.Descuento = 7501d;

        sut.DescuentoEsValido.Should().BeFalse();
        sut.TieneErrorDescuento.Should().BeTrue();
        sut.DescuentoAplicado.Should().Be(0m, "un descuento inválido no se resta en los totales");
        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Descuento_Negativo_EsInvalido()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        sut.Descuento = -1d;

        sut.DescuentoEsValido.Should().BeFalse();
    }

    [Fact]
    public void Descuento_IgualAlSubtotal_EsValidoYElTotalEsCero()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        sut.Descuento = 7500d;

        sut.DescuentoEsValido.Should().BeTrue();
        sut.Total.Should().Be(0m);
    }

    [Fact]
    public void Totales_ConRenglonesYDescuento_SumanEnPesos()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3), Fila(2, 500m, 2)]);

        sut.Descuento = 1000d;

        sut.Subtotal.Should().Be(8500m);
        sut.DescuentoAplicado.Should().Be(1000m);
        sut.Total.Should().Be(7500m);
    }

    [Fact]
    public void Descuento_SeRecalculaAlCambiarElSubtotal_SiElDescuentoYaNoEntra()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);
        sut.Descuento = 7000d;
        sut.DescuentoEsValido.Should().BeTrue();

        sut.Editor.Renglones[0].Cantidad = 1;

        sut.DescuentoEsValido.Should().BeFalse("el subtotal bajó a 2.500");
        sut.GuardarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Guardar_SoloConArticulosDadosDeBaja_NoSePuedeEjecutar()
    {
        var sut = CrearNuevo();

        sut.Editor.CargarRenglones([Fila(1, 2500m, 3, disponible: false)]);

        sut.GuardarCommand.CanExecute(null).Should().BeFalse("hace falta al menos un artículo disponible");
    }

    [Fact]
    public async Task Guardar_Nuevo_EmiteConUsuarioClienteTituloVigenciaDescuentoYRenglones()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3), Fila(2, 500m, 2)]);
        sut.Titulo = "  Útiles 3.º grado  ";
        sut.Cliente = Cliente(9, "Escuela 12");
        sut.VigenciaDias = 30d;
        sut.Descuento = 500d;

        await sut.GuardarCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).EmitirAsync(
            Arg.Is<CrearPresupuestoDto>(d =>
                d.IdUsuario == IdUsuario &&
                d.IdCliente == 9 &&
                d.Titulo == "Útiles 3.º grado" &&
                d.VigenciaDias == 30 &&
                d.Descuento == 500m &&
                d.Items.Count == 2 &&
                d.Items[0].IdArticulo == 1 && d.Items[0].Cantidad == 3 && d.Items[0].PrecioUnitarioPactado == 2500m &&
                d.Items[1].IdArticulo == 2 && d.Items[1].Cantidad == 2 && d.Items[1].PrecioUnitarioPactado == 500m),
            Arg.Any<CancellationToken>());
        await _presupuestoService.DidNotReceiveWithAnyArgs().ReemplazarAsync(default, default!, default);
    }

    [Fact]
    public async Task Guardar_SinTituloNiCliente_EmiteConTituloNuloYConsumidorFinal()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        await sut.GuardarCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).EmitirAsync(
            Arg.Is<CrearPresupuestoDto>(d => d.Titulo == null && d.IdCliente == null && d.Descuento == 0m && d.VigenciaDias == 15),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_NoEnviaLosRenglonesDadosDeBaja()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3), Fila(2, 500m, 2, disponible: false)]);

        await sut.GuardarCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).EmitirAsync(
            Arg.Is<CrearPresupuestoDto>(d => d.Items.Count == 1 && d.Items[0].IdArticulo == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_Exitoso_PasaAlEstadoGuardadoConElNumero()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.Guardado.Should().BeTrue();
        sut.EstaEditando.Should().BeFalse();
        sut.TextoGuardado.Should().Be("Presupuesto N.º 00000045 guardado · válido hasta 04/12/2026");
        sut.GuardarCommand.CanExecute(null).Should().BeFalse("ya se emitió: no se puede guardar dos veces");
        sut.ImprimirCommand.CanExecute(null).Should().BeTrue();
        sut.ExportarPdfCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Guardar_SinSesion_InformaYNoEmite()
    {
        _sesion.IdUsuario.Returns((int?)null);
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("sesión");
        await _presupuestoService.DidNotReceiveWithAnyArgs().EmitirAsync(default!, default);
    }

    [Fact]
    public async Task Guardar_ErrorInesperado_MuestraUnMensajeGenericoYConservaLoCargado()
    {
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().NotContain("boom");
        sut.TieneMensajeError.Should().BeTrue();
        sut.Guardado.Should().BeFalse();
        sut.Editor.Renglones.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------ //
    // Precio desactualizado (PE-10)                                       //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Guardar_ElPrecioCambio_MuestraElMensajeYOfreceActualizarPrecios()
    {
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El precio de 'Artículo 1' cambió de $2.500,00 a $2.700,00."));
        PrepararVerificacion([new PrecioActualizadoDto { IdArticulo = 1, Descripcion = "Artículo 1", PrecioAnterior = 2500m, PrecioActual = 2700m }]);
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3), Fila(2, 500m, 2)]);

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.Guardado.Should().BeFalse();
        sut.MensajeError.Should().Contain("cambió");
        sut.HayPreciosDesactualizados.Should().BeTrue();
        sut.ActualizarPreciosCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarPrecios_ConPreciosNuevos_ActualizaLosRenglonesYConservaTodoLoDemas()
    {
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El precio de 'Artículo 1' cambió de $2.500,00 a $2.700,00."));
        PrepararVerificacion([new PrecioActualizadoDto { IdArticulo = 1, Descripcion = "Artículo 1", PrecioAnterior = 2500m, PrecioActual = 2700m }]);
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3), Fila(2, 500m, 2)]);
        sut.Titulo = "Útiles";
        sut.Cliente = Cliente(9, "Escuela 12");
        sut.VigenciaDias = 30d;
        await sut.GuardarCommand.ExecuteAsync(null);

        await sut.ActualizarPreciosCommand.ExecuteAsync(null);

        sut.Editor.Renglones[0].PrecioUnitario.Should().Be(2700m);
        sut.Editor.Renglones[0].Cantidad.Should().Be(3);
        sut.Editor.Renglones[1].PrecioUnitario.Should().Be(500m, "el precio que no cambió queda igual");
        sut.Subtotal.Should().Be(9100m);
        sut.Titulo.Should().Be("Útiles");
        sut.ClienteNombre.Should().Be("Escuela 12");
        sut.VigenciaDias.Should().Be(30d);
        sut.HayPreciosDesactualizados.Should().BeFalse();
        sut.TieneMensajeError.Should().BeFalse();
        sut.MensajeAviso.Should().Contain("Se actualizó 1 precio");
    }

    [Fact]
    public async Task ActualizarPrecios_TrasActualizar_ElSiguienteGuardarEmiteConLosPreciosNuevos()
    {
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<PresupuestoDto>(new DomainException("El precio cambió.")),
                _ => Task.FromResult(Emitido(IdNuevo)));
        PrepararVerificacion([new PrecioActualizadoDto { IdArticulo = 1, Descripcion = "Artículo 1", PrecioAnterior = 2500m, PrecioActual = 2700m }]);
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);
        await sut.GuardarCommand.ExecuteAsync(null);
        await sut.ActualizarPreciosCommand.ExecuteAsync(null);

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.Guardado.Should().BeTrue();
        await _presupuestoService.Received(1).EmitirAsync(
            Arg.Is<CrearPresupuestoDto>(d => d.Items[0].PrecioUnitarioPactado == 2700m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_ArticuloDadoDeBaja_MuestraElMensajeYElRenglonSePuedeQuitar()
    {
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El artículo 'Artículo 2' ya no está disponible: fue dado de baja."));
        PrepararVerificacion([], ["Artículo 2"]);
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3), Fila(2, 500m, 2)]);

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("fue dado de baja");
        sut.HayPreciosDesactualizados.Should().BeFalse("no hay precios distintos que actualizar");

        sut.Editor.QuitarRenglonCommand.Execute(sut.Editor.Renglones[1]);

        sut.Editor.Renglones.Should().HaveCount(1);
        sut.GuardarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarPrecios_HayArticulosDadosDeBaja_LosNombraEnElMensaje()
    {
        _presupuestoService.EmitirAsync(Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El precio cambió."));
        PrepararVerificacion(
            [new PrecioActualizadoDto { IdArticulo = 1, Descripcion = "Artículo 1", PrecioAnterior = 2500m, PrecioActual = 2700m }],
            ["Artículo 2"]);
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3), Fila(2, 500m, 2)]);
        await sut.GuardarCommand.ExecuteAsync(null);

        await sut.ActualizarPreciosCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("Artículo 2");
    }

    // ------------------------------------------------------------------ //
    // Cliente                                                             //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SeleccionarCliente_ElUsuarioElige_QuedaElCliente()
    {
        var elegido = Cliente(9, "Escuela 12");
        _ventaDialogService.MostrarSeleccionarClienteModalAsync(null).Returns(elegido);
        var sut = CrearNuevo();

        await sut.SeleccionarClienteCommand.ExecuteAsync(null);

        sut.Cliente.Should().Be(elegido);
        sut.ClienteNombre.Should().Be("Escuela 12");
        sut.TieneCliente.Should().BeTrue();
    }

    [Fact]
    public async Task SeleccionarCliente_ElUsuarioCancela_ConservaElClienteActual()
    {
        var actual = Cliente(9, "Escuela 12");
        _ventaDialogService.MostrarSeleccionarClienteModalAsync(actual).Returns(actual);
        var sut = CrearNuevo();
        sut.Cliente = actual;

        await sut.SeleccionarClienteCommand.ExecuteAsync(null);

        sut.Cliente.Should().Be(actual);
    }

    [Fact]
    public void QuitarCliente_VuelveAConsumidorFinal()
    {
        var sut = CrearNuevo();
        sut.Cliente = Cliente(9, "Escuela 12");

        sut.QuitarClienteCommand.Execute(null);

        sut.Cliente.Should().BeNull();
        sut.ClienteNombre.Should().Be("Consumidor Final");
    }

    // ------------------------------------------------------------------ //
    // Corrección (PE-02, PE-05, PE-06, PE-11)                             //
    // ------------------------------------------------------------------ //

    [Fact]
    public void Correccion_AlAbrir_PrecargaTituloDescuentoYLosPreciosDeHoy()
    {
        var original = Original([RenglonOriginal(1, pactado: 2500m, actual: 2700m, cantidad: 3), RenglonOriginal(2, 500m, 500m, 2)]);

        var sut = Crear(original);

        sut.EsCorreccion.Should().BeTrue();
        sut.TituloVentana.Should().Be("Corregir presupuesto N.º 00000041");
        sut.Titulo.Should().Be("Lista escolar");
        sut.Descuento.Should().Be(500d);
        sut.VigenciaDias.Should().Be(15d, "la vigencia del corregido corre desde hoy (PE-05)");
        sut.Editor.Renglones.Should().HaveCount(2);
        sut.Editor.Renglones[0].PrecioUnitario.Should().Be(2700m, "el corregido toma el precio de catálogo de hoy (PE-06)");
        sut.Editor.Renglones[0].Cantidad.Should().Be(3);
        sut.GuardarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Correccion_ArticulosNoDisponibles_NoSePrecarganYSeAvisa()
    {
        var original = Original([RenglonOriginal(1, 2500m, 2500m, 3)], noDisponibles: ["Tijera escolar", "Regla 30 cm"]);

        var sut = Crear(original);

        sut.Editor.Renglones.Should().ContainSingle();
        sut.MensajeAviso.Should().Contain("Se omitieron 2 artículos dados de baja").And.Contain("Tijera escolar").And.Contain("Regla 30 cm");
    }

    [Fact]
    public void Correccion_PreciosQueCambiaron_SeCuentanRespectoDelOriginal()
    {
        var original = Original(
        [
            RenglonOriginal(1, pactado: 2500m, actual: 2700m, cantidad: 1),
            RenglonOriginal(2, pactado: 500m, actual: 450m, cantidad: 1),
            RenglonOriginal(3, pactado: 100m, actual: 150m, cantidad: 1),
            RenglonOriginal(4, pactado: 800m, actual: 800m, cantidad: 1)
        ]);

        var sut = Crear(original);

        sut.TieneAviso.Should().BeTrue();
        sut.MensajeAviso.Should().Be("3 precios cambiaron respecto del presupuesto original.");
    }

    [Fact]
    public void Correccion_SinCambiosDePrecio_NoMuestraAviso()
    {
        var sut = Crear(Original([RenglonOriginal(1, 2500m, 2500m, 3)]));

        sut.TieneAviso.Should().BeFalse();
    }

    [Fact]
    public async Task Correccion_ClienteExistente_QuedaPrecargadoAlInicializar()
    {
        _clienteService.ObtenerClientePorIdAsync(9, Arg.Any<CancellationToken>()).Returns(Cliente(9, "Escuela 12"));
        var sut = Crear(Original([RenglonOriginal(1, 2500m, 2500m, 3)], idCliente: 9));

        await sut.InicializarAsync();

        sut.ClienteNombre.Should().Be("Escuela 12");
        sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Correccion_ClienteDadoDeBaja_QuedaConsumidorFinalYAvisa()
    {
        _clienteService.ObtenerClientePorIdAsync(9, Arg.Any<CancellationToken>()).Returns((ClienteDto?)null);
        var sut = Crear(Original([RenglonOriginal(1, 2500m, 2500m, 3)], idCliente: 9));

        await sut.InicializarAsync();

        sut.Cliente.Should().BeNull();
        sut.ClienteNombre.Should().Be("Consumidor Final");
        sut.MensajeAviso.Should().Contain("ya no existe");
    }

    [Fact]
    public async Task Correccion_FallaLaBusquedaDelCliente_QuedaConsumidorFinalYAvisa()
    {
        _clienteService.ObtenerClientePorIdAsync(9, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("sin conexión"));
        var sut = Crear(Original([RenglonOriginal(1, 2500m, 2500m, 3)], idCliente: 9));

        await sut.InicializarAsync();

        sut.Cliente.Should().BeNull();
        sut.TieneAviso.Should().BeTrue();
        sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Correccion_PresupuestoSinCliente_NoBuscaNada()
    {
        var sut = Crear(Original([RenglonOriginal(1, 2500m, 2500m, 3)]));

        await sut.InicializarAsync();

        await _clienteService.DidNotReceiveWithAnyArgs().ObtenerClientePorIdAsync(default, default);
    }

    [Fact]
    public async Task Correccion_Guardar_LlamaAReemplazarConElOriginalYElNuevoContenido()
    {
        var sut = Crear(Original([RenglonOriginal(1, pactado: 2500m, actual: 2700m, cantidad: 3)]));

        await sut.GuardarCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).ReemplazarAsync(
            IdOriginal,
            Arg.Is<CrearPresupuestoDto>(d =>
                d.IdUsuario == IdUsuario &&
                d.Titulo == "Lista escolar" &&
                d.Descuento == 500m &&
                d.VigenciaDias == 15 &&
                d.Items.Count == 1 &&
                d.Items[0].PrecioUnitarioPactado == 2700m),
            Arg.Any<CancellationToken>());
        await _presupuestoService.DidNotReceiveWithAnyArgs().EmitirAsync(default!, default);
        sut.Guardado.Should().BeTrue();
        sut.TextoGuardado.Should().Be("Se emitió el presupuesto N.º 00000045, que reemplaza al N.º 00000041.");
    }

    [Fact]
    public async Task Correccion_ElOriginalCambioMientrasSeCorregia_MuestraElErrorYNoCierra()
    {
        _presupuestoService.ReemplazarAsync(IdOriginal, Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El presupuesto N.º 41 cambió mientras lo corregía (por ejemplo, se cobró). Vuelva a abrirlo."));
        var sut = Crear(Original([RenglonOriginal(1, 2500m, 2500m, 3)]));

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.Guardado.Should().BeFalse();
        sut.EstaEditando.Should().BeTrue();
        sut.MensajeError.Should().Contain("cambió mientras lo corregía");
        sut.Editor.Renglones.Should().HaveCount(1, "lo cargado se conserva");

        // El conflicto llega como DomainException, igual que un precio desactualizado: el ViewModel verifica los precios,
        // pero sin diferencias reales no ofrece "Actualizar precios".
        sut.HayPreciosDesactualizados.Should().BeFalse();
    }

    [Fact]
    public async Task Correccion_ElOriginalYaNoEstaPendiente_MuestraElErrorSinConsultarPrecios()
    {
        _presupuestoService.ReemplazarAsync(IdOriginal, Arg.Any<CrearPresupuestoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TransicionPresupuestoInvalidaException(EstadoPresupuestoEnum.Convertido, "reemplazar"));
        var sut = Crear(Original([RenglonOriginal(1, 2500m, 2500m, 3)]));

        await sut.GuardarCommand.ExecuteAsync(null);

        sut.Guardado.Should().BeFalse();
        sut.TieneMensajeError.Should().BeTrue();
        sut.HayPreciosDesactualizados.Should().BeFalse();
        await _ventaService.DidNotReceiveWithAnyArgs().VerificarTicketAsync(default!, default, default);
    }

    // ------------------------------------------------------------------ //
    // Después de guardar: imprimir y PDF                                  //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Imprimir_TrasGuardar_ImprimeElNuevoPresupuestoYAvisa()
    {
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);
        await sut.GuardarCommand.ExecuteAsync(null);

        await sut.ImprimirCommand.ExecuteAsync(null);

        await _presupuestoService.Received(1).ImprimirAsync(IdNuevo, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Presupuesto impreso", Arg.Is<string>(m => m.Contains("00000045")));
    }

    [Fact]
    public async Task Imprimir_LaImpresoraFalla_MuestraElErrorSinPerderElEstadoGuardado()
    {
        _presupuestoService.ImprimirAsync(IdNuevo, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("sin papel"));
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);
        await sut.GuardarCommand.ExecuteAsync(null);

        await sut.ImprimirCommand.ExecuteAsync(null);

        _dialogService.Received(1).MostrarError("Error inesperado", Arg.Is<string>(m => !m.Contains("sin papel")));
        sut.Guardado.Should().BeTrue();
    }

    [Fact]
    public async Task ExportarPdf_ElUsuarioCancelaLaRuta_NoLlamaAlServicio()
    {
        _dialogService.ElegirRutaPdf(Arg.Any<string>()).Returns((string?)null);
        var sut = CrearNuevo();
        sut.Editor.CargarRenglones([Fila(1, 2500m, 3)]);
        await sut.GuardarCommand.ExecuteAsync(null);

        await sut.ExportarPdfCommand.ExecuteAsync(null);

        _dialogService.Received(1).ElegirRutaPdf("Presupuesto-00000045.pdf");
        await _presupuestoService.DidNotReceiveWithAnyArgs().ExportarPdfAsync(default, default!, default);
    }
}
