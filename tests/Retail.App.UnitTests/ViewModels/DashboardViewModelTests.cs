using System.IO;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using LiveChartsCore.SkiaSharpView;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Reportes;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class DashboardViewModelTests : IDisposable
{
    private const int IdActor = 7;
    private const string Ruta = @"C:\Reportes\reporte.xlsx";

    // Hoy en el comercio: 15/10/2026 (15:00 UTC = 12:00 en Argentina, UTC-3).
    private static readonly DateOnly Hoy = new(2026, 10, 15);

    private readonly IReporteService _reporteService = Substitute.For<IReporteService>();
    private readonly IExportacionReportesService _exportacionService = Substitute.For<IExportacionReportesService>();
    private readonly IInventarioService _inventarioService = Substitute.For<IInventarioService>();
    private readonly IUsuarioService _usuarioService = Substitute.For<IUsuarioService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly IReporteDialogService _dialogService = Substitute.For<IReporteDialogService>();
    private readonly DashboardViewModel _sut;

    public DashboardViewModelTests()
    {
        _session.IdUsuario.Returns(IdActor);
        _session.EsGerente.Returns(true);

        _inventarioService.ListarCategoriasAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CategoriaDto> { new() { IdCategoria = 1, NombreCategoria = "Literatura" } });
        _inventarioService.ListarMarcasAsync(Arg.Any<CancellationToken>())
            .Returns(new List<MarcaDto> { new() { IdMarca = 2, NombreMarca = "Planeta" } });
        _usuarioService.ListarUsuariosAsync(IdActor, Arg.Any<CancellationToken>())
            .Returns(new List<UsuarioDto>
            {
                Usuario(3, "Ana", "Pérez", RolUsuarioEnum.Cajero),
                Usuario(4, "Admin", "Soporte", RolUsuarioEnum.Administrador)
            });

        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new DateOnly(2020, 1, 1));
        _reporteService.ObtenerTableroAsync(IdActor, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>())
            .Returns(Tablero());

        _dialogService.ElegirArchivoDestino(Arg.Any<string>(), Arg.Any<FormatoExportacionEnum>()).Returns(Ruta);
        _dialogService.AbrirArchivoDestino(Ruta).Returns(_ => new MemoryStream());

        _sut = new DashboardViewModel(
            _reporteService,
            _exportacionService,
            _inventarioService,
            _usuarioService,
            _session,
            _dialogService,
            new RelojFijo(new DateTimeOffset(2026, 10, 15, 15, 0, 0, TimeSpan.Zero)));
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ Entrada y consulta

    [Fact]
    public async Task CargarAsync_AlEntrar_ConsultaElTableroConElMesEnCursoYCargaLosCombos()
    {
        // Act
        await _sut.CargarAsync();

        // Assert
        await _reporteService.Received(1).ObtenerTableroAsync(
            IdActor,
            Arg.Is<FiltroPeriodoDto>(f => f.Desde == new DateOnly(2026, 10, 1) && f.Hasta == Hoy),
            Arg.Any<CancellationToken>());
        _sut.ResultadoActual.Should().BeOfType<TableroDto>();
        _sut.MostrarTablero.Should().BeTrue();
        _sut.EstaCargando.Should().BeFalse();

        _sut.Categorias.Select(c => c.Nombre).Should().Equal("Todas", "Literatura");
        _sut.Marcas.Select(m => m.Nombre).Should().Equal("Todas", "Planeta");
        _sut.Cajeros.Select(c => c.Nombre).Should().Equal("Todos", "Ana Pérez");
        _sut.CategoriaSeleccionada!.Id.Should().BeNull();
    }

    [Fact]
    public async Task CargarAsync_ConElTableroYaConsultado_NoVuelveAConsultar()
    {
        // Arrange
        await _sut.CargarAsync();

        // Act
        await _sut.CargarAsync();

        // Assert
        await _reporteService.Received(1).ObtenerTableroAsync(IdActor, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsultarAsync_Tablero_UsaElPeriodoElegido()
    {
        // Arrange
        await _sut.CargarAsync();
        _sut.Desde = new DateTime(2026, 9, 1);
        _sut.Hasta = new DateTime(2026, 9, 30);

        // Act
        await _sut.ConsultarAsync();

        // Assert
        await _reporteService.Received(1).ObtenerTableroAsync(
            IdActor,
            Arg.Is<FiltroPeriodoDto>(f => f.Desde == new DateOnly(2026, 9, 1) && f.Hasta == new DateOnly(2026, 9, 30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsultarAsync_VentasPorPeriodo_LlamaAlServicioConAgrupacionYCajero()
    {
        // Arrange
        _reporteService.ObtenerVentasPorPeriodoAsync(IdActor, Arg.Any<FiltroVentasPorPeriodoDto>(), Arg.Any<CancellationToken>())
            .Returns(VentasPorPeriodo(conPuntos: true));
        await _sut.CargarAsync();
        _sut.ReporteSeleccionado = TipoReporteEnum.VentasPorPeriodo;
        await _sut.EsperarConsultaAutomaticaAsync();
        _sut.Agrupacion = AgrupacionTemporalEnum.Mes;
        _sut.CajeroSeleccionado = _sut.Cajeros[1];

        // Act
        await _sut.ConsultarAsync();

        // Assert
        await _reporteService.Received(1).ObtenerVentasPorPeriodoAsync(
            IdActor,
            Arg.Is<FiltroVentasPorPeriodoDto>(f => f.Agrupacion == AgrupacionTemporalEnum.Mes && f.IdUsuario == 3
                && f.Desde == new DateOnly(2026, 10, 1) && f.Hasta == Hoy),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsultarAsync_Ranking_LlamaAlServicioConLosFiltros()
    {
        // Arrange
        _reporteService.ObtenerRankingAsync(IdActor, Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>())
            .Returns(Ranking(cantidad: 2));
        await _sut.CargarAsync();
        _sut.ReporteSeleccionado = TipoReporteEnum.Ranking;
        await _sut.EsperarConsultaAutomaticaAsync();
        _sut.CategoriaSeleccionada = _sut.Categorias[1];
        _sut.MarcaSeleccionada = _sut.Marcas[1];
        _sut.Criterio = CriterioRankingEnum.Importe;
        _sut.Cantidad = 5;

        // Act
        await _sut.ConsultarAsync();

        // Assert
        await _reporteService.Received(1).ObtenerRankingAsync(
            IdActor,
            Arg.Is<FiltroRankingDto>(f => f.IdCategoria == 1 && f.IdMarca == 2 && f.Criterio == CriterioRankingEnum.Importe && f.Cantidad == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsultarAsync_Rentabilidad_LlamaAlServicioConLaAgrupacion()
    {
        // Arrange
        _reporteService.ObtenerRentabilidadAsync(IdActor, Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(Rentabilidad(filas: 2));
        await _sut.CargarAsync();
        _sut.ReporteSeleccionado = TipoReporteEnum.Rentabilidad;
        await _sut.EsperarConsultaAutomaticaAsync();
        _sut.AgrupacionRentabilidad = AgrupacionRentabilidadEnum.Marca;

        // Act
        await _sut.ConsultarAsync();

        // Assert
        await _reporteService.Received(1).ObtenerRentabilidadAsync(
            IdActor,
            Arg.Is<FiltroRentabilidadDto>(f => f.AgruparPor == AgrupacionRentabilidadEnum.Marca),
            Arg.Any<CancellationToken>());
        _sut.TotalRentabilidad.Should().NotBeNull();
    }

    [Fact]
    public async Task ConsultarAsync_StockCriticoYSinMovimiento_NoUsanFechasYUsanSusFiltros()
    {
        // Arrange
        _reporteService.ObtenerStockCriticoAsync(IdActor, Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>())
            .Returns(new StockCriticoDto { Filtro = new FiltroInventarioDto(), Filas = [new StockCriticoFilaDto { Descripcion = "Lapicera" }] });
        _reporteService.ObtenerSinMovimientoAsync(IdActor, Arg.Any<FiltroSinMovimientoDto>(), Arg.Any<CancellationToken>())
            .Returns(new SinMovimientoDto { Filtro = new FiltroSinMovimientoDto(), Filas = [new ArticuloSinMovimientoDto { Descripcion = "Atlas" }] });
        await _sut.CargarAsync();

        // Act
        _sut.CategoriaSeleccionada = _sut.Categorias[1];
        _sut.ReporteSeleccionado = TipoReporteEnum.StockCritico;
        await _sut.EsperarConsultaAutomaticaAsync();
        _sut.Dias = 30;
        _sut.ReporteSeleccionado = TipoReporteEnum.SinMovimiento;
        await _sut.EsperarConsultaAutomaticaAsync();

        // Assert
        await _reporteService.Received(1).ObtenerStockCriticoAsync(
            IdActor, Arg.Is<FiltroInventarioDto>(f => f.IdCategoria == 1 && f.IdMarca == null), Arg.Any<CancellationToken>());
        await _reporteService.Received(1).ObtenerSinMovimientoAsync(
            IdActor, Arg.Is<FiltroSinMovimientoDto>(f => f.IdCategoria == 1 && f.Dias == 30), Arg.Any<CancellationToken>());
        _sut.MostrarFechas.Should().BeFalse();
        _sut.MostrarDias.Should().BeTrue();
    }

    [Fact]
    public async Task CambiarDeReporte_AUnaPestanaYaConsultada_NoVuelveAConsultar()
    {
        // Arrange
        _reporteService.ObtenerRankingAsync(IdActor, Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>())
            .Returns(Ranking(cantidad: 2));
        await _sut.CargarAsync();

        // Act
        _sut.ReporteSeleccionado = TipoReporteEnum.Ranking;
        await _sut.EsperarConsultaAutomaticaAsync();
        _sut.ReporteSeleccionado = TipoReporteEnum.Tablero;
        await _sut.EsperarConsultaAutomaticaAsync();
        _sut.ReporteSeleccionado = TipoReporteEnum.Ranking;
        await _sut.EsperarConsultaAutomaticaAsync();

        // Assert
        await _reporteService.Received(1).ObtenerRankingAsync(IdActor, Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>());
        await _reporteService.Received(1).ObtenerTableroAsync(IdActor, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>());
        _sut.MasVendidos.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConsultarAsync_SinFechas_MuestraElErrorYNoLlamaAlServicio()
    {
        // Arrange
        await _sut.CargarAsync();
        _reporteService.ClearReceivedCalls();
        _sut.Desde = null;

        // Act
        await _sut.ConsultarAsync();

        // Assert
        _sut.TieneError.Should().BeTrue();
        _reporteService.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ConsultarAsync_ValidationException_MuestraLosErroresSinLanzar()
    {
        // Arrange
        await _sut.CargarAsync();
        _reporteService.ObtenerTableroAsync(IdActor, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(
            [
                new ValidationFailure("Desde", "La fecha desde no puede ser posterior a la fecha hasta."),
                new ValidationFailure("Hasta", "La fecha hasta no puede ser posterior a hoy.")
            ]));

        // Act
        var consultar = () => _sut.ConsultarAsync();

        // Assert
        await consultar.Should().NotThrowAsync();
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("La fecha desde no puede ser posterior").And.Contain("La fecha hasta no puede ser posterior a hoy.");
        _sut.EstaCargando.Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarAsync_OperacionNoAutorizada_MuestraElMensajeDelServicio()
    {
        // Arrange
        _reporteService.ObtenerTableroAsync(IdActor, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperacionNoAutorizadaException("Solo un Gerente puede ver los reportes."));

        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.MensajeError.Should().Be("Solo un Gerente puede ver los reportes.");
    }

    [Fact]
    public async Task ConsultarAsync_ErrorInesperado_MuestraUnMensajeGenericoSinFiltrarElDetalleTecnico()
    {
        // Arrange
        _reporteService.ObtenerTableroAsync(IdActor, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidDataException("Cannot open database 'RetailDb' requested by the login."));

        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.TieneError.Should().BeTrue();
        _sut.MensajeError.Should().NotContain("RetailDb").And.Contain("error inesperado");
    }

    // ------------------------------------------------------------------ Atajos de período

    [Fact]
    public void AtajosDePeriodo_EstablecenLasFechas()
    {
        // Assert: por defecto, el mes en curso hasta hoy
        _sut.Desde.Should().Be(new DateTime(2026, 10, 1));
        _sut.Hasta.Should().Be(new DateTime(2026, 10, 15));

        _sut.MesAnteriorCommand.Execute(null);
        _sut.Desde.Should().Be(new DateTime(2026, 9, 1));
        _sut.Hasta.Should().Be(new DateTime(2026, 9, 30));

        _sut.EsteAnioCommand.Execute(null);
        _sut.Desde.Should().Be(new DateTime(2026, 1, 1));
        _sut.Hasta.Should().Be(new DateTime(2026, 10, 15));

        _sut.EsteMesCommand.Execute(null);
        _sut.Desde.Should().Be(new DateTime(2026, 10, 1));
    }

    [Fact]
    public async Task AtajosDePeriodo_ConPrimeraVentaPosterior_RecortanElDesdeALaFechaMinima()
    {
        // Arrange: la primera venta fue el 10/03/2026
        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new DateOnly(2026, 3, 10));
        await _sut.CargarAsync();

        // Act + Assert: Este año arranca el 01/01 y se recorta; Mes anterior (setiembre) no necesita recorte
        _sut.EsteAnioCommand.Execute(null);
        _sut.Desde.Should().Be(new DateTime(2026, 3, 10));

        _sut.MesAnteriorCommand.Execute(null);
        _sut.Desde.Should().Be(new DateTime(2026, 9, 1));
    }

    [Fact]
    public async Task AtajosDePeriodo_ConPrimeraVentaEnElMesAnterior_RecortanMesAnterior()
    {
        // Arrange: la primera venta fue el 20/09/2026
        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new DateOnly(2026, 9, 20));
        await _sut.CargarAsync();

        // Act
        _sut.MesAnteriorCommand.Execute(null);

        // Assert
        _sut.Desde.Should().Be(new DateTime(2026, 9, 20));
        _sut.Hasta.Should().Be(new DateTime(2026, 9, 30));
    }

    [Fact]
    public async Task MesAnterior_ConPrimeraVentaEnElMesEnCurso_EstaDeshabilitadoConSuTooltip()
    {
        // Arrange: la primera venta fue el 05/10/2026; setiembre no tiene ventas
        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new DateOnly(2026, 10, 5));

        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.MesAnteriorCommand.CanExecute(null).Should().BeFalse();
        _sut.TooltipMesAnterior.Should().Be("No hay ventas registradas en el mes anterior");
    }

    [Fact]
    public async Task MesAnterior_ConPrimeraVentaAnterior_EstaHabilitadoSinTooltip()
    {
        // Arrange
        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new DateOnly(2026, 9, 30));

        // Act
        await _sut.CargarAsync();

        // Assert: el último día de setiembre alcanza
        _sut.MesAnteriorCommand.CanExecute(null).Should().BeTrue();
        _sut.TooltipMesAnterior.Should().BeNull();
    }

    // ------------------------------------------------------------------ Límites del período (D-32) y comparación (D-31)

    [Fact]
    public void FechaMinima_AntesDeCargar_NoLimitaElCalendario()
    {
        _sut.FechaMinima.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public async Task CargarAsync_ConsultaLaFechaMinimaAntesDelTableroYLaExpone()
    {
        // Arrange
        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new DateOnly(2026, 3, 9));

        // Act
        await _sut.CargarAsync();

        // Assert
        Received.InOrder(() =>
        {
            _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>());
            _reporteService.ObtenerTableroAsync(IdActor, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>());
        });
        _sut.FechaMinima.Should().Be(new DateTime(2026, 3, 9));
    }

    [Fact]
    public async Task CargarAsync_SinVentas_LaFechaMinimaEsElDiaUnoDelMesYNoCambiaElDesde()
    {
        // Arrange
        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new DateOnly(2026, 10, 1));

        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.FechaMinima.Should().Be(new DateTime(2026, 10, 1));
        _sut.Desde.Should().Be(new DateTime(2026, 10, 1));
    }

    [Fact]
    public async Task CargarAsync_ConPrimeraVentaDentroDelMesEnCurso_CorreElDesdePorDefectoALaPrimeraVenta()
    {
        // Arrange: la primera venta fue el 05/10/2026; el día 1 del mes no se podría consultar
        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>()).Returns(new DateOnly(2026, 10, 5));

        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.Desde.Should().Be(new DateTime(2026, 10, 5));
        await _reporteService.Received(1).ObtenerTableroAsync(
            IdActor,
            Arg.Is<FiltroPeriodoDto>(f => f.Desde == new DateOnly(2026, 10, 5)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CargarAsync_AlPresentarElTablero_IndicaContraQueFechasSeCompara()
    {
        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.TextoComparacion.Should().Be("Comparado con el período del 01/09/2026 al 15/09/2026");
    }

    [Fact]
    public async Task CargarAsync_SiFallaLaFechaMinima_InformaElErrorYNoAbortaElTablero()
    {
        // Arrange
        _reporteService.ObtenerFechaMinimaAsync(IdActor, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperacionNoAutorizadaException("Solo un Gerente puede consultar los reportes gerenciales."));

        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.FechaMinima.Should().Be(DateTime.MinValue);
        _sut.ResultadoActual.Should().BeOfType<TableroDto>();
    }

    // ------------------------------------------------------------------ Exportación

    [Fact]
    public void Exportar_SinResultado_EstaDeshabilitadoConSuTooltip()
    {
        // Assert
        _sut.ExportarExcelCommand.CanExecute(null).Should().BeFalse();
        _sut.ExportarPdfCommand.CanExecute(null).Should().BeFalse();
        _sut.TooltipExportar.Should().Be("No hay datos para exportar");
    }

    [Fact]
    public async Task Exportar_ConElTablero_EstaHabilitadoSinTooltip()
    {
        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.ExportarExcelCommand.CanExecute(null).Should().BeTrue();
        _sut.ExportarPdfCommand.CanExecute(null).Should().BeTrue();
        _sut.TooltipExportar.Should().BeNull();
    }

    [Fact]
    public async Task Exportar_ConUnReporteVacio_EstaDeshabilitadoYMuestraElEstadoVacio()
    {
        // Arrange
        _reporteService.ObtenerVentasPorPeriodoAsync(IdActor, Arg.Any<FiltroVentasPorPeriodoDto>(), Arg.Any<CancellationToken>())
            .Returns(VentasPorPeriodo(conPuntos: false));
        await _sut.CargarAsync();

        // Act
        _sut.ReporteSeleccionado = TipoReporteEnum.VentasPorPeriodo;
        await _sut.EsperarConsultaAutomaticaAsync();

        // Assert
        _sut.EstaVacio.Should().BeTrue();
        _sut.MensajeVacio.Should().Be("No hay datos para el período elegido");
        _sut.ExportarExcelCommand.CanExecute(null).Should().BeFalse();
        _sut.TooltipExportar.Should().Be("No hay datos para exportar");
    }

    [Fact]
    public async Task Exportar_Excel_EscribeElResultadoVisibleEnElArchivoElegido()
    {
        // Arrange
        await _sut.CargarAsync();
        var resultado = _sut.ResultadoActual!;

        // Act
        await _sut.ExportarExcelAsync();

        // Assert
        _dialogService.Received(1).ElegirArchivoDestino("Reporte-Tablero-20261001-20261015", FormatoExportacionEnum.Excel);
        await _exportacionService.Received(1).ExportarAsync(
            IdActor,
            Arg.Is<ResultadoReporteDto>(r => ReferenceEquals(r, resultado)),
            FormatoExportacionEnum.Excel,
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarExito($"Reporte guardado en {Ruta}");
        _sut.EstaExportando.Should().BeFalse();
    }

    [Fact]
    public async Task Exportar_StockCritico_NombraElArchivoConLaFechaDeHoy()
    {
        // Arrange
        _reporteService.ObtenerStockCriticoAsync(IdActor, Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>())
            .Returns(new StockCriticoDto { Filtro = new FiltroInventarioDto(), Filas = [new StockCriticoFilaDto { Descripcion = "Lapicera" }] });
        await _sut.CargarAsync();
        _sut.ReporteSeleccionado = TipoReporteEnum.StockCritico;
        await _sut.EsperarConsultaAutomaticaAsync();

        // Act
        await _sut.ExportarPdfAsync();

        // Assert
        _dialogService.Received(1).ElegirArchivoDestino("Reporte-StockCritico-20261015", FormatoExportacionEnum.Pdf);
    }

    [Fact]
    public async Task Exportar_ConElDialogoCancelado_NoExportaNiAbreArchivos()
    {
        // Arrange
        await _sut.CargarAsync();
        _dialogService.ElegirArchivoDestino(Arg.Any<string>(), Arg.Any<FormatoExportacionEnum>()).Returns((string?)null);

        // Act
        await _sut.ExportarPdfAsync();

        // Assert
        _dialogService.DidNotReceive().AbrirArchivoDestino(Arg.Any<string>());
        _exportacionService.ReceivedCalls().Should().BeEmpty();
        _dialogService.DidNotReceive().MostrarExito(Arg.Any<string>());
    }

    [Fact]
    public async Task Exportar_ConUnErrorDeEscrituraYReintento_ReintentaConElMismoResultadoSinConsultarDeNuevo()
    {
        // Arrange
        await _sut.CargarAsync();
        var resultado = _sut.ResultadoActual!;
        _reporteService.ClearReceivedCalls();

        // El archivo está abierto en Excel la primera vez y libre la segunda.
        _dialogService.AbrirArchivoDestino(Ruta).Returns(
            _ => throw new IOException("El proceso no puede obtener acceso al archivo."),
            _ => new MemoryStream());
        _dialogService.ConfirmarReintento(Arg.Any<string>()).Returns(true);

        // Act
        await _sut.ExportarExcelAsync();

        // Assert
        _dialogService.Received(1).ConfirmarReintento(
            Arg.Is<string>(m => m.Contains("No se pudo guardar el archivo: El proceso no puede obtener acceso al archivo.")
                && m.Contains("¿Reintentar?")));
        await _exportacionService.Received(1).ExportarAsync(
            IdActor,
            Arg.Is<ResultadoReporteDto>(r => ReferenceEquals(r, resultado)),
            FormatoExportacionEnum.Excel,
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
        _reporteService.ReceivedCalls().Should().BeEmpty("RF-26: reintentar no vuelve a consultar la base");
        _dialogService.Received(1).MostrarExito($"Reporte guardado en {Ruta}");
    }

    [Fact]
    public async Task Exportar_SiFallaAlGenerarDespuesDeAbrirElArchivo_BorraElParcialAntesDeOfrecerElReintento()
    {
        // Arrange
        await _sut.CargarAsync();
        _exportacionService.ExportarAsync(
                IdActor, Arg.Any<ResultadoReporteDto>(), Arg.Any<FormatoExportacionEnum>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Disco lleno.")), Task.CompletedTask);
        _dialogService.ConfirmarReintento(Arg.Any<string>()).Returns(true);

        // Act
        await _sut.ExportarExcelAsync();

        // Assert: primero se borra el archivo a medias y recién después se pregunta
        Received.InOrder(() =>
        {
            _dialogService.EliminarArchivoParcial(Ruta);
            _dialogService.ConfirmarReintento(Arg.Any<string>());
        });
        _dialogService.Received(1).EliminarArchivoParcial(Ruta);
        _dialogService.Received(1).MostrarExito($"Reporte guardado en {Ruta}");
    }

    [Fact]
    public async Task Exportar_SiFallaAlAbrirElArchivo_NoBorraNada()
    {
        // Arrange: el archivo está abierto en Excel, así que ni siquiera se pudo abrir
        await _sut.CargarAsync();
        _dialogService.AbrirArchivoDestino(Ruta).Returns(_ => throw new IOException("El archivo está en uso."));
        _dialogService.ConfirmarReintento(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ExportarExcelAsync();

        // Assert
        _dialogService.DidNotReceive().EliminarArchivoParcial(Arg.Any<string>());
        _dialogService.Received(1).ConfirmarReintento(Arg.Any<string>());
    }

    [Fact]
    public async Task Exportar_SiElServicioFallaDespuesDeAbrirElArchivo_BorraElParcialYMuestraElError()
    {
        // Arrange
        await _sut.CargarAsync();
        _exportacionService.ExportarAsync(
                IdActor, Arg.Any<ResultadoReporteDto>(), Arg.Any<FormatoExportacionEnum>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperacionNoAutorizadaException("Solo un Gerente puede exportar reportes."));

        // Act
        await _sut.ExportarPdfAsync();

        // Assert
        _dialogService.Received(1).EliminarArchivoParcial(Ruta);
    }

    [Fact]
    public async Task Exportar_ConUnErrorDeEscrituraYSinReintento_NoExportaNiMuestraExito()
    {
        // Arrange
        await _sut.CargarAsync();
        _dialogService.AbrirArchivoDestino(Ruta).Returns(_ => throw new UnauthorizedAccessException("Acceso denegado."));
        _dialogService.ConfirmarReintento(Arg.Any<string>()).Returns(false);

        // Act
        await _sut.ExportarPdfAsync();

        // Assert
        _dialogService.Received(1).ConfirmarReintento(Arg.Any<string>());
        _exportacionService.ReceivedCalls().Should().BeEmpty();
        _dialogService.DidNotReceive().MostrarExito(Arg.Any<string>());
        _sut.EstaExportando.Should().BeFalse();
    }

    [Fact]
    public async Task Exportar_ConUnErrorDelServicio_LoMuestraEnPantallaSinReintentar()
    {
        // Arrange
        await _sut.CargarAsync();
        _exportacionService.ExportarAsync(
                IdActor, Arg.Any<ResultadoReporteDto>(), Arg.Any<FormatoExportacionEnum>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperacionNoAutorizadaException("Solo un Gerente puede exportar reportes."));

        // Act
        await _sut.ExportarExcelAsync();

        // Assert
        _sut.MensajeError.Should().Be("Solo un Gerente puede exportar reportes.");
        _dialogService.DidNotReceive().ConfirmarReintento(Arg.Any<string>());
    }

    // ------------------------------------------------------------------ Presentación

    [Fact]
    public async Task CargarAsync_ElTablero_MuestraLasTarjetasConFormatoArgentino()
    {
        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.TotalVendidoTexto.Should().Be("$ 1.234.567,50");
        _sut.TotalVendidoVariacion.Texto.Should().Be("\u25B2 +12,50 % vs período anterior");
        _sut.TotalVendidoVariacion.Estado.Should().Be(EstadoVariacionEnum.Sube);
        _sut.CantidadVentasTexto.Should().Be("1.250");
        _sut.CantidadVentasVariacion.Texto.Should().Be("\u25BC \u22123,10 % vs período anterior");
        _sut.CantidadVentasVariacion.Estado.Should().Be(EstadoVariacionEnum.Baja);
        _sut.MargenBrutoTexto.Should().Be("$ 400.000,00");
        _sut.MargenBrutoVariacion.Estado.Should().Be(EstadoVariacionEnum.SinBase);
        _sut.MargenBrutoVariacion.Texto.Should().Be("Sin datos del período anterior");
        _sut.ValorizacionTexto.Should().Be("$ 5.000.000,00");
        _sut.BajoMinimoTexto.Should().Be("3 bajo mínimo");
        _sut.HayBajoMinimo.Should().BeTrue();
    }

    [Fact]
    public async Task CargarAsync_ElTablero_ArmaLaEvolucionComoLineaSinSuavizarYElMargenComoBarrasConElPrimeroArriba()
    {
        // Act
        await _sut.CargarAsync();

        // Assert: evolución, una sola serie de líneas sin suavizar ni relleno, con el eje de valores desde cero
        var linea = _sut.GraficoEvolucion.Series.Should().ContainSingle().Which.Should().BeOfType<LineSeries<double>>().Subject;
        linea.LineSmoothness.Should().Be(0);
        linea.Fill.Should().BeNull();
        linea.GeometrySize.Should().Be(8);
        _sut.GraficoEvolucion.EjesY[0].MinLimit.Should().Be(0);

        // Assert: margen por categoría, barras horizontales con el orden invertido (LiveCharts dibuja el índice 0 abajo)
        var barras = _sut.GraficoMargenCategoria.Series.Should().ContainSingle().Which.Should().BeOfType<RowSeries<double>>().Subject;
        ((IEnumerable<double>)barras.Values!).Should().Equal(100d, 300d);
        _sut.GraficoMargenCategoria.EjesY[0].Labels.Should().Equal("Papelería", "Libros");
    }

    [Fact]
    public async Task ConsultarAsync_Ranking_MuestraLasDosTablasSinGraficoEnPantalla()
    {
        // Arrange
        _reporteService.ObtenerRankingAsync(IdActor, Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>())
            .Returns(Ranking(cantidad: 20));
        await _sut.CargarAsync();

        // Act
        _sut.ReporteSeleccionado = TipoReporteEnum.Ranking;
        await _sut.EsperarConsultaAutomaticaAsync();

        // Assert: las dos grillas ocupan todo el ancho (D-33); el PDF conserva el gráfico, la pantalla no
        _sut.MostrarRanking.Should().BeTrue();
        _sut.MasVendidos.Should().HaveCount(20);
        _sut.MenosVendidos.Should().ContainSingle();
        _sut.MostrarGraficoReporte.Should().BeFalse();
        _sut.GraficoReporte.TieneDatos.Should().BeFalse();
        _sut.TituloGraficoReporte.Should().BeEmpty();
        _sut.TieneResumen.Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarAsync_Rentabilidad_MuestraLaFilaTotalAparte()
    {
        // Arrange
        _reporteService.ObtenerRentabilidadAsync(IdActor, Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(Rentabilidad(filas: 2));
        await _sut.CargarAsync();

        // Act
        _sut.ReporteSeleccionado = TipoReporteEnum.Rentabilidad;
        await _sut.EsperarConsultaAutomaticaAsync();

        // Assert
        _sut.FilasRentabilidad.Should().HaveCount(2);
        _sut.TieneResumen.Should().BeFalse("el total va en su propia fila alineada con la grilla");
        _sut.TotalRentabilidad.Should().NotBeNull();
        _sut.TotalRentabilidad!.MargenBruto.Should().Be(600m);
        _sut.TotalRentabilidad.MargenPorcentual.Should().Be(30m);
        _sut.TituloGraficoReporte.Should().Be("Margen bruto por categoría");
    }

    [Fact]
    public async Task ReporteSeleccionado_StockCritico_NoMuestraGraficoYUsaTodoElAncho()
    {
        // Arrange
        _reporteService.ObtenerStockCriticoAsync(IdActor, Arg.Any<FiltroInventarioDto>(), Arg.Any<CancellationToken>())
            .Returns(new StockCriticoDto
            {
                Filtro = new FiltroInventarioDto(),
                Filas = [new StockCriticoFilaDto { Descripcion = "Lapicera", Faltante = 4 }],
                ArticulosConsiderados = 10,
                ValorizacionCosto = 1000m,
                ValorizacionVenta = 1500m
            });
        await _sut.CargarAsync();

        // Act
        _sut.ReporteSeleccionado = TipoReporteEnum.StockCritico;
        await _sut.EsperarConsultaAutomaticaAsync();

        // Assert
        _sut.MostrarStockCritico.Should().BeTrue();
        _sut.MostrarGraficoReporte.Should().BeFalse();
        _sut.Resumen.Select(d => d.Etiqueta).Should().Equal("Artículos considerados", "Valorización a costo", "Valorización a venta");
    }

    [Fact]
    public async Task ConsultarAsync_VentasPorPeriodo_MuestraElDesglosePorMedioEnLosTotalesYNoHayGrafico()
    {
        // Arrange
        _reporteService.ObtenerVentasPorPeriodoAsync(IdActor, Arg.Any<FiltroVentasPorPeriodoDto>(), Arg.Any<CancellationToken>())
            .Returns(VentasPorPeriodo(conPuntos: true) with
            {
                TotalVendido = 2300m,
                CantidadVentas = 3,
                Descuentos = 20m,
                Efectivo = 600m,
                TarjetaDebito = 400m,
                TarjetaCredito = 500m,
                TransferenciaQr = 300m,
                CuentaCorriente = 500m
            });
        await _sut.CargarAsync();

        // Act
        _sut.ReporteSeleccionado = TipoReporteEnum.VentasPorPeriodo;
        await _sut.EsperarConsultaAutomaticaAsync();

        // Assert
        _sut.MostrarVentasPorPeriodo.Should().BeTrue();
        _sut.MostrarGraficoReporte.Should().BeFalse();
        _sut.GraficoReporte.TieneDatos.Should().BeFalse();
        _sut.TieneResumen.Should().BeFalse("los totales van en una fila alineada con la grilla");
        _sut.TotalesVentas.Should().NotBeNull();
        _sut.TotalesVentas!.TotalVendido.Should().Be(2300m);
        _sut.TotalesVentas.Efectivo.Should().Be(600m);
        _sut.TotalesVentas.CuentaCorriente.Should().Be(500m);
    }

    [Fact]
    public async Task CambiarDePestana_VolviendoAReportesYaConsultados_ElGraficoSiempreEsElDeSuReporte()
    {
        // Arrange: reportes con y sin gráfico en pantalla (el ranking no lo tiene), consultados en este orden
        _reporteService.ObtenerVentasPorPeriodoAsync(IdActor, Arg.Any<FiltroVentasPorPeriodoDto>(), Arg.Any<CancellationToken>())
            .Returns(VentasPorPeriodo(conPuntos: true));
        _reporteService.ObtenerRankingAsync(IdActor, Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>())
            .Returns(Ranking(cantidad: 3));
        _reporteService.ObtenerRentabilidadAsync(IdActor, Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>())
            .Returns(Rentabilidad(filas: 2));
        await _sut.CargarAsync();

        foreach (var tipo in new[] { TipoReporteEnum.VentasPorPeriodo, TipoReporteEnum.Ranking, TipoReporteEnum.Rentabilidad })
        {
            _sut.ReporteSeleccionado = tipo;
            await _sut.EsperarConsultaAutomaticaAsync();
        }

        // Act + Assert: ir y volver entre las tres pestañas, sin consultar de nuevo
        var recorrido = new[]
        {
            TipoReporteEnum.VentasPorPeriodo, TipoReporteEnum.Ranking, TipoReporteEnum.Rentabilidad,
            TipoReporteEnum.Ranking, TipoReporteEnum.VentasPorPeriodo, TipoReporteEnum.Tablero, TipoReporteEnum.Rentabilidad
        };

        foreach (var tipo in recorrido)
        {
            _sut.ReporteSeleccionado = tipo;
            await _sut.EsperarConsultaAutomaticaAsync();

            switch (tipo)
            {
                case TipoReporteEnum.VentasPorPeriodo:
                    _sut.GraficoReporte.TieneDatos.Should().BeFalse();
                    _sut.TotalesVentas.Should().NotBeNull();
                    break;
                case TipoReporteEnum.Ranking:
                    _sut.GraficoReporte.TieneDatos.Should().BeFalse();
                    _sut.MasVendidos.Select(a => a.Descripcion).Should().Equal("Artículo 1", "Artículo 2", "Artículo 3");
                    break;
                case TipoReporteEnum.Rentabilidad:
                    _sut.GraficoReporte.Series.Should().ContainSingle().Which.Should().BeOfType<RowSeries<double>>();
                    _sut.GraficoReporte.EjesY[0].Labels.Should().Equal("Grupo 2", "Grupo 1");
                    break;
                default:
                    _sut.GraficoReporte.TieneDatos.Should().BeFalse();
                    break;
            }
        }

        await _reporteService.Received(1).ObtenerVentasPorPeriodoAsync(IdActor, Arg.Any<FiltroVentasPorPeriodoDto>(), Arg.Any<CancellationToken>());
        await _reporteService.Received(1).ObtenerRankingAsync(IdActor, Arg.Any<FiltroRankingDto>(), Arg.Any<CancellationToken>());
        await _reporteService.Received(1).ObtenerRentabilidadAsync(IdActor, Arg.Any<FiltroRentabilidadDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CargarAsync_ElTableroConMasDeCincoCategorias_GraficaSoloLasCincoPrimerasYLoIndicaEnElTitulo()
    {
        // Arrange
        var tablero = Tablero() with
        {
            MargenPorCategoria = Enumerable.Range(1, 12)
                .Select(i => new RentabilidadFilaDto { Grupo = $"Categoría {i}", MargenBruto = 1000m - i })
                .ToList()
        };
        _reporteService.ObtenerTableroAsync(IdActor, Arg.Any<FiltroPeriodoDto>(), Arg.Any<CancellationToken>()).Returns(tablero);

        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.GraficoMargenCategoria.EjesY[0].Labels.Should().HaveCount(ConstructorGraficos.MaximoBarrasTablero);
        _sut.GraficoMargenCategoria.EjesY[0].Labels!.Last().Should().Be("Categoría 1");
        _sut.TituloMargenCategoria.Should().Be("Margen bruto por categoría (primeras 5)");
    }

    [Fact]
    public async Task CargarAsync_ElTableroConPocasCategorias_NoAgregaSufijoAlTitulo()
    {
        // Act
        await _sut.CargarAsync();

        // Assert
        _sut.TituloMargenCategoria.Should().Be("Margen bruto por categoría");
    }

    // ------------------------------------------------------------------ Datos de prueba

    private static UsuarioDto Usuario(int id, string nombre, string apellido, RolUsuarioEnum rol) => new()
    {
        IdUsuario = id,
        NombreUsuario = nombre.ToLowerInvariant(),
        Nombre = nombre,
        Apellido = apellido,
        Rol = rol,
        Activo = true,
        CreatedAt = new DateTime(2026, 1, 1)
    };

    private static TableroDto Tablero() => new()
    {
        Filtro = new FiltroPeriodoDto { Desde = new DateOnly(2026, 10, 1), Hasta = Hoy },
        DesdeAnterior = new DateOnly(2026, 9, 1),
        HastaAnterior = new DateOnly(2026, 9, 15),
        TotalVendido = new IndicadorDto { Valor = 1234567.5m, ValorAnterior = 1097000m, VariacionPorcentual = 12.5m },
        CantidadVentas = new IndicadorDto { Valor = 1250m, ValorAnterior = 1290m, VariacionPorcentual = -3.1m },
        MargenBruto = new IndicadorDto { Valor = 400000m, ValorAnterior = 0m, VariacionPorcentual = null },
        MargenPorcentual = 32.4m,
        MargenPorcentualAnterior = 30.3m,
        VariacionMargenPuntos = 2.1m,
        ArticulosBajoStockMinimo = 3,
        ValorizacionInventarioCosto = 5000000m,
        AgrupacionEvolucion = AgrupacionTemporalEnum.Dia,
        EvolucionVentas =
        [
            new PuntoVentasDto { InicioPeriodo = new DateOnly(2026, 10, 1), CantidadVentas = 10, TotalVendido = 1000m },
            new PuntoVentasDto { InicioPeriodo = new DateOnly(2026, 10, 2), CantidadVentas = 12, TotalVendido = 1500m }
        ],
        MargenPorCategoria =
        [
            new RentabilidadFilaDto { Grupo = "Libros", MargenBruto = 300m },
            new RentabilidadFilaDto { Grupo = "Papelería", MargenBruto = 100m }
        ]
    };

    private static VentasPorPeriodoDto VentasPorPeriodo(bool conPuntos) => new()
    {
        Filtro = new FiltroVentasPorPeriodoDto { Desde = new DateOnly(2026, 10, 1), Hasta = Hoy },
        Puntos = conPuntos
            ? [new PuntoVentasDto { InicioPeriodo = new DateOnly(2026, 10, 1), CantidadVentas = 3, TotalVendido = 900m }]
            : []
    };

    private static RankingArticulosDto Ranking(int cantidad) => new()
    {
        Filtro = new FiltroRankingDto { Desde = new DateOnly(2026, 10, 1), Hasta = Hoy },

        // El artículo 1 es el más vendido (20 unidades) y cada uno vende una unidad menos que el anterior.
        MasVendidos = Enumerable.Range(1, cantidad)
            .Select(i => new RankingArticuloDto { IdArticulo = i, Descripcion = $"Artículo {i}", Unidades = 21 - i, IngresoNeto = i * 10m })
            .ToList(),
        MenosVendidos = [new RankingArticuloDto { IdArticulo = 99, Descripcion = "Atlas", Unidades = 0 }]
    };

    private static RentabilidadDto Rentabilidad(int filas) => new()
    {
        Filtro = new FiltroRentabilidadDto { Desde = new DateOnly(2026, 10, 1), Hasta = Hoy },
        Filas = Enumerable.Range(1, filas)
            .Select(i => new RentabilidadFilaDto { Grupo = $"Grupo {i}", Unidades = 5, IngresoNeto = 1000m, Costo = 700m, MargenBruto = 300m, MargenPorcentual = 30m })
            .ToList(),
        Total = new RentabilidadFilaDto
        {
            Grupo = "Total",
            Unidades = 10,
            IngresoNeto = 2000m,
            Costo = 1400m,
            MargenBruto = 600m,
            MargenPorcentual = 30m
        }
    };

    /// <summary>Reloj fijo con la zona de Argentina (UTC-3, sin horario de verano) para no depender de la máquina.</summary>
    private sealed class RelojFijo : TimeProvider
    {
        private static readonly TimeZoneInfo s_argentina = TimeZoneInfo.CreateCustomTimeZone("AR-Test", TimeSpan.FromHours(-3), "AR", "AR");
        private readonly DateTimeOffset _ahoraUtc;

        public RelojFijo(DateTimeOffset ahoraUtc)
        {
            _ahoraUtc = ahoraUtc;
        }

        public override TimeZoneInfo LocalTimeZone => s_argentina;

        public override DateTimeOffset GetUtcNow() => _ahoraUtc;
    }
}
