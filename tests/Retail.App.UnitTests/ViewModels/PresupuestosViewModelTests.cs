using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.UnitTests.Helpers;
using Retail.App.ViewModels.Comun;
using Retail.App.ViewModels.Presupuestos;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class PresupuestosViewModelTests : IDisposable
{
    private const int IdPresupuesto = 123;

    private readonly IPresupuestoService _presupuestoService = Substitute.For<IPresupuestoService>();
    private readonly IPresupuestoDialogService _dialogService = Substitute.For<IPresupuestoDialogService>();
    private readonly PresupuestosViewModel _sut;
    private readonly List<string> _archivosTemporales = [];

    public PresupuestosViewModelTests()
    {
        PrepararListado(Resumen(IdPresupuesto, EstadoPresupuestoEnum.Pendiente, vigente: true));

        _sut = new PresupuestosViewModel(
            _presupuestoService,
            _dialogService,
            new RelojDePrueba(new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero)),
            NullLogger<PresupuestosViewModel>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();

        foreach (var archivo in _archivosTemporales.Where(File.Exists))
        {
            File.Delete(archivo);
        }

        GC.SuppressFinalize(this);
    }

    private static PresupuestoResumenDto Resumen(int id, EstadoPresupuestoEnum estado, bool vigente, string? titulo = null, string? cliente = null)
    {
        return new PresupuestoResumenDto
        {
            IdPresupuesto = id,
            Titulo = titulo,
            FechaEmision = new DateTime(2026, 11, 14, 13, 20, 0, DateTimeKind.Utc),
            FechaVencimiento = new DateOnly(2026, 11, 29),
            ClienteNombre = cliente,
            UsuarioNombre = "Lucía",
            Total = 12500m,
            Estado = estado,
            EstaVigente = vigente,
            CantidadRenglones = 2
        };
    }

    private void PrepararListado(params PresupuestoResumenDto[] items)
    {
        _presupuestoService.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>()).Returns(new PresupuestosPaginadosDto
        {
            Items = items,
            TotalRegistros = items.Length,
            PaginaActual = 1,
            TamanoPagina = 15
        });
    }

    private int LlamadasAlListado => _presupuestoService.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IPresupuestoService.ListarAsync));

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        for (int i = 0; i < 100 && !condicion(); i++)
        {
            await Task.Delay(20);
        }

        condicion().Should().BeTrue("la condición debía cumplirse tras la carga asíncrona");
    }

    private string NuevaRutaTemporal()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"presupuesto-{Guid.NewGuid():N}.pdf");
        _archivosTemporales.Add(ruta);
        return ruta;
    }

    // ------------------------------------------------------------------ //
    // Carga, filtro, búsqueda y paginación                                //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task InicializarAsync_CargaLaPrimeraPagina_MuestraLasFilasYConsultaConLosValoresPorOmision()
    {
        await _sut.InicializarAsync();

        _sut.Presupuestos.Should().ContainSingle().Which.IdPresupuesto.Should().Be(IdPresupuesto);
        _sut.TotalRegistros.Should().Be(1);
        _sut.IsBusy.Should().BeFalse();
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c =>
                c.Pagina == 1 && c.TamanoPagina == 15 && c.Estados.Count == 0 && c.IdsCliente.Count == 0 &&
                !c.IncluirConsumidorFinal && c.EmisionDesdeUtc == null && c.EmisionHastaUtc == null &&
                c.VenceDesde == null && c.VenceHasta == null && string.IsNullOrEmpty(c.TerminoBusqueda)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InicializarAsync_CargaLosClientesDelFiltro_ConsumidorFinalPrimeroYLuegoLosClientes()
    {
        _presupuestoService.ListarClientesConPresupuestosAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ClienteConPresupuestosDto { IdCliente = 7, Nombre = "Escuela Nº 5" },
            new ClienteConPresupuestosDto { IdCliente = 3, Nombre = "Ana Pérez" }
        ]);

        await _sut.InicializarAsync();

        var opciones = _sut.FiltroCliente.Grupos.SelectMany(g => g.Opciones).ToList();
        opciones.Select(o => o.Nombre).Should().Equal("Consumidor Final", "Ana Pérez", "Escuela Nº 5");
        opciones.Select(o => o.Id).Should().Equal(0, 3, 7);
        LlamadasAlListado.Should().Be(1, "cargar las opciones no dispara una consulta propia");
    }

    [Fact]
    public async Task InicializarAsync_SinClientesConPresupuestos_ConsumidorFinalSigueDisponible()
    {
        await _sut.InicializarAsync();

        _sut.FiltroCliente.Grupos.SelectMany(g => g.Opciones).Should().ContainSingle().Which.Nombre.Should().Be("Consumidor Final");
    }

    [Fact]
    public async Task FiltroEstado_ConVariosEstados_ConsultaConEllosDesdeLaPrimeraPagina()
    {
        await _sut.InicializarAsync();
        _sut.PaginaActual = 3;

        AplicarOpciones(_sut.FiltroEstado, (int)FiltroEstadoPresupuestoEnum.Vigentes, (int)FiltroEstadoPresupuestoEnum.Anulados);

        await EsperarAsync(() => LlamadasAlListado == 2);
        _sut.PaginaActual.Should().Be(1);
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c =>
                c.Pagina == 1 &&
                c.Estados.SequenceEqual(new[] { FiltroEstadoPresupuestoEnum.Vigentes, FiltroEstadoPresupuestoEnum.Anulados })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FiltroCliente_ConUnClienteReal_ConsultaPorSuIdSinConsumidorFinal()
    {
        PrepararClientes();
        await _sut.InicializarAsync();

        AplicarOpciones(_sut.FiltroCliente, 7);

        await EsperarAsync(() => LlamadasAlListado == 2);
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.IdsCliente.Count == 1 && c.IdsCliente[0] == 7 && !c.IncluirConsumidorFinal),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FiltroCliente_ConConsumidorFinal_SeTraduceALaBanderaYNoEnviaElIdLocal()
    {
        PrepararClientes();
        await _sut.InicializarAsync();

        AplicarOpciones(_sut.FiltroCliente, 0);

        await EsperarAsync(() => LlamadasAlListado == 2);
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.IdsCliente.Count == 0 && c.IncluirConsumidorFinal),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FiltroCliente_ConConsumidorFinalYUnCliente_EnviaAmbos()
    {
        PrepararClientes();
        await _sut.InicializarAsync();

        AplicarOpciones(_sut.FiltroCliente, 0, 7);

        await EsperarAsync(() => LlamadasAlListado == 2);
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.IdsCliente.Count == 1 && c.IdsCliente[0] == 7 && c.IncluirConsumidorFinal),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FiltroEmision_ConUnRango_ConsultaElIntervaloUtcSemiabierto()
    {
        await _sut.InicializarAsync();

        _sut.FiltroEmision.BorradorDesde = new DateTime(2026, 11, 10);
        _sut.FiltroEmision.BorradorHasta = new DateTime(2026, 11, 12);
        _sut.FiltroEmision.AplicarCommand.Execute(null);

        var (desdeEsperado, hastaEsperado) = _sut.FiltroEmision.RangoUtc();
        desdeEsperado.Should().NotBeNull();
        hastaEsperado.Should().Be(desdeEsperado!.Value.AddDays(3));
        await EsperarAsync(() => LlamadasAlListado == 2);
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.EmisionDesdeUtc == desdeEsperado && c.EmisionHastaUtc == hastaEsperado && c.Pagina == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FiltroVencimiento_ConUnRango_ConsultaFechasCalendarioInclusivas()
    {
        await _sut.InicializarAsync();

        _sut.FiltroVencimiento.BorradorDesde = new DateTime(2026, 11, 20, 18, 30, 0);
        _sut.FiltroVencimiento.BorradorHasta = new DateTime(2026, 11, 29);
        _sut.FiltroVencimiento.AplicarCommand.Execute(null);

        await EsperarAsync(() => LlamadasAlListado == 2);
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c =>
                c.VenceDesde == new DateOnly(2026, 11, 20) && c.VenceHasta == new DateOnly(2026, 11, 29) && c.Pagina == 1),
            Arg.Any<CancellationToken>());
    }

    private void PrepararClientes()
    {
        _presupuestoService.ListarClientesConPresupuestosAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ClienteConPresupuestosDto { IdCliente = 7, Nombre = "Escuela Nº 5" },
            new ClienteConPresupuestosDto { IdCliente = 3, Nombre = "Ana Pérez" }
        ]);
    }

    // Marca solo estas casillas y aplica, como lo hace el usuario con el panel del filtro de columna
    private static void AplicarOpciones(FiltroColumnaViewModel filtro, params int[] ids)
    {
        foreach (var opcion in filtro.Grupos.SelectMany(g => g.Opciones))
        {
            opcion.Seleccionada = ids.Contains(opcion.Id);
        }

        filtro.AplicarCommand.Execute(null);
    }

    [Fact]
    public async Task TextoBusqueda_Cambia_ConsultaConElTerminoDesdeLaPrimeraPagina()
    {
        await _sut.InicializarAsync();
        _sut.PaginaActual = 2;

        _sut.TextoBusqueda = "escuela";

        await EsperarAsync(() => LlamadasAlListado == 2);
        _sut.PaginaActual.Should().Be(1);
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.TerminoBusqueda == "escuela" && c.Pagina == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TextoBusqueda_VariasTeclasSeguidas_ConsultaUnaSolaVezConElTextoCompleto()
    {
        await _sut.InicializarAsync();

        _sut.TextoBusqueda = "e";
        _sut.TextoBusqueda = "es";
        _sut.TextoBusqueda = "esc";

        await EsperarAsync(() => LlamadasAlListado >= 2);
        await Task.Delay(400);
        LlamadasAlListado.Should().Be(2, "el debounce descarta las consultas intermedias");
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.TerminoBusqueda == "esc"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LimpiarFiltros_ConTodosLosFiltrosActivos_VuelveAlListadoCompletoConUnaSolaConsulta()
    {
        PrepararClientes();
        await _sut.InicializarAsync();
        AplicarOpciones(_sut.FiltroEstado, (int)FiltroEstadoPresupuestoEnum.Vencidos);
        AplicarOpciones(_sut.FiltroCliente, 7);
        _sut.FiltroEmision.BorradorDesde = new DateTime(2026, 11, 1);
        _sut.FiltroEmision.AplicarCommand.Execute(null);
        _sut.FiltroVencimiento.BorradorHasta = new DateTime(2026, 11, 30);
        _sut.FiltroVencimiento.AplicarCommand.Execute(null);

        // Una consulta nueva cancela la anterior: se espera a que las cuatro de arriba se asienten antes de contar
        await Task.Delay(300);
        var antesDeLimpiar = LlamadasAlListado;
        _sut.LimpiarFiltrosCommand.CanExecute(null).Should().BeTrue();

        _sut.LimpiarFiltrosCommand.Execute(null);

        await EsperarAsync(() => LlamadasAlListado == antesDeLimpiar + 1);
        await Task.Delay(100);
        LlamadasAlListado.Should().Be(antesDeLimpiar + 1);
        _sut.FiltroEstado.HayFiltroActivo.Should().BeFalse();
        _sut.FiltroCliente.HayFiltroActivo.Should().BeFalse();
        _sut.FiltroEmision.HayFiltroActivo.Should().BeFalse();
        _sut.FiltroVencimiento.HayFiltroActivo.Should().BeFalse();
        _sut.TextoBusqueda.Should().BeEmpty();
        _sut.LimpiarFiltrosCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task PaginaSiguiente_ConMasPaginas_ConsultaLaPaginaSiguiente()
    {
        _presupuestoService.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>()).Returns(new PresupuestosPaginadosDto
        {
            Items = [Resumen(IdPresupuesto, EstadoPresupuestoEnum.Pendiente, true)],
            TotalRegistros = 40,
            PaginaActual = 1,
            TamanoPagina = 15
        });
        await _sut.InicializarAsync();
        _sut.TotalPaginas.Should().Be(3);
        _sut.PuedeAvanzarPagina.Should().BeTrue();
        _sut.PuedeRetrocederPagina.Should().BeFalse();

        await _sut.PaginaSiguienteCommand.ExecuteAsync(null);

        _sut.PaginaActual.Should().Be(2);
        await _presupuestoService.Received(1).ListarAsync(Arg.Is<ConsultaPresupuestosDto>(c => c.Pagina == 2), Arg.Any<CancellationToken>());

        await _sut.UltimaPaginaCommand.ExecuteAsync(null);
        _sut.PaginaActual.Should().Be(3);
        _sut.PuedeAvanzarPagina.Should().BeFalse();

        await _sut.PaginaAnteriorCommand.ExecuteAsync(null);
        _sut.PaginaActual.Should().Be(2);

        await _sut.PrimeraPaginaCommand.ExecuteAsync(null);
        _sut.PaginaActual.Should().Be(1);
    }

    [Fact]
    public async Task IrAPagina_FueraDeRango_NoConsulta()
    {
        await _sut.InicializarAsync();

        await _sut.IrAPaginaCommand.ExecuteAsync(9);
        await _sut.PaginaAnteriorCommand.ExecuteAsync(null);

        LlamadasAlListado.Should().Be(1);
        _sut.PaginaActual.Should().Be(1);
    }

    [Fact]
    public async Task CargarPresupuestos_ElServicioFalla_MuestraElErrorYNoElEstadoVacio()
    {
        _presupuestoService.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("sin conexión"));

        await _sut.InicializarAsync();

        _sut.TieneMensajeError.Should().BeTrue();
        _sut.EstaVacio.Should().BeFalse("el error se informa aparte: la grilla no está vacía por falta de presupuestos");
        _sut.IsBusy.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Estado vacío                                                        //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task EstaVacio_SinPresupuestosYSinFiltros_OrientaHaciaElPuntoDeVenta()
    {
        PrepararListado();

        _sut.EstaVacio.Should().BeFalse("antes de la primera respuesta no se sabe si hay presupuestos");
        await _sut.InicializarAsync();

        _sut.EstaVacio.Should().BeTrue();
        _sut.TextoVacio.Should().Contain("Punto de venta");
    }

    [Fact]
    public async Task TextoVacio_ConUnFiltroActivo_DiceQueElFiltroNoEncontroNada()
    {
        PrepararListado();
        await _sut.InicializarAsync();

        AplicarOpciones(_sut.FiltroEstado, (int)FiltroEstadoPresupuestoEnum.Anulados);
        await EsperarAsync(() => LlamadasAlListado == 2);

        _sut.EstaVacio.Should().BeTrue();
        _sut.TextoVacio.Should().Contain("coincide");
    }

    // ------------------------------------------------------------------ //
    // Anular                                                              //
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(EstadoPresupuestoEnum.Pendiente, true, true)]
    [InlineData(EstadoPresupuestoEnum.Pendiente, false, true)]
    [InlineData(EstadoPresupuestoEnum.Convertido, true, false)]
    [InlineData(EstadoPresupuestoEnum.Anulado, true, false)]
    public async Task AnularCommand_SegunElEstado_SoloSePuedeEjecutarSiEstaPendiente(EstadoPresupuestoEnum estado, bool vigente, bool puede)
    {
        PrepararListado(Resumen(IdPresupuesto, estado, vigente));
        await _sut.InicializarAsync();
        var fila = _sut.Presupuestos.Single();

        _sut.AnularCommand.CanExecute(fila).Should().Be(puede);
        fila.PuedeAnular.Should().Be(puede);
    }

    [Fact]
    public async Task Anular_PresupuestoConvertido_NoPideConfirmacionNiLlamaAlServicio()
    {
        PrepararListado(Resumen(IdPresupuesto, EstadoPresupuestoEnum.Convertido, true));
        await _sut.InicializarAsync();

        await _sut.AnularCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.DidNotReceiveWithAnyArgs().Confirmar(default!, default!);
        await _presupuestoService.DidNotReceiveWithAnyArgs().AnularAsync(default, default);
    }

    [Fact]
    public async Task Anular_ConfirmaElUsuario_AnulaYRecargaLaPaginaActual()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _sut.AnularCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.Received(1).Confirmar(
            "Anular presupuesto",
            Arg.Is<string>(m => m.Contains("N.º 00000123") && m.Contains("No se podrá recuperar en el POS")));
        await _presupuestoService.Received(1).AnularAsync(IdPresupuesto, Arg.Any<CancellationToken>());
        LlamadasAlListado.Should().Be(2, "tras anular se vuelve a consultar la página para ver el estado real");
    }

    [Fact]
    public async Task Anular_CancelaElUsuario_NoLlamaAlServicioNiRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.AnularCommand.ExecuteAsync(_sut.Presupuestos.Single());

        await _presupuestoService.DidNotReceiveWithAnyArgs().AnularAsync(default, default);
        LlamadasAlListado.Should().Be(1);
    }

    [Fact]
    public async Task Anular_OtraTerminalYaLoCambio_InformaYRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _presupuestoService.AnularAsync(IdPresupuesto, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TransicionPresupuestoInvalidaException(EstadoPresupuestoEnum.Convertido, "anular"));

        await _sut.AnularCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.Received(1).MostrarAlerta("El presupuesto cambió de estado", Arg.Is<string>(m => m.Contains("Convertido")));
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task Anular_ConflictoDeConcurrencia_InformaYRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _presupuestoService.AnularAsync(IdPresupuesto, Arg.Any<CancellationToken>()).ThrowsAsync(new ConflictoDeConcurrenciaException());

        await _sut.AnularCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.Received(1).MostrarAlerta("Presupuesto modificado en otra terminal", Arg.Any<string>());
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task Anular_ErrorInesperado_MuestraUnErrorGenericoYRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _presupuestoService.AnularAsync(IdPresupuesto, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        await _sut.AnularCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.Received(1).MostrarError("Error inesperado", Arg.Is<string>(m => !m.Contains("boom")));
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task Anular_SeAnuloElUltimoDeLaUltimaPagina_RetrocedeALaUltimaPaginaConDatos()
    {
        var fila = Resumen(IdPresupuesto, EstadoPresupuestoEnum.Pendiente, true);
        var llamada = 0;
        _presupuestoService.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            llamada++;

            // 1.ª: página 2 con una fila (16 registros). 2.ª (tras anular): 15 registros, la página 2 ya no existe.
            return llamada switch
            {
                1 => new PresupuestosPaginadosDto { Items = [fila], TotalRegistros = 16, PaginaActual = 2, TamanoPagina = 15 },
                2 => new PresupuestosPaginadosDto { Items = [], TotalRegistros = 15, PaginaActual = 2, TamanoPagina = 15 },
                _ => new PresupuestosPaginadosDto { Items = [fila], TotalRegistros = 15, PaginaActual = 1, TamanoPagina = 15 }
            };
        });
        await _sut.InicializarAsync();
        _sut.PaginaActual = 2;
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _sut.AnularCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _sut.PaginaActual.Should().Be(1);
        _sut.Presupuestos.Should().HaveCount(1);
        LlamadasAlListado.Should().Be(3);
    }

    // ------------------------------------------------------------------ //
    // Nuevo presupuesto y Corregir (módulo 5.4)                           //
    // ------------------------------------------------------------------ //

    private static PresupuestoParaVentaDto ParaCorregir()
    {
        return new PresupuestoParaVentaDto
        {
            IdPresupuesto = IdPresupuesto,
            EstaVigente = true,
            FechaVencimiento = new DateOnly(2026, 11, 29),
            Descuento = 0m,
            Renglones = [],
            Discrepancias = [],
            Faltantes = [],
            ArticulosNoDisponibles = []
        };
    }

    [Fact]
    public async Task FiltroEstado_TieneLaOpcionReemplazados_YLaEnviaALaConsulta()
    {
        await _sut.InicializarAsync();

        _sut.FiltroEstado.Grupos.SelectMany(g => g.Opciones).Should().Contain(o => o.Nombre == "Reemplazados");

        AplicarOpciones(_sut.FiltroEstado, (int)FiltroEstadoPresupuestoEnum.Reemplazados);
        await EsperarAsync(() => LlamadasAlListado == 2);

        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.Estados.SequenceEqual(new[] { FiltroEstadoPresupuestoEnum.Reemplazados })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NuevoPresupuesto_SeGuardo_AbreElEditorVacioYRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirEditor(null, Arg.Any<CargaSerializada>()).Returns(true);

        await _sut.NuevoPresupuestoCommand.ExecuteAsync(null);

        _dialogService.Received(1).AbrirEditor(null, Arg.Any<CargaSerializada>());
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task NuevoPresupuesto_SeCancelo_NoRecarga()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirEditor(null, Arg.Any<CargaSerializada>()).Returns(false);

        await _sut.NuevoPresupuestoCommand.ExecuteAsync(null);

        LlamadasAlListado.Should().Be(1);
    }

    [Theory]
    [InlineData(EstadoPresupuestoEnum.Pendiente, true, true)]
    [InlineData(EstadoPresupuestoEnum.Pendiente, false, true)]
    [InlineData(EstadoPresupuestoEnum.Convertido, true, false)]
    [InlineData(EstadoPresupuestoEnum.Anulado, true, false)]
    [InlineData(EstadoPresupuestoEnum.Reemplazado, true, false)]
    public async Task CorregirCommand_SegunElEstado_SoloSePuedeEjecutarSiEstaPendiente(EstadoPresupuestoEnum estado, bool vigente, bool puede)
    {
        PrepararListado(Resumen(IdPresupuesto, estado, vigente));
        await _sut.InicializarAsync();
        var fila = _sut.Presupuestos.Single();

        _sut.CorregirCommand.CanExecute(fila).Should().Be(puede);
        fila.PuedeCorregir.Should().Be(puede);
    }

    [Fact]
    public async Task Corregir_PresupuestoPendiente_LeeConPrecioDeHoyAbreElEditorPrecargadoYRecarga()
    {
        await _sut.InicializarAsync();
        var original = ParaCorregir();
        _presupuestoService.RecuperarParaVentaAsync(IdPresupuesto, Arg.Any<CancellationToken>()).Returns(original);
        _dialogService.AbrirEditor(original, Arg.Any<CargaSerializada>()).Returns(true);

        await _sut.CorregirCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.Received(1).AbrirEditor(original, Arg.Any<CargaSerializada>());
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task Corregir_OtraTerminalLoCobro_InformaYRecargaSinAbrirElEditor()
    {
        await _sut.InicializarAsync();
        _presupuestoService.RecuperarParaVentaAsync(IdPresupuesto, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TransicionPresupuestoInvalidaException(EstadoPresupuestoEnum.Convertido, "recuperar para la venta"));

        await _sut.CorregirCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.Received(1).MostrarAlerta("El presupuesto cambió de estado", Arg.Any<string>());
        _dialogService.DidNotReceiveWithAnyArgs().AbrirEditor(default, default!);
        LlamadasAlListado.Should().Be(2);
    }

    [Fact]
    public async Task Fila_ReemplazadoPorOtro_MuestraElTextoDeLaCadena()
    {
        var resumen = Resumen(IdPresupuesto, EstadoPresupuestoEnum.Reemplazado, true) with { IdPresupuestoReemplazante = 45 };
        PrepararListado(resumen);
        await _sut.InicializarAsync();

        var fila = _sut.Presupuestos.Single();

        fila.EstadoVisual.Should().Be(EstadoVisualPresupuesto.Reemplazado);
        fila.EstadoTexto.Should().Be("Reemplazado");
        fila.TextoReemplazo.Should().Be("Reemplazado por N.º 00000045");
    }

    [Fact]
    public async Task Fila_SinCorreccion_NoTieneTextoDeReemplazo()
    {
        await _sut.InicializarAsync();

        _sut.Presupuestos.Single().TextoReemplazo.Should().BeNull();
    }

    [Fact]
    public async Task VerDetalle_SeCambioElTitulo_Recarga()
    {
        await _sut.InicializarAsync();
        _dialogService.MostrarDetalle(IdPresupuesto).Returns(true);

        await _sut.VerDetalleCommand.ExecuteAsync(_sut.Presupuestos.Single());

        LlamadasAlListado.Should().Be(2);
    }

    // ------------------------------------------------------------------ //
    // Ver detalle e imprimir                                              //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task VerDetalle_ConUnaFila_AbreElDetalleDeEsePresupuesto()
    {
        await _sut.InicializarAsync();

        _sut.VerDetalleCommand.Execute(_sut.Presupuestos.Single());

        _dialogService.Received(1).MostrarDetalle(IdPresupuesto);
    }

    [Fact]
    public async Task Imprimir_ConUnaFila_ImprimeYAvisa()
    {
        await _sut.InicializarAsync();

        await _sut.ImprimirCommand.ExecuteAsync(_sut.Presupuestos.Single());

        await _presupuestoService.Received(1).ImprimirAsync(IdPresupuesto, Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Presupuesto impreso", Arg.Is<string>(m => m.Contains("00000123")));
    }

    [Fact]
    public async Task Imprimir_LaImpresoraFalla_MuestraElError()
    {
        await _sut.InicializarAsync();
        _presupuestoService.ImprimirAsync(IdPresupuesto, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("sin papel"));

        await _sut.ImprimirCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.Received(1).MostrarError("Error inesperado", Arg.Any<string>());
        _dialogService.DidNotReceiveWithAnyArgs().MostrarInformacion(default!, default!);
    }

    // ------------------------------------------------------------------ //
    // Exportar a PDF                                                      //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task ExportarPdf_ElUsuarioElige_ProponeElNombreConElNumeroYEscribeElArchivo()
    {
        await _sut.InicializarAsync();
        var ruta = NuevaRutaTemporal();
        _dialogService.ElegirRutaPdf("Presupuesto-00000123.pdf").Returns(ruta);
        _presupuestoService
            .When(s => s.ExportarPdfAsync(IdPresupuesto, Arg.Any<Stream>(), Arg.Any<CancellationToken>()))
            .Do(ci => ci.Arg<Stream>().Write("%PDF-1.7"u8));

        await _sut.ExportarPdfCommand.ExecuteAsync(_sut.Presupuestos.Single());

        File.ReadAllText(ruta).Should().Be("%PDF-1.7");
        _dialogService.Received(1).MostrarInformacion("PDF guardado", Arg.Is<string>(m => m.Contains(ruta)));
    }

    [Fact]
    public async Task ExportarPdf_ElUsuarioCancelaLaRuta_NoLlamaAlServicio()
    {
        await _sut.InicializarAsync();
        _dialogService.ElegirRutaPdf(Arg.Any<string>()).Returns((string?)null);

        await _sut.ExportarPdfCommand.ExecuteAsync(_sut.Presupuestos.Single());

        await _presupuestoService.DidNotReceiveWithAnyArgs().ExportarPdfAsync(default, default!, default);
    }

    [Fact]
    public async Task ExportarPdf_ElArchivoEstaBloqueado_InformaYReintentaSinPerderLaRuta()
    {
        await _sut.InicializarAsync();
        var ruta = NuevaRutaTemporal();
        _dialogService.ElegirRutaPdf(Arg.Any<string>()).Returns(ruta);
        _presupuestoService
            .When(s => s.ExportarPdfAsync(IdPresupuesto, Arg.Any<Stream>(), Arg.Any<CancellationToken>()))
            .Do(ci => ci.Arg<Stream>().Write("%PDF-1.7"u8));

        // Otro proceso (por ejemplo un visor de PDF) tiene el archivo abierto sin compartirlo
        var bloqueo = new FileStream(ruta, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        _dialogService.Confirmar("No se pudo guardar el PDF", Arg.Any<string>()).Returns(_ =>
        {
            // El usuario cierra el visor y acepta reintentar
            bloqueo.Dispose();
            return true;
        });

        await _sut.ExportarPdfCommand.ExecuteAsync(_sut.Presupuestos.Single());

        _dialogService.Received(1).Confirmar("No se pudo guardar el PDF", Arg.Is<string>(m => m.Contains("Reintentar")));
        _dialogService.Received(1).ElegirRutaPdf(Arg.Any<string>());
        await _presupuestoService.Received(1).ExportarPdfAsync(IdPresupuesto, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        File.ReadAllText(ruta).Should().Be("%PDF-1.7");
        _dialogService.Received(1).MostrarInformacion("PDF guardado", Arg.Any<string>());
    }

    [Fact]
    public async Task ExportarPdf_ElArchivoEstaBloqueadoYNoReintenta_NoEscribeNada()
    {
        await _sut.InicializarAsync();
        var ruta = NuevaRutaTemporal();
        _dialogService.ElegirRutaPdf(Arg.Any<string>()).Returns(ruta);
        _dialogService.Confirmar("No se pudo guardar el PDF", Arg.Any<string>()).Returns(false);

        using (new FileStream(ruta, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            await _sut.ExportarPdfCommand.ExecuteAsync(_sut.Presupuestos.Single());
        }

        await _presupuestoService.DidNotReceiveWithAnyArgs().ExportarPdfAsync(default, default!, default);
        _dialogService.DidNotReceiveWithAnyArgs().MostrarInformacion(default!, default!);
    }

    [Fact]
    public async Task ExportarPdf_FallaLaGeneracion_BorraElArchivoAMediasYMuestraElError()
    {
        await _sut.InicializarAsync();
        var ruta = NuevaRutaTemporal();
        _dialogService.ElegirRutaPdf(Arg.Any<string>()).Returns(ruta);
        _presupuestoService
            .When(s => s.ExportarPdfAsync(IdPresupuesto, Arg.Any<Stream>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("QuestPDF falló"));

        await _sut.ExportarPdfCommand.ExecuteAsync(_sut.Presupuestos.Single());

        File.Exists(ruta).Should().BeFalse("un PDF a medias no se deja en disco");
        _dialogService.Received(1).MostrarError("Error inesperado", Arg.Any<string>());
    }

    // ------------------------------------------------------------------ //
    // Presentación de la fila                                             //
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(EstadoPresupuestoEnum.Pendiente, true, EstadoVisualPresupuesto.Vigente, "Vigente")]
    [InlineData(EstadoPresupuestoEnum.Pendiente, false, EstadoVisualPresupuesto.Vencido, "Vencido")]
    [InlineData(EstadoPresupuestoEnum.Convertido, true, EstadoVisualPresupuesto.Convertido, "Convertido")]
    [InlineData(EstadoPresupuestoEnum.Convertido, false, EstadoVisualPresupuesto.Convertido, "Convertido")]
    [InlineData(EstadoPresupuestoEnum.Anulado, true, EstadoVisualPresupuesto.Anulado, "Anulado")]
    [InlineData(EstadoPresupuestoEnum.Anulado, false, EstadoVisualPresupuesto.Anulado, "Anulado")]
    public void PresupuestoFila_EstadoYVigencia_SeTraduceAlTextoDelBadge(
        EstadoPresupuestoEnum estado,
        bool vigente,
        EstadoVisualPresupuesto visualEsperado,
        string textoEsperado)
    {
        var fila = new PresupuestoFila(Resumen(IdPresupuesto, estado, vigente), TimeProvider.System);

        fila.EstadoVisual.Should().Be(visualEsperado);
        fila.EstadoTexto.Should().Be(textoEsperado);
    }

    [Fact]
    public void PresupuestoFila_Datos_SeMuestranComoPideLaGrilla()
    {
        var reloj = new RelojDePrueba(new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero));

        var sinCliente = new PresupuestoFila(Resumen(7, EstadoPresupuestoEnum.Pendiente, true), reloj);
        var conTitulo = new PresupuestoFila(Resumen(8, EstadoPresupuestoEnum.Pendiente, true, titulo: "Lista escolar", cliente: "Escuela 12"), reloj);

        sinCliente.Numero.Should().Be("00000007");
        sinCliente.ClienteTexto.Should().Be("Consumidor Final");
        sinCliente.Titulo.Should().BeEmpty();
        sinCliente.TituloTooltip.Should().BeNull();
        sinCliente.EmisionLocal.Should().Be(new DateTime(2026, 11, 14, 10, 20, 0), "la emisión se guarda en UTC y se muestra en hora local (UTC-3)");
        sinCliente.Vencimiento.Should().Be(new DateTime(2026, 11, 29));
        conTitulo.Titulo.Should().Be("Lista escolar");
        conTitulo.TituloTooltip.Should().Be("Lista escolar");
        conTitulo.ClienteTexto.Should().Be("Escuela 12");
    }
}
