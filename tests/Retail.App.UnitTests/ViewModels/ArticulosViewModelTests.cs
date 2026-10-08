using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ArticulosViewModelTests : IDisposable
{
    private readonly IInventarioService _inventarioService;
    private readonly IArticuloDialogService _dialogService;
    private readonly ArticulosViewModel _sut;

    private readonly List<ArticuloDto> _articulosEjemplo =
    [
        new()
        {
            IdArticulo = 1,
            CodigoBarras = "7791111111111",
            Descripcion = "Cuaderno Rivadavia A4",
            IdCategoria = 1,
            CategoriaNombre = "Escolar",
            IdMarca = 1,
            MarcaNombre = "Rivadavia",
            CostoReposicion = 1000m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 1400m,
            StockActual = 2,
            StockMinimo = 5,
            EsServicio = false // StockBajo = true
        },
        new()
        {
            IdArticulo = 2,
            CodigoBarras = "7792222222222",
            Descripcion = "Bolígrafo Bic Cristal",
            IdCategoria = 1,
            CategoriaNombre = "Escolar",
            IdMarca = 2,
            MarcaNombre = "Bic",
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m,
            StockActual = 50,
            StockMinimo = 10,
            EsServicio = false // StockBajo = false
        },
        new()
        {
            IdArticulo = 3,
            CodigoBarras = null,
            Descripcion = "Señalador Artesanal de Madera",
            IdCategoria = 2,
            CategoriaNombre = "Regalería",
            IdMarca = 3,
            MarcaNombre = "Taller",
            CostoReposicion = 300m,
            PorcentajeGanancia = 100m,
            PrecioVenta = 600m,
            StockActual = 8,
            StockMinimo = 2,
            EsServicio = false // StockBajo = false
        }
    ];

    private readonly List<CategoriaDto> _categoriasEjemplo =
    [
        new() { IdCategoria = 1, NombreCategoria = "Escolar" },
        new() { IdCategoria = 2, NombreCategoria = "Regalería" }
    ];

    private readonly List<MarcaDto> _marcasEjemplo =
    [
        new() { IdMarca = 1, NombreMarca = "Rivadavia" },
        new() { IdMarca = 2, NombreMarca = "Bic" },
        new() { IdMarca = 3, NombreMarca = "Taller" }
    ];

    public ArticulosViewModelTests()
    {
        _inventarioService = Substitute.For<IInventarioService>();
        _dialogService = Substitute.For<IArticuloDialogService>();

        _inventarioService.ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var consulta = callInfo.Arg<ConsultaArticulosDto>();
                var items = _articulosEjemplo.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(consulta.TerminoBusqueda))
                {
                    items = items.Where(a => a.Descripcion.Contains(consulta.TerminoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                                             (a.CodigoBarras != null && a.CodigoBarras.Contains(consulta.TerminoBusqueda, StringComparison.OrdinalIgnoreCase)));
                }
                if (consulta.IdCategoria.HasValue && consulta.IdCategoria.Value > 0)
                {
                    items = items.Where(a => a.IdCategoria == consulta.IdCategoria.Value);
                }
                if (consulta.SoloStockCritico)
                {
                    items = items.Where(a => a.StockBajo);
                }
                var list = items.ToList();
                int tamano = Math.Max(1, consulta.TamanoPagina);
                int pagina = Math.Max(1, consulta.Pagina);
                var paged = list.Skip((pagina - 1) * tamano).Take(tamano).ToList();

                return new ArticulosPaginadosDto
                {
                    Items = paged,
                    TotalRegistros = list.Count,
                    TotalArticulos = _articulosEjemplo.Count,
                    TotalAlertasStock = _articulosEjemplo.Count(a => a.StockBajo),
                    PaginaActual = pagina,
                    TamanoPagina = tamano
                };
            });

        _inventarioService.ListarCategoriasAsync(Arg.Any<CancellationToken>())
            .Returns(_categoriasEjemplo);
        _inventarioService.ListarMarcasAsync(Arg.Any<CancellationToken>())
            .Returns(_marcasEjemplo);

        _sut = new ArticulosViewModel(_inventarioService, _dialogService);
    }

    [Fact]
    public async Task CargarArticulosAsync_DebePoblarListaYCalcularMetricas()
    {
        // Act
        await _sut.CargarArticulosCommand.ExecuteAsync(null);

        // Assert
        _sut.Articulos.Should().HaveCount(3);
        _sut.TotalArticulos.Should().Be(3);
        _sut.TotalAlertasStock.Should().Be(1);
        _sut.CategoriasFiltro.Should().HaveCount(3); // "Todas" + 2 categorías
    }

    [Fact]
    public async Task FiltrarArticulos_PorTexto_MuestraSoloCoincidencias()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);

        // Act
        _sut.TextoBusqueda = "Bic";
        await Task.Delay(350);

        // Assert
        _sut.Articulos.Should().HaveCount(1);
        _sut.Articulos[0].Descripcion.Should().Be("Bolígrafo Bic Cristal");
    }

    [Fact]
    public async Task FiltrarArticulos_PorCategoria_MuestraSoloArticulosDeEsaCategoria()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);

        // Act
        _sut.IdCategoriaFiltro = 2; // Regalería
        await Task.Delay(100);

        // Assert
        _sut.Articulos.Should().HaveCount(1);
        _sut.Articulos[0].Descripcion.Should().Be("Señalador Artesanal de Madera");
    }

    [Fact]
    public async Task FiltrarArticulos_PorSoloStockCritico_MuestraSoloAlertasDeStock()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);

        // Act
        _sut.SoloStockCritico = true;
        await Task.Delay(100);

        // Assert
        _sut.Articulos.Should().HaveCount(1);
        _sut.Articulos[0].Descripcion.Should().Be("Cuaderno Rivadavia A4");
        _sut.Articulos[0].StockBajo.Should().BeTrue();
    }

    [Fact]
    public async Task Paginacion_AvanzarYRetroceder_CambiaPaginaYItems()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);

        // Act - Cambiar tamaño a 2 (con 3 items => 2 páginas)
        _sut.TamanoPagina = 2;
        await Task.Delay(100);

        // Assert página 1
        _sut.TotalPaginas.Should().Be(2);
        _sut.PaginaActual.Should().Be(1);
        _sut.Articulos.Should().HaveCount(2);
        _sut.PuedeAvanzarPagina.Should().BeTrue();
        _sut.PuedeRetrocederPagina.Should().BeFalse();

        // Act - Avanzar
        await _sut.PaginaSiguienteCommand.ExecuteAsync(null);

        // Assert página 2
        _sut.PaginaActual.Should().Be(2);
        _sut.Articulos.Should().HaveCount(1);
        _sut.PuedeAvanzarPagina.Should().BeFalse();
        _sut.PuedeRetrocederPagina.Should().BeTrue();

        // Act - Retroceder
        await _sut.PaginaAnteriorCommand.ExecuteAsync(null);
        _sut.PaginaActual.Should().Be(1);
        _sut.Articulos.Should().HaveCount(2);
    }

    [Fact]
    public async Task NuevoArticuloAsync_CuandoUsuarioConfirma_LlamaCrearYRecarga()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);

        var nuevoDto = new CrearArticuloDto
        {
            Descripcion = "Regla 20cm",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 200m,
            PorcentajeGanancia = 50m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        _dialogService.MostrarDialogoCrear(
            Arg.Any<IReadOnlyList<CategoriaDto>>(),
            Arg.Any<IReadOnlyList<MarcaDto>>(),
            Arg.Any<Func<CrearArticuloDto, Task>>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Func<CrearArticuloDto, Task>>();
                callback?.Invoke(nuevoDto);
                return nuevoDto;
            });

        // Act
        await _sut.NuevoArticuloCommand.ExecuteAsync(null);

        // Assert
        await _inventarioService.Received(1).CrearArticuloAsync(nuevoDto, Arg.Any<CancellationToken>());
        await _inventarioService.Received(2).ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NuevoArticuloAsync_CuandoUsuarioCancela_NoLlamaCrear()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);

        _dialogService.MostrarDialogoCrear(
            Arg.Any<IReadOnlyList<CategoriaDto>>(),
            Arg.Any<IReadOnlyList<MarcaDto>>(),
            Arg.Any<Func<CrearArticuloDto, Task>>())
            .Returns((CrearArticuloDto?)null);

        // Act
        await _sut.NuevoArticuloCommand.ExecuteAsync(null);

        // Assert
        await _inventarioService.DidNotReceive().CrearArticuloAsync(Arg.Any<CrearArticuloDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditarArticuloAsync_ConArticuloSeleccionado_LlamaModificarYRecarga()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);
        _sut.ArticuloSeleccionado = _articulosEjemplo[0];

        var modDto = new ActualizarArticuloDto
        {
            IdArticulo = 1,
            Descripcion = "Cuaderno Rivadavia Modificado",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 1200m,
            PorcentajeGanancia = 40m,
            StockActual = 5,
            StockMinimo = 2,
            EsServicio = false
        };

        _dialogService.MostrarDialogoModificar(
            Arg.Any<ArticuloDto>(),
            Arg.Any<IReadOnlyList<CategoriaDto>>(),
            Arg.Any<IReadOnlyList<MarcaDto>>(),
            Arg.Any<Func<ActualizarArticuloDto, Task>>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Func<ActualizarArticuloDto, Task>>();
                callback?.Invoke(modDto);
                return modDto;
            });

        // Act
        await _sut.EditarArticuloCommand.ExecuteAsync(null);

        // Assert
        await _inventarioService.Received(1).ActualizarArticuloAsync(modDto, Arg.Any<CancellationToken>());
        await _inventarioService.Received(2).ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaArticuloAsync_CuandoUsuarioConfirma_LlamaBajaYRecarga()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);
        _sut.ArticuloSeleccionado = _articulosEjemplo[0];

        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        // Act
        await _sut.BajaArticuloCommand.ExecuteAsync(null);

        // Assert
        await _inventarioService.Received(1).BajaArticuloAsync(1, Arg.Any<CancellationToken>());
        await _inventarioService.Received(2).ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaArticuloAsync_CuandoUsuarioCancela_NoLlamaBaja()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);
        _sut.ArticuloSeleccionado = _articulosEjemplo[0];

        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        // Act
        await _sut.BajaArticuloCommand.ExecuteAsync(null);

        // Assert
        await _inventarioService.DidNotReceive().BajaArticuloAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarFiltro_ConBusquedaEnCurso_EsperaQueTermineYNoMuestraErrorDeLaBusquedaObsoleta()
    {
        // Arrange: la búsqueda "Bic" está consultando cuando el usuario activa "solo stock crítico"
        var consultoBic = new TaskCompletionSource();
        var respuestaBic = new TaskCompletionSource<ArticulosPaginadosDto>();
        _inventarioService.ListarArticulosPaginadosAsync(
                Arg.Is<ConsultaArticulosDto>(c => c.TerminoBusqueda == "Bic" && !c.SoloStockCritico),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                consultoBic.TrySetResult();
                return respuestaBic.Task;
            });
        await _sut.CargarArticulosAsync();
        _sut.TextoBusqueda = "Bic";
        await consultoBic.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act: antes los dos usaban el DbContext a la vez y el segundo abría un diálogo de error (H-19)
        _sut.SoloStockCritico = true;
        respuestaBic.SetException(new InvalidOperationException("Operation cancelled by user"));
        await _sut.CargarArticulosAsync().WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _dialogService.DidNotReceiveWithAnyArgs().MostrarError(default!, default!);
        _sut.MensajeError.Should().BeNull();
        _sut.IsBusy.Should().BeFalse();
    }

    // ---------- Presentaciones y fraccionamiento (RF-21) ----------

    [Fact]
    public async Task CrearPresentacionAsync_CuandoUsuarioConfirma_LlamaServicioYRecarga()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);
        var origen = _articulosEjemplo[0];
        var dto = new CrearPresentacionDto
        {
            IdArticuloOrigen = origen.IdArticulo,
            UnidadesPorOrigen = 10,
            Descripcion = "Cuaderno Rivadavia A4 (unidad)",
            PorcentajeGanancia = 40m,
            StockMinimo = 0
        };
        _dialogService.MostrarDialogoCrearPresentacion(origen, Arg.Any<Func<CrearPresentacionDto, Task>>())
            .Returns(callInfo =>
            {
                callInfo.Arg<Func<CrearPresentacionDto, Task>>()?.Invoke(dto);
                return dto;
            });

        // Act
        await _sut.CrearPresentacionCommand.ExecuteAsync(origen);

        // Assert
        await _inventarioService.Received(1).CrearPresentacionAsync(dto, Arg.Any<CancellationToken>());
        await _inventarioService.Received(2).ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearPresentacionAsync_ArticuloDerivado_InformaSinAbrirDialogo()
    {
        // Act
        await _sut.CrearPresentacionCommand.ExecuteAsync(Presentacion(idOrigen: 1));

        // Assert
        _dialogService.DidNotReceive().MostrarDialogoCrearPresentacion(Arg.Any<ArticuloDto>(), Arg.Any<Func<CrearPresentacionDto, Task>>());
        _dialogService.Received(1).MostrarInformacion(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("ya es una presentación")));
    }

    [Fact]
    public async Task CrearPresentacionAsync_Servicio_InformaSinAbrirDialogo()
    {
        // Arrange
        var servicio = _articulosEjemplo[1] with { EsServicio = true };

        // Act
        await _sut.CrearPresentacionCommand.ExecuteAsync(servicio);

        // Assert
        _dialogService.DidNotReceive().MostrarDialogoCrearPresentacion(Arg.Any<ArticuloDto>(), Arg.Any<Func<CrearPresentacionDto, Task>>());
    }

    [Fact]
    public async Task FraccionarAsync_CuandoUsuarioConfirma_LlamaFraccionarYRecarga()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);
        var origen = _articulosEjemplo[0];
        var derivado = Presentacion(idOrigen: origen.IdArticulo);
        _inventarioService.ObtenerPorIdAsync(origen.IdArticulo, Arg.Any<CancellationToken>()).Returns(origen);
        _inventarioService.FraccionarAsync(Arg.Any<FraccionarDto>(), Arg.Any<CancellationToken>()).Returns(20);
        _dialogService.MostrarDialogoFraccionar(derivado, origen, Arg.Any<Func<FraccionarDto, Task<int>>>())
            .Returns(callInfo => callInfo.Arg<Func<FraccionarDto, Task<int>>>()!
                .Invoke(new FraccionarDto { IdArticuloDerivado = derivado.IdArticulo, CantidadOrigen = 2 })
                .GetAwaiter()
                .GetResult());

        // Act
        await _sut.FraccionarCommand.ExecuteAsync(derivado);

        // Assert
        await _inventarioService.Received(1).FraccionarAsync(
            Arg.Is<FraccionarDto>(d => d.IdArticuloDerivado == derivado.IdArticulo && d.CantidadOrigen == 2),
            Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("20 unidad(es)")));
        await _inventarioService.Received(2).ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FraccionarAsync_CuandoUsuarioCancela_NoRecarga()
    {
        // Arrange
        await _sut.CargarArticulosCommand.ExecuteAsync(null);
        var origen = _articulosEjemplo[0];
        _inventarioService.ObtenerPorIdAsync(origen.IdArticulo, Arg.Any<CancellationToken>()).Returns(origen);
        _dialogService.MostrarDialogoFraccionar(Arg.Any<ArticuloDto>(), Arg.Any<ArticuloDto>(), Arg.Any<Func<FraccionarDto, Task<int>>>())
            .Returns((int?)null);

        // Act
        await _sut.FraccionarCommand.ExecuteAsync(Presentacion(idOrigen: origen.IdArticulo));

        // Assert
        await _inventarioService.DidNotReceive().FraccionarAsync(Arg.Any<FraccionarDto>(), Arg.Any<CancellationToken>());
        await _inventarioService.Received(1).ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FraccionarAsync_ArticuloNoDerivado_InformaSinConsultarElOrigen()
    {
        // Act
        await _sut.FraccionarCommand.ExecuteAsync(_articulosEjemplo[1]);

        // Assert
        await _inventarioService.DidNotReceive().ObtenerPorIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("no es una presentación derivada")));
    }

    [Fact]
    public async Task FraccionarAsync_OrigenDadoDeBaja_MuestraErrorSinAbrirDialogo()
    {
        // Arrange
        _inventarioService.ObtenerPorIdAsync(99, Arg.Any<CancellationToken>()).Returns((ArticuloDto?)null);

        // Act
        await _sut.FraccionarCommand.ExecuteAsync(Presentacion(idOrigen: 99));

        // Assert
        _dialogService.DidNotReceive().MostrarDialogoFraccionar(Arg.Any<ArticuloDto>(), Arg.Any<ArticuloDto>(), Arg.Any<Func<FraccionarDto, Task<int>>>());
        _dialogService.Received(1).MostrarError(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("dado de baja")));
    }

    private static ArticuloDto Presentacion(int idOrigen)
    {
        return new ArticuloDto
        {
            IdArticulo = 10,
            Descripcion = "Cuaderno Rivadavia A4 (unidad)",
            IdArticuloOrigen = idOrigen,
            UnidadesPorOrigen = 10,
            CostoReposicion = 100m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 140m,
            StockActual = 0,
            StockMinimo = 0,
            EsServicio = false
        };
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }
}
