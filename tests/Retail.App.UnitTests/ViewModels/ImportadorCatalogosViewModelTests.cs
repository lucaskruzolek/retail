using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Proveedores;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ImportadorCatalogosViewModelTests : IDisposable
{
    private readonly IProveedorService _proveedorService;
    private readonly IProveedorDialogService _dialogService;
    private readonly IInventarioService _inventarioService;
    private readonly INavigationService _navigationService;
    private static readonly TimeSpan LimiteEspera = TimeSpan.FromSeconds(5);

    private readonly ImportadorCatalogosViewModel _sut;

    public ImportadorCatalogosViewModelTests()
    {
        _proveedorService = Substitute.For<IProveedorService>();
        _dialogService = Substitute.For<IProveedorDialogService>();
        _inventarioService = Substitute.For<IInventarioService>();
        _navigationService = Substitute.For<INavigationService>();

        // Por defecto el catálogo responde vacío. Sin esto el mock devuelve null, la carga que dispara
        // ProveedorActivo falla y su mensaje de error puede pisar el de otro comando (H-18).
        _proveedorService.ListarItemsCatalogoAsync(default!, default)
            .ReturnsForAnyArgs(new CatalogoPaginadoDto());

        _sut = new ImportadorCatalogosViewModel(
            _proveedorService,
            _dialogService,
            _inventarioService,
            _navigationService,
            NullLogger<ImportadorCatalogosViewModel>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Inicializar_ConProveedor_FijaProveedorActivoYCargaCatalogo()
    {
        // Arrange
        var proveedor = new ProveedorDto
        {
            IdProveedor = 3,
            RazonSocial = "Distribuidora Papelera",
            Cuit = "30-33333333-3"
        };

        var paginado = new CatalogoPaginadoDto
        {
            Items = new List<CatalogoProveedorDto>
            {
                new()
                {
                    Id = 1,
                    IdProveedor = 3,
                    CodigoProveedor = "PAP-01",
                    DescripcionProveedor = "Resma A4 75g",
                    CostoReposicion = 4500m,
                    FechaActualizacion = DateTime.UtcNow
                }
            },
            TotalRegistros = 1,
            PaginaActual = 1,
            TamanoPagina = 50
        };

        _proveedorService.ListarItemsCatalogoAsync(Arg.Any<ConsultaCatalogoProveedorDto>(), Arg.Any<CancellationToken>())
            .Returns(paginado);

        // Act
        await _sut.InicializarAsync(proveedor);

        // Assert
        _sut.ProveedorActivo.Should().Be(proveedor);
        _sut.TotalItemsCatalogo.Should().Be(1);
        _sut.PaginaActual.Should().Be(1);
        _sut.TotalPaginas.Should().Be(1);
    }

    [Fact]
    public async Task VincularArticuloCommand_CuandoDialogoRetornaTrue_RecargaItems()
    {
        // Arrange
        var proveedor = new ProveedorDto { IdProveedor = 5, RazonSocial = "Proveedor 5", Cuit = "30-55555555-5" };
        var item = new CatalogoProveedorDto
        {
            Id = 12,
            IdProveedor = 5,
            CodigoProveedor = "SKU-99",
            DescripcionProveedor = "Item para vincular",
            CostoReposicion = 200m,
            FechaActualizacion = DateTime.UtcNow
        };

        _proveedorService.ListarItemsCatalogoAsync(Arg.Any<ConsultaCatalogoProveedorDto>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogoPaginadoDto
            {
                Items = new List<CatalogoProveedorDto> { item },
                TotalRegistros = 1,
                PaginaActual = 1,
                TamanoPagina = 50
            });

        await _sut.InicializarAsync(proveedor);
        _dialogService.AbrirSelectorVinculacionArticuloAsync(item).Returns(true);

        // Act
        await _sut.VincularArticuloCommand.ExecuteAsync(item);

        // Assert
        await _dialogService.Received(1).AbrirSelectorVinculacionArticuloAsync(item);
        _sut.MensajeEstado.Should().Contain("vinculado con éxito");
    }

    [Fact]
    public async Task IncorporarSeleccionadosCommand_SinElementosSeleccionados_EstableceMensajeDeError()
    {
        // Arrange
        var proveedor = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
        _sut.ProveedorActivo = proveedor;

        // Act
        await _sut.IncorporarSeleccionadosCommand.ExecuteAsync(new List<CatalogoProveedorDto>());

        // Assert
        _sut.TieneMensajeError.Should().BeTrue();
        _sut.MensajeError.Should().Contain("Seleccione al menos un artículo");
    }

    [Fact]
    public async Task CargarProveedoresDisponibles_DebePoblarColeccionYSeleccionarPrimerProveedor()
    {
        // Arrange
        var lista = new List<ProveedorDto>
        {
            new() { IdProveedor = 1, RazonSocial = "Distribuidora A", Cuit = "30-11111111-1" },
            new() { IdProveedor = 2, RazonSocial = "Distribuidora B", Cuit = "30-22222222-2" }
        };

        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(lista);

        // Act
        await _sut.CargarProveedoresDisponiblesAsync();

        // Assert
        _sut.ProveedoresDisponibles.Should().HaveCount(2);
        _sut.ProveedorActivo.Should().NotBeNull();
        _sut.ProveedorActivo!.IdProveedor.Should().Be(1);
    }

    [Fact]
    public async Task ImportarPlanillaCommand_CuandoDialogoRetornaResultado_ActualizaResultadoYRecargaCatalogo()
    {
        // Arrange
        var proveedor = new ProveedorDto { IdProveedor = 7, RazonSocial = "Distribuidora Mayorista", Cuit = "30-77777777-7" };
        _sut.ProveedorActivo = proveedor;

        var resultadoDto = new ResultadoImportacionDto
        {
            TotalFilasProcesadas = 100,
            NuevosRegistros = 80,
            PreciosActualizados = 15,
            FilasConError = 5,
            TiempoTranscurrido = TimeSpan.FromSeconds(2)
        };

        _dialogService.AbrirImportarPlanillaAsync(proveedor).Returns(resultadoDto);
        _proveedorService.ListarItemsCatalogoAsync(Arg.Any<ConsultaCatalogoProveedorDto>(), Arg.Any<CancellationToken>())
            .Returns(new CatalogoPaginadoDto { Items = new List<CatalogoProveedorDto>(), TotalRegistros = 0, PaginaActual = 1, TamanoPagina = 50 });

        // Act
        await _sut.ImportarPlanillaCommand.ExecuteAsync(null);

        // Assert
        await _dialogService.Received(1).AbrirImportarPlanillaAsync(proveedor);
        _sut.UltimoResultado.Should().Be(resultadoDto);
        _sut.MensajeEstado.Should().Contain("Importación completada");
    }

    [Fact]
    public async Task FiltroEstado_CuandoCambia_ReiniciaPaginaYRecargaCatalogo()
    {
        // Arrange
        var proveedor = new ProveedorDto { IdProveedor = 8, RazonSocial = "Distribuidora Test", Cuit = "30-88888888-8" };
        await _sut.InicializarAsync(proveedor);
        _sut.PaginaActual = 3;

        // Act
        _sut.FiltroEstado = EstadoVinculacionCatalogoEnum.YaEnTienda;
        await Task.Delay(50);

        // Assert
        _sut.PaginaActual.Should().Be(1);
        await _proveedorService.Received().ListarItemsCatalogoAsync(
            Arg.Is<ConsultaCatalogoProveedorDto>(c => c.EstadoVinculacion == EstadoVinculacionCatalogoEnum.YaEnTienda),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CargarCatalogoAsync_ConCargaReemplazadaQueRespondeTarde_EsperaQueLibereElContextoYMuestraSoloLaUltima()
    {
        // Arrange: la carga de "bi" ya está consultando cuando el usuario escribe "bic"
        var consultoBi = new TaskCompletionSource();
        var respuestaBi = new TaskCompletionSource<CatalogoPaginadoDto>();
        ConfigurarRespuesta("bi", respuestaBi.Task, consultoBi);
        ConfigurarRespuesta("bic", Task.FromResult(Paginado("BIC-01")));
        _sut.TextoBusquedaCatalogo = "bi";
        _sut.ProveedorActivo = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
        await consultoBi.Task.WaitAsync(LimiteEspera);

        // Act
        _sut.TextoBusquedaCatalogo = "bic";
        await Task.Delay(400);
        var consultoBicAntesDeLiberar = ConsultasDe("bic");
        respuestaBi.SetResult(Paginado("BI-99"));
        await _sut.CargarCatalogoAsync().WaitAsync(LimiteEspera);

        // Assert
        consultoBicAntesDeLiberar.Should().Be(0, "mientras la carga anterior ocupa el DbContext, la nueva espera (H-19)");
        _sut.ItemsCatalogo.Should().ContainSingle().Which.CodigoProveedor.Should().Be("BIC-01", "la respuesta tardía de \"bi\" se descarta (H-18)");
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task CargarCatalogoAsync_ConCargaObsoletaQueFalla_NoMuestraError()
    {
        // Arrange
        var consultoBi = new TaskCompletionSource();
        var respuestaBi = new TaskCompletionSource<CatalogoPaginadoDto>();
        ConfigurarRespuesta("bi", respuestaBi.Task, consultoBi);
        ConfigurarRespuesta("bic", Task.FromResult(Paginado("BIC-01")));
        _sut.TextoBusquedaCatalogo = "bi";
        _sut.ProveedorActivo = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
        await consultoBi.Task.WaitAsync(LimiteEspera);

        // Act: la consulta reemplazada termina como termina una consulta cancelada de SQL Server
        _sut.TextoBusquedaCatalogo = "bic";
        respuestaBi.SetException(new InvalidOperationException("Operation cancelled by user"));
        await _sut.CargarCatalogoAsync().WaitAsync(LimiteEspera);

        // Assert
        _sut.MensajeError.Should().BeNull();
        _sut.ItemsCatalogo.Should().ContainSingle().Which.CodigoProveedor.Should().Be("BIC-01");
    }

    [Fact]
    public async Task Dispose_ConCargaEnCurso_NoMuestraErrorNiResultado()
    {
        // Arrange
        var consultoBi = new TaskCompletionSource();
        var respuestaBi = new TaskCompletionSource<CatalogoPaginadoDto>();
        ConfigurarRespuesta("bi", respuestaBi.Task, consultoBi);
        _sut.TextoBusquedaCatalogo = "bi";
        _sut.ProveedorActivo = new ProveedorDto { IdProveedor = 1, RazonSocial = "Prov 1", Cuit = "30-11111111-1" };
        await consultoBi.Task.WaitAsync(LimiteEspera);

        // Act: se sale de la pantalla y la consulta termina con el DbContext ya descartado
        _sut.Dispose();
        respuestaBi.SetException(new ObjectDisposedException("RetailDbContext"));
        await Task.Delay(100);

        // Assert
        _sut.MensajeError.Should().BeNull("no debe aparecer un error de una pantalla que ya no está");
        _sut.ItemsCatalogo.Should().BeEmpty();
    }

    private void ConfigurarRespuesta(string terminoBusqueda, Task<CatalogoPaginadoDto> respuesta, TaskCompletionSource? alConsultar = null)
    {
        _proveedorService.ListarItemsCatalogoAsync(
                Arg.Is<ConsultaCatalogoProveedorDto>(c => c.TerminoBusqueda == terminoBusqueda),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                alConsultar?.TrySetResult();
                return respuesta;
            });
    }

    private int ConsultasDe(string terminoBusqueda)
    {
        return _proveedorService.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == nameof(IProveedorService.ListarItemsCatalogoAsync)
                        && c.GetArguments()[0] is ConsultaCatalogoProveedorDto consulta
                        && consulta.TerminoBusqueda == terminoBusqueda);
    }
    private static CatalogoPaginadoDto Paginado(string codigo)
    {
        return new CatalogoPaginadoDto
        {
            Items = new List<CatalogoProveedorDto>
            {
                new()
                {
                    Id = 1,
                    IdProveedor = 1,
                    CodigoProveedor = codigo,
                    DescripcionProveedor = $"Artículo {codigo}",
                    CostoReposicion = 100m,
                    FechaActualizacion = DateTime.UtcNow
                }
            },
            TotalRegistros = 1,
            PaginaActual = 1,
            TamanoPagina = 50
        };
    }
}
