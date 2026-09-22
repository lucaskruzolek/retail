using FluentAssertions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ArticulosViewModelTests
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
}
