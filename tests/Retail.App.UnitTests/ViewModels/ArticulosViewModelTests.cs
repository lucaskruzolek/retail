using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Retail.App.ViewModels.Pedidos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ArticulosViewModelTests : IDisposable
{
    private readonly IInventarioService _inventarioService;
    private readonly IProveedorService _proveedorService;
    private readonly IArticuloDialogService _dialogService;
    private readonly ArticulosViewModel _sut;

    private readonly List<ProveedorDto> _proveedoresEjemplo =
    [
        new() { IdProveedor = 1, RazonSocial = "Distribuidora Papelera", Cuit = "30111111118" },
        new() { IdProveedor = 2, RazonSocial = "Librería Mayorista", Cuit = "30222222224" }
    ];

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
        _proveedorService = Substitute.For<IProveedorService>();

        _proveedorService.ListarProveedoresAsync(Arg.Any<CancellationToken>())
            .Returns(_proveedoresEjemplo);

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

        _sut = new ArticulosViewModel(_inventarioService, _dialogService, _proveedorService);
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
        _sut.MarcasFiltro.Should().HaveCount(4); // "Todas" + 3 marcas
        _sut.DistribuidoresFiltro.Should().HaveCount(4); // "Todos", "Ninguno" + 2 proveedores
        _sut.OpcionesStockFiltro.Should().HaveCount(3); // "Todos", "Stock crítico", "Sin stock"
    }

    [Fact]
    public void LimpiarFiltros_DebeRestablecerFiltrosYDispararRecarga()
    {
        // Arrange
        _sut.TextoBusqueda = "Cuaderno";
        _sut.IdCategoriaFiltro = 1;
        _sut.IdMarcaFiltro = 2;
        _sut.IdDistribuidorFiltro = 1;
        _sut.FiltroStock = FiltroStockArticuloEnum.StockCritico;

        _sut.HayFiltrosActivos.Should().BeTrue();
        _sut.LimpiarFiltrosCommand.CanExecute(null).Should().BeTrue();

        // Act
        _sut.LimpiarFiltrosCommand.Execute(null);

        // Assert
        _sut.TextoBusqueda.Should().BeEmpty();
        _sut.IdCategoriaFiltro.Should().Be(0);
        _sut.IdMarcaFiltro.Should().Be(0);
        _sut.IdDistribuidorFiltro.Should().Be(0);
        _sut.FiltroStock.Should().Be(FiltroStockArticuloEnum.Todos);
        _sut.HayFiltrosActivos.Should().BeFalse();
        _sut.LimpiarFiltrosCommand.CanExecute(null).Should().BeFalse();
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
        _sut.Articulos[0].Articulo.Descripcion.Should().Be("Bolígrafo Bic Cristal");
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
        _sut.Articulos[0].Articulo.Descripcion.Should().Be("Señalador Artesanal de Madera");
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
        _sut.Articulos[0].Articulo.Descripcion.Should().Be("Cuaderno Rivadavia A4");
        _sut.Articulos[0].Articulo.StockBajo.Should().BeTrue();
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
            Arg.Any<IReadOnlyList<FuenteArticuloDto>>(),
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
    public async Task EditarArticuloAsync_ArticuloDeCompra_AbreElFormularioConSusFuentes()
    {
        // Arrange (Track 4.3, A-19, M5)
        await _sut.CargarArticulosCommand.ExecuteAsync(null);
        IReadOnlyList<FuenteArticuloDto> fuentes = [new FuenteArticuloDto { IdCatalogoProveedor = 40, EsPrincipal = true }];
        _inventarioService.ObtenerFuentesDeArticuloAsync(1, Arg.Any<CancellationToken>()).Returns(fuentes);

        // Act
        await _sut.EditarArticuloCommand.ExecuteAsync(_articulosEjemplo[0]);

        // Assert
        _dialogService.Received(1).MostrarDialogoModificar(
            _articulosEjemplo[0],
            fuentes,
            Arg.Any<IReadOnlyList<CategoriaDto>>(),
            Arg.Any<IReadOnlyList<MarcaDto>>(),
            Arg.Any<Func<ActualizarArticuloDto, Task>>());
    }

    [Fact]
    public async Task EditarArticuloAsync_PresentacionDerivada_NoConsultaFuentes()
    {
        // Arrange: una presentación no tiene fuentes propias (RF-21)
        await _sut.CargarArticulosCommand.ExecuteAsync(null);
        var derivado = _articulosEjemplo[0] with { IdArticulo = 9, IdArticuloOrigen = 1, UnidadesPorOrigen = 10 };

        // Act
        await _sut.EditarArticuloCommand.ExecuteAsync(derivado);

        // Assert
        await _inventarioService.DidNotReceive().ObtenerFuentesDeArticuloAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarDialogoModificar(
            derivado,
            Arg.Is<IReadOnlyList<FuenteArticuloDto>>(f => f.Count == 0),
            Arg.Any<IReadOnlyList<CategoriaDto>>(),
            Arg.Any<IReadOnlyList<MarcaDto>>(),
            Arg.Any<Func<ActualizarArticuloDto, Task>>());
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

    private ArticulosViewModel CrearConClasificacion(
        IClasificacionDialogService? dialogoClasificacion,
        bool esGerente = false,
        bool esEncargado = false)
    {
        var sesion = Substitute.For<ICurrentUserSession>();
        sesion.EsGerente.Returns(esGerente);
        sesion.EsEncargado.Returns(esEncargado);
        return new ArticulosViewModel(
            _inventarioService, _dialogService, _proveedorService, null, dialogoClasificacion, sesion);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void PuedeAdministrarClasificacion_SegunElRol_SoloGerenteYEncargado(bool esGerente, bool esEncargado, bool esperado)
    {
        using var sut = CrearConClasificacion(Substitute.For<IClasificacionDialogService>(), esGerente, esEncargado);

        sut.PuedeAdministrarClasificacion.Should().Be(esperado);
        sut.AdministrarClasificacionCommand.CanExecute(null).Should().Be(esperado);
    }

    [Fact]
    public void PuedeAdministrarClasificacion_SinServicioDeDialogo_EsFalso()
    {
        using var sut = CrearConClasificacion(null, esGerente: true);

        sut.PuedeAdministrarClasificacion.Should().BeFalse();
    }

    [Fact]
    public async Task AdministrarClasificacion_ConCambios_RecargaCombosYConservaLaSeleccion()
    {
        var dialogoClasificacion = Substitute.For<IClasificacionDialogService>();
        dialogoClasificacion.AbrirAdministracion().Returns(true);
        using var sut = CrearConClasificacion(dialogoClasificacion, esGerente: true);
        await sut.CargarArticulosCommand.ExecuteAsync(null);
        sut.IdCategoriaFiltro = 2;
        sut.IdMarcaFiltro = 3;

        // El diálogo agregó una categoría y dio de baja otra marca (la 1).
        _inventarioService.ListarCategoriasAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CategoriaDto>(_categoriasEjemplo) { new() { IdCategoria = 9, NombreCategoria = "Nueva" } });
        _inventarioService.ListarMarcasAsync(Arg.Any<CancellationToken>())
            .Returns(_marcasEjemplo.Where(m => m.IdMarca != 1).ToList());

        await sut.AdministrarClasificacionCommand.ExecuteAsync(null);

        sut.CategoriasFiltro.Should().Contain(c => c.IdCategoria == 9);
        sut.MarcasFiltro.Should().NotContain(m => m.IdMarca == 1);
        sut.IdCategoriaFiltro.Should().Be(2);
        sut.IdMarcaFiltro.Should().Be(3);
    }

    [Fact]
    public async Task AdministrarClasificacion_SeleccionEliminada_VuelveATodas()
    {
        var dialogoClasificacion = Substitute.For<IClasificacionDialogService>();
        dialogoClasificacion.AbrirAdministracion().Returns(true);
        using var sut = CrearConClasificacion(dialogoClasificacion, esEncargado: true);
        await sut.CargarArticulosCommand.ExecuteAsync(null);
        sut.IdMarcaFiltro = 2;

        _inventarioService.ListarMarcasAsync(Arg.Any<CancellationToken>())
            .Returns(_marcasEjemplo.Where(m => m.IdMarca != 2).ToList());

        await sut.AdministrarClasificacionCommand.ExecuteAsync(null);

        sut.IdMarcaFiltro.Should().Be(0);
    }

    [Fact]
    public async Task AdministrarClasificacion_SinCambios_NoRecargaLosCombos()
    {
        var dialogoClasificacion = Substitute.For<IClasificacionDialogService>();
        dialogoClasificacion.AbrirAdministracion().Returns(false);
        using var sut = CrearConClasificacion(dialogoClasificacion, esGerente: true);
        await sut.CargarArticulosCommand.ExecuteAsync(null);
        _inventarioService.ClearReceivedCalls();

        await sut.AdministrarClasificacionCommand.ExecuteAsync(null);

        await _inventarioService.DidNotReceive().ListarCategoriasAsync(Arg.Any<CancellationToken>());
        await _inventarioService.DidNotReceive().ListarMarcasAsync(Arg.Any<CancellationToken>());
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

    // ------------------------------------------------------------------ //
    // Selección y pedido al proveedor (Track 4.3, L2a-2)                 //
    // ------------------------------------------------------------------ //

    private static readonly string[] AvisosEsperadosDeExcluidos =
    [
        "Bolígrafo Bic Cristal: no tiene fuentes",
        "Fotocopia simple faz: es un servicio: no se pide a proveedores"
    ];

    private sealed class EscenarioPedido : IDisposable
    {
        public IPedidoService PedidoService { get; } = Substitute.For<IPedidoService>();
        public IPedidoDialogService PedidoDialogService { get; } = Substitute.For<IPedidoDialogService>();
        public ICurrentUserSession Session { get; } = Substitute.For<ICurrentUserSession>();
        public ArticulosViewModel Sut { get; }

        public EscenarioPedido(IInventarioService inventarioService, IArticuloDialogService dialogService, bool esEncargado = true)
        {
            Session.IdUsuario.Returns(7);
            Session.EsEncargado.Returns(esEncargado);
            Sut = new ArticulosViewModel(
                inventarioService,
                dialogService,
                session: Session,
                flujoPedir: new FlujoPedirArticulos(PedidoService, PedidoDialogService, Session, NullLogger<FlujoPedirArticulos>.Instance));
        }

        public void Dispose()
        {
            Sut.Dispose();
        }
    }

    /// <summary>Una página con un artículo vinculado (catálogo 40), uno sin proveedor y un servicio.</summary>
    private async Task<EscenarioPedido> CrearEscenarioPedidoAsync(bool esEncargado = true)
    {
        var articulos = new List<ArticuloDto>
        {
            _articulosEjemplo[0] with { IdCatalogoProveedor = 40, ProveedorNombre = "Distribuidora Papelera" },
            _articulosEjemplo[1],
            _articulosEjemplo[2] with { Descripcion = "Fotocopia simple faz", EsServicio = true }
        };
        _inventarioService.ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>())
            .Returns(new ArticulosPaginadosDto { Items = articulos, TotalRegistros = 3, TotalArticulos = 3, PaginaActual = 1, TamanoPagina = 10 });

        var escenario = new EscenarioPedido(_inventarioService, _dialogService, esEncargado);
        await escenario.Sut.CargarArticulosAsync();
        return escenario;
    }

    private static void ConfigurarPedidoConfirmado(EscenarioPedido escenario)
    {
        escenario.PedidoService.ObtenerVistaPreviaAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new VistaPreviaPedidoDto());
        escenario.PedidoDialogService.MostrarPedirArticulos(default!, default!, default)
            .ReturnsForAnyArgs(RespuestaPedirArticulos.Confirmada([new RenglonAAgregarDto { IdCatalogoProveedor = 40, Cantidad = 2 }]));
        escenario.PedidoService.AgregarVariosAlPedidoAsync(Arg.Any<AgregarVariosAlPedidoDto>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoAgregarAlPedidoDto
            {
                Pedidos = [new PedidoResumenDto { IdPedido = 12, IdProveedor = 1, RazonSocialProveedor = "Distribuidora Papelera" }]
            });
    }

    [Fact]
    public async Task SeleccionarTodos_MarcarYDesmarcarUnaFila_ReflejaTodasNingunaOAlgunas()
    {
        using var escenario = await CrearEscenarioPedidoAsync();
        var sut = escenario.Sut;

        sut.SeleccionarTodos = true;
        sut.Articulos.Should().OnlyContain(f => f.EstaSeleccionado);
        sut.TieneArticulosSeleccionados.Should().BeTrue();

        sut.Articulos[1].EstaSeleccionado = false;
        sut.SeleccionarTodos.Should().BeNull("hay filas marcadas y otras no");

        sut.SeleccionarTodos = false;
        sut.Articulos.Should().OnlyContain(f => !f.EstaSeleccionado);
        sut.TieneArticulosSeleccionados.Should().BeFalse();
    }

    [Fact]
    public async Task CargarArticulosAsync_ConFilasMarcadas_LaNuevaPaginaEmpiezaSinSeleccion()
    {
        using var escenario = await CrearEscenarioPedidoAsync();
        escenario.Sut.SeleccionarTodos = true;

        await escenario.Sut.CargarArticulosAsync();

        escenario.Sut.Articulos.Should().OnlyContain(f => !f.EstaSeleccionado);
        escenario.Sut.SeleccionarTodos.Should().BeFalse();
        escenario.Sut.TieneArticulosSeleccionados.Should().BeFalse();
    }

    [Fact]
    public async Task PedirArticuloCommand_FiltradoPorDistribuidor_LoPasaComoProveedorPreferido()
    {
        // Track 4.3, K6: filtrar por un distribuidor es elegir a quién reponer.
        using var escenario = await CrearEscenarioPedidoAsync();
        ConfigurarPedidoConfirmado(escenario);
        var fila = escenario.Sut.Articulos[0];
        escenario.Sut.IdDistribuidorFiltro = 5;

        await escenario.Sut.PedirArticuloCommand.ExecuteAsync(fila);

        await escenario.PedidoService.Received(1).ObtenerVistaPreviaAsync(
            Arg.Any<IReadOnlyCollection<int>>(),
            5,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PedirArticuloCommand_SinPrincipalConFuentes_PideLaFuenteMasBarataDeLaConsulta()
    {
        // Track 4.3, L4, paso 3c: la consulta trae la más barata (26); el modal la preselecciona (K2)
        var sinPrincipal = _articulosEjemplo[1] with { IdCatalogoProveedor = null, IdFuentePedido = 26, CantidadFuentes = 2 };
        _inventarioService.ListarArticulosPaginadosAsync(Arg.Any<ConsultaArticulosDto>(), Arg.Any<CancellationToken>())
            .Returns(new ArticulosPaginadosDto { Items = [sinPrincipal], TotalRegistros = 1, TotalArticulos = 1, PaginaActual = 1, TamanoPagina = 10 });
        using var escenario = new EscenarioPedido(_inventarioService, _dialogService);
        await escenario.Sut.CargarArticulosAsync();
        ConfigurarPedidoConfirmado(escenario);

        await escenario.Sut.PedirArticuloCommand.ExecuteAsync(escenario.Sut.Articulos[0]);

        await escenario.PedidoService.Received(1).ObtenerVistaPreviaAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(26)),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PedirArticuloCommand_SinFiltroDeDistribuidor_NoPasaProveedorPreferido()
    {
        using var escenario = await CrearEscenarioPedidoAsync();
        ConfigurarPedidoConfirmado(escenario);

        await escenario.Sut.PedirArticuloCommand.ExecuteAsync(escenario.Sut.Articulos[0]);

        await escenario.PedidoService.Received(1).ObtenerVistaPreviaAsync(
            Arg.Any<IReadOnlyCollection<int>>(),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PedirArticuloCommand_ArticuloVinculado_PideSuItemDeCatalogoSinOfrecerIncorporar()
    {
        using var escenario = await CrearEscenarioPedidoAsync();
        ConfigurarPedidoConfirmado(escenario);

        await escenario.Sut.PedirArticuloCommand.ExecuteAsync(escenario.Sut.Articulos[0]);

        await escenario.PedidoService.Received(1).ObtenerVistaPreviaAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Single() == 40),
            Arg.Any<int?>(), Arg.Any<CancellationToken>());
        escenario.PedidoDialogService.Received(1).MostrarPedirArticulos(
            Arg.Any<VistaPreviaPedidoDto>(),
            Arg.Is<IReadOnlyList<string>>(avisos => avisos.Count == 0),
            false);
        await escenario.PedidoService.Received(1).AgregarVariosAlPedidoAsync(
            Arg.Is<AgregarVariosAlPedidoDto>(d => d.IdUsuario == 7 && d.Renglones.Single().Cantidad == 2),
            Arg.Any<CancellationToken>());
        _dialogService.Received(1).MostrarInformacion("Pedido a proveedor", "Se agregó al borrador #12 (Distribuidora Papelera).");
    }

    [Fact]
    public async Task PedirSeleccionadosCommand_ConSinProveedorYServicio_LosExcluyeYAvisaConSuMotivo()
    {
        using var escenario = await CrearEscenarioPedidoAsync();
        ConfigurarPedidoConfirmado(escenario);
        escenario.Sut.SeleccionarTodos = true;

        await escenario.Sut.PedirSeleccionadosCommand.ExecuteAsync(null);

        await escenario.PedidoService.Received(1).ObtenerVistaPreviaAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Single() == 40),
            Arg.Any<int?>(), Arg.Any<CancellationToken>());
        escenario.PedidoDialogService.Received(1).MostrarPedirArticulos(
            Arg.Any<VistaPreviaPedidoDto>(),
            Arg.Is<IReadOnlyList<string>>(avisos => avisos.SequenceEqual(AvisosEsperadosDeExcluidos)),
            false);
        escenario.Sut.Articulos.Should().OnlyContain(f => !f.EstaSeleccionado, "lo pedido deja de estar seleccionado");
    }

    [Fact]
    public async Task PedirSeleccionadosCommand_SoloArticulosQueNoSePuedenPedir_AvisaSinConsultarNiAbrirElModal()
    {
        using var escenario = await CrearEscenarioPedidoAsync();
        escenario.Sut.Articulos[1].EstaSeleccionado = true;
        escenario.Sut.Articulos[2].EstaSeleccionado = true;

        await escenario.Sut.PedirSeleccionadosCommand.ExecuteAsync(null);

        escenario.PedidoDialogService.Received(1).MostrarAlerta("No hay nada para pedir", Arg.Is<string>(m => m.Contains("Bolígrafo Bic Cristal: no tiene fuentes")));
        await escenario.PedidoService.DidNotReceiveWithAnyArgs().ObtenerVistaPreviaAsync(default!, default);
        escenario.PedidoDialogService.DidNotReceiveWithAnyArgs().MostrarPedirArticulos(default!, default!, default);
    }

    [Fact]
    public async Task PedirArticuloCommand_ServicioRechazaElLote_MuestraElError()
    {
        using var escenario = await CrearEscenarioPedidoAsync();
        ConfigurarPedidoConfirmado(escenario);
        escenario.PedidoService.AgregarVariosAlPedidoAsync(Arg.Any<AgregarVariosAlPedidoDto>(), Arg.Any<CancellationToken>())
            .Returns<ResultadoAgregarAlPedidoDto>(_ => throw new DomainException("Otra terminal modificó el borrador."));

        await escenario.Sut.PedirArticuloCommand.ExecuteAsync(escenario.Sut.Articulos[0]);

        _dialogService.Received(1).MostrarError("No se pudo pedir", "Otra terminal modificó el borrador.");
        _dialogService.DidNotReceiveWithAnyArgs().MostrarInformacion(default!, default!);
    }

    [Fact]
    public async Task PedirArticuloCommand_Cajero_NoPuedePedirNiConsulta()
    {
        using var escenario = await CrearEscenarioPedidoAsync(esEncargado: false);

        await escenario.Sut.PedirArticuloCommand.ExecuteAsync(escenario.Sut.Articulos[0]);

        escenario.Sut.PuedePedir.Should().BeFalse();
        await escenario.PedidoService.DidNotReceiveWithAnyArgs().ObtenerVistaPreviaAsync(default!, default);
    }

    [Fact]
    public void PuedePedir_SinFlujoDePedidos_EsFalso()
    {
        _sut.PuedePedir.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // Filtro por rubro (RF-04)                                           //
    // ------------------------------------------------------------------ //

    /// <summary>Escolar (Cuadernos, Lápices) y Oficina (Resmas).</summary>
    private async Task<ArticulosViewModel> CrearSutConRubrosAsync()
    {
        var rubroService = Substitute.For<IRubroService>();
        rubroService.ListarAsync(null, Arg.Any<CancellationToken>()).Returns(
        [
            new RubroDto { IdRubro = 1, NombreRubro = "Escolar" },
            new RubroDto { IdRubro = 2, NombreRubro = "Oficina" }
        ]);
        _inventarioService.ListarCategoriasAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new CategoriaDto { IdCategoria = 10, NombreCategoria = "Cuadernos", IdRubro = 1 },
            new CategoriaDto { IdCategoria = 11, NombreCategoria = "Lápices", IdRubro = 1 },
            new CategoriaDto { IdCategoria = 20, NombreCategoria = "Resmas", IdRubro = 2 }
        ]);

        var sut = new ArticulosViewModel(_inventarioService, _dialogService, _proveedorService, rubroService: rubroService);
        await sut.CargarArticulosAsync();
        return sut;
    }

    [Fact]
    public async Task CargarArticulosAsync_ConRubros_OfreceTodosLosRubrosYTodasLasCategorias()
    {
        using var sut = await CrearSutConRubrosAsync();

        sut.RubrosFiltro.Select(r => r.NombreRubro).Should().Equal("Todos los Rubros", "Escolar", "Oficina");
        sut.CategoriasFiltro.Select(c => c.NombreCategoria).Should().Equal("Todas las Categorías", "Cuadernos", "Lápices", "Resmas");
    }

    [Fact]
    public async Task IdRubroFiltro_ElegirRubro_AcotaLasCategoriasReiniciaLaCategoriaYConsultaPorRubro()
    {
        using var sut = await CrearSutConRubrosAsync();
        sut.IdCategoriaFiltro = 20;
        await sut.CargarArticulosAsync();
        _inventarioService.ClearReceivedCalls();

        sut.IdRubroFiltro = 1;
        await sut.CargarArticulosAsync();

        sut.CategoriasFiltro.Select(c => c.NombreCategoria).Should().Equal("Todas las Categorías", "Cuadernos", "Lápices");
        sut.IdCategoriaFiltro.Should().Be(0, "\"Todas las Categorías\" pasa a significar todas las del rubro");
        sut.HayFiltrosActivos.Should().BeTrue();
        await _inventarioService.Received().ListarArticulosPaginadosAsync(
            Arg.Is<ConsultaArticulosDto>(c => c.IdRubro == 1 && c.IdCategoria == null),
            Arg.Any<CancellationToken>());
        await _inventarioService.DidNotReceive().ListarArticulosPaginadosAsync(
            Arg.Is<ConsultaArticulosDto>(c => c.IdRubro == 1 && c.IdCategoria == 20),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LimpiarFiltros_ConRubroElegido_VuelveATodosLosRubrosYTodasLasCategorias()
    {
        using var sut = await CrearSutConRubrosAsync();
        sut.IdRubroFiltro = 2;

        sut.LimpiarFiltros();

        sut.IdRubroFiltro.Should().Be(0);
        sut.CategoriasFiltro.Should().HaveCount(4);
        sut.HayFiltrosActivos.Should().BeFalse();
    }
}
