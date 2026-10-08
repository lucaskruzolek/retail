using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Articulos;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class InventarioServiceTests
{
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IRepository<Categoria> _categoriaRepository;
    private readonly IRepository<Marca> _marcaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CrearArticuloValidator _crearValidator;
    private readonly ActualizarArticuloValidator _actualizarValidator;
    private readonly InventarioService _sut;

    public InventarioServiceTests()
    {
        _articuloRepository = Substitute.For<IRepository<Articulo>>();
        _categoriaRepository = Substitute.For<IRepository<Categoria>>();
        _marcaRepository = Substitute.For<IRepository<Marca>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _crearValidator = new CrearArticuloValidator();
        _actualizarValidator = new ActualizarArticuloValidator();

        _sut = new InventarioService(
            _articuloRepository,
            _categoriaRepository,
            _marcaRepository,
            _unitOfWork,
            _crearValidator,
            _actualizarValidator,
            new CrearPresentacionValidator(),
            new FraccionarValidator());
    }

    [Fact]
    public async Task ListarArticulosAsync_DebeRetornarArticulosConNombresDeCategoriaYMarca()
    {
        // Arrange
        var categorias = new List<Categoria>
        {
            new() { Id = 1, NombreCategoria = "Escolar" }
        };
        var marcas = new List<Marca>
        {
            new() { Id = 2, NombreMarca = "Rivadavia" }
        };
        var articulos = new List<Articulo>
        {
            new()
            {
                Id = 10,
                Descripcion = "Cuaderno 50 Hojas",
                IdCategoria = 1,
                IdMarca = 2,
                CostoReposicion = 1000m,
                PorcentajeGanancia = 50m,
                PrecioVenta = 1500m,
                StockActual = 8,
                StockMinimo = 5,
                EsServicio = false
            }
        };

        _articuloRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(articulos);
        _categoriaRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(categorias);
        _marcaRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(marcas);

        // Act
        var resultado = await _sut.ListarArticulosAsync();

        // Assert
        resultado.Should().HaveCount(1);
        resultado[0].Descripcion.Should().Be("Cuaderno 50 Hojas");
        resultado[0].CategoriaNombre.Should().Be("Escolar");
        resultado[0].MarcaNombre.Should().Be("Rivadavia");
        resultado[0].PrecioVenta.Should().Be(1500m);
        resultado[0].StockBajo.Should().BeFalse();
    }

    [Fact]
    public async Task CrearArticuloAsync_ConDatosValidos_DebeCrearYPersistir()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            CodigoBarras = "7791234567890",
            Descripcion = "Bolígrafo Azul",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 200m,
            PorcentajeGanancia = 50m,
            StockActual = 50,
            StockMinimo = 10,
            EsServicio = false
        };

        _articuloRepository.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _categoriaRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Categoria { Id = 1, NombreCategoria = "Librería" });
        _marcaRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Marca { Id = 1, NombreMarca = "Bic" });

        // Act
        var resultado = await _sut.CrearArticuloAsync(dto);

        // Assert
        resultado.Should().NotBeNull();
        resultado.Descripcion.Should().Be("Bolígrafo Azul");
        resultado.PrecioVenta.Should().Be(300m);
        resultado.CategoriaNombre.Should().Be("Librería");
        resultado.MarcaNombre.Should().Be("Bic");

        await _articuloRepository.Received(1).AddAsync(Arg.Is<Articulo>(a =>
            a.Descripcion == "Bolígrafo Azul" &&
            a.PrecioVenta == 300m), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearArticuloAsync_ConCodigoBarrasDuplicado_DebeLanzarDomainException()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            CodigoBarras = "7791234567890",
            Descripcion = "Duplicado",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 100m,
            PorcentajeGanancia = 20m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        _articuloRepository.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([new Articulo { Id = 5, CodigoBarras = "7791234567890", Descripcion = "Existente" }]);

        // Act
        var act = () => _sut.CrearArticuloAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Ya existe un artículo activo con el código de barras*");
        await _articuloRepository.DidNotReceive().AddAsync(Arg.Any<Articulo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearArticuloAsync_ConCodigoBarrasNulo_DebeCrearExitosamente()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            CodigoBarras = null,
            Descripcion = "Vasija de Cerámica Hecha a Mano",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 800m,
            PorcentajeGanancia = 50m,
            StockActual = 3,
            StockMinimo = 1,
            EsServicio = false
        };

        // Act
        var resultado = await _sut.CrearArticuloAsync(dto);

        // Assert
        resultado.Should().NotBeNull();
        resultado.CodigoBarras.Should().BeNull();
        resultado.PrecioVenta.Should().Be(1200m);

        await _articuloRepository.Received(1).AddAsync(Arg.Is<Articulo>(a => a.CodigoBarras == null), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearArticuloAsync_ConCategoriaYMarcaNulas_DebeCrearConValoresNulosYTextosPorDefecto()
    {
        // Arrange
        var dto = new CrearArticuloDto
        {
            CodigoBarras = null,
            Descripcion = "Servicio de Plastificado",
            IdCategoria = null,
            IdMarca = null,
            CostoReposicion = 100m,
            PorcentajeGanancia = 100m,
            StockActual = 0,
            StockMinimo = 0,
            EsServicio = true
        };

        // Act
        var resultado = await _sut.CrearArticuloAsync(dto);

        // Assert
        resultado.Should().NotBeNull();
        resultado.IdCategoria.Should().BeNull();
        resultado.CategoriaNombre.Should().Be("Sin categoría");
        resultado.IdMarca.Should().BeNull();
        resultado.MarcaNombre.Should().Be("Sin marca");

        await _articuloRepository.Received(1).AddAsync(
            Arg.Is<Articulo>(a => a.IdCategoria == null && a.IdMarca == null),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarArticuloAsync_ConDatosValidos_DebeActualizarYPersistir()
    {
        // Arrange
        var articuloExistente = new Articulo
        {
            Id = 1,
            Descripcion = "Antiguo",
            IdCategoria = 1,
            IdMarca = 1,
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m,
            StockActual = 10,
            StockMinimo = 2,
            EsServicio = false
        };

        var dto = new ActualizarArticuloDto
        {
            IdArticulo = 1,
            CodigoBarras = "7799999999999",
            Descripcion = "Actualizado",
            IdCategoria = 2,
            IdMarca = 3,
            CostoReposicion = 200m,
            PorcentajeGanancia = 30m,
            StockActual = 15,
            StockMinimo = 4,
            EsServicio = false
        };

        _articuloRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(articuloExistente);
        _articuloRepository.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var resultado = await _sut.ActualizarArticuloAsync(dto);

        // Assert
        resultado.Descripcion.Should().Be("Actualizado");
        resultado.PrecioVenta.Should().Be(260m);
        articuloExistente.Descripcion.Should().Be("Actualizado");
        articuloExistente.PrecioVenta.Should().Be(260m);

        await _articuloRepository.Received(1).UpdateAsync(articuloExistente, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaArticuloAsync_ArticuloExistente_DebeMarcarComoEliminadoYPersistir()
    {
        // Arrange
        var articulo = new Articulo
        {
            Id = 1,
            Descripcion = "Articulo para baja"
        };

        _articuloRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(articulo);

        // Act
        await _sut.BajaArticuloAsync(1);

        // Assert
        articulo.IsDeleted.Should().BeTrue();
        await _articuloRepository.Received(1).UpdateAsync(articulo, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObtenerAlertasStockMinimoAsync_DebeFiltrarSoloArticulosCriticos()
    {
        // Arrange
        var articulosCriticos = new List<Articulo>
        {
            new() { Id = 1, Descripcion = "Lápiz Negro", StockActual = 2, StockMinimo = 5, EsServicio = false }
        };

        _articuloRepository.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(articulosCriticos);

        // Act
        var resultado = await _sut.ObtenerAlertasStockMinimoAsync();

        // Assert
        resultado.Should().HaveCount(1);
        resultado[0].Descripcion.Should().Be("Lápiz Negro");
        resultado[0].Deficit.Should().Be(3);
    }

    [Fact]
    public async Task ActualizarCostoYPrecioAsync_DebeRecalcularPrecioYPersistir()
    {
        // Arrange
        var articulo = new Articulo
        {
            Id = 1,
            Descripcion = "Resma A4",
            CostoReposicion = 4000m,
            PorcentajeGanancia = 25m,
            PrecioVenta = 5000m
        };

        _articuloRepository.GetByIdAsync(1, includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(articulo);

        // Act
        await _sut.ActualizarCostoYPrecioAsync(1, 5000m);

        // Assert
        articulo.CostoReposicion.Should().Be(5000m);
        articulo.PrecioVenta.Should().Be(6250m);
        await _articuloRepository.Received(1).UpdateAsync(articulo, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarCategoriasAsync_DebeRetornarCategoriasOrdenadas()
    {
        // Arrange
        var categorias = new List<Categoria>
        {
            new() { Id = 2, NombreCategoria = "Oficina" },
            new() { Id = 1, NombreCategoria = "Artística" }
        };

        _categoriaRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(categorias);

        // Act
        var resultado = await _sut.ListarCategoriasAsync();

        // Assert
        resultado.Should().HaveCount(2);
        resultado[0].NombreCategoria.Should().Be("Artística");
        resultado[1].NombreCategoria.Should().Be("Oficina");
    }

    [Fact]
    public async Task ListarMarcasAsync_DebeRetornarMarcasOrdenadas()
    {
        // Arrange
        var marcas = new List<Marca>
        {
            new() { Id = 2, NombreMarca = "Pelikan" },
            new() { Id = 1, NombreMarca = "Faber-Castell" }
        };

        _marcaRepository.ListAllAsync(includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(marcas);

        // Act
        var resultado = await _sut.ListarMarcasAsync();

        // Assert
        resultado.Should().HaveCount(2);
        resultado[0].NombreMarca.Should().Be("Faber-Castell");
        resultado[1].NombreMarca.Should().Be("Pelikan");
    }

    // ---------- Presentaciones derivadas y fraccionamiento (RF-21) ----------

    [Fact]
    public async Task CrearPresentacionAsync_OrigenValido_CreaDerivadoConCostoDerivadoYDatosHeredados()
    {
        // Arrange
        var origen = CrearOrigen();
        PrepararTienda(origen);
        Articulo? agregado = null;
        await _articuloRepository.AddAsync(Arg.Do<Articulo>(a => agregado = a), Arg.Any<CancellationToken>());

        var dto = new CrearPresentacionDto
        {
            IdArticuloOrigen = 1,
            UnidadesPorOrigen = 100,
            Descripcion = "Sobre manila (unidad)",
            CodigoBarras = "SOBRE-U",
            PorcentajeGanancia = 100m,
            StockMinimo = 20
        };

        // Act
        var resultado = await _sut.CrearPresentacionAsync(dto);

        // Assert
        agregado.Should().NotBeNull();
        agregado!.IdArticuloOrigen.Should().Be(1);
        agregado.UnidadesPorOrigen.Should().Be(100);
        agregado.CostoReposicion.Should().Be(10m);
        agregado.PrecioVenta.Should().Be(20m);
        agregado.StockActual.Should().Be(0);
        agregado.StockMinimo.Should().Be(20);
        agregado.IdCategoria.Should().Be(3);
        agregado.IdMarca.Should().Be(4);
        resultado.EsDerivado.Should().BeTrue();
        resultado.UnidadesPorOrigen.Should().Be(100);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearPresentacionAsync_OrigenInexistente_LanzaDomainException()
    {
        // Arrange
        PrepararTienda();

        // Act
        var act = () => _sut.CrearPresentacionAsync(PresentacionDe(idOrigen: 99));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*99*");
        await _articuloRepository.DidNotReceive().AddAsync(Arg.Any<Articulo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearPresentacionAsync_OrigenEsDerivado_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        var origen = CrearOrigen();
        var derivado = CrearDerivado(origen);
        PrepararTienda(origen, derivado);

        // Act
        var act = () => _sut.CrearPresentacionAsync(PresentacionDe(idOrigen: derivado.Id));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*un solo nivel*");
        await _articuloRepository.DidNotReceive().AddAsync(Arg.Any<Articulo>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearPresentacionAsync_CodigoBarrasDuplicado_LanzaDomainException()
    {
        // Arrange
        var otro = new Articulo { Id = 7, Descripcion = "Otro", CodigoBarras = "7790001" };
        PrepararTienda(CrearOrigen(), otro);

        // Act
        var act = () => _sut.CrearPresentacionAsync(PresentacionDe(idOrigen: 1) with { CodigoBarras = "7790001" });

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*7790001*");
        await _articuloRepository.DidNotReceive().AddAsync(Arg.Any<Articulo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FraccionarAsync_StockSuficiente_ActualizaAmbosStocksConUnSoloSaveChanges()
    {
        // Arrange
        var origen = CrearOrigen(stock: 5);
        var derivado = CrearDerivado(origen, stock: 3);
        PrepararTienda(origen, derivado);

        // Act
        var unidades = await _sut.FraccionarAsync(new FraccionarDto { IdArticuloDerivado = 2, CantidadOrigen = 2 });

        // Assert
        unidades.Should().Be(200);
        origen.StockActual.Should().Be(3);
        derivado.StockActual.Should().Be(203);
        await _articuloRepository.Received(1).UpdateAsync(origen, Arg.Any<CancellationToken>());
        await _articuloRepository.Received(1).UpdateAsync(derivado, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FraccionarAsync_StockInsuficiente_NoGuarda()
    {
        // Arrange
        var origen = CrearOrigen(stock: 1);
        var derivado = CrearDerivado(origen, stock: 3);
        PrepararTienda(origen, derivado);

        // Act
        var act = () => _sut.FraccionarAsync(new FraccionarDto { IdArticuloDerivado = 2, CantidadOrigen = 2 });

        // Assert
        await act.Should().ThrowAsync<StockInsuficienteException>();
        origen.StockActual.Should().Be(1);
        derivado.StockActual.Should().Be(3);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FraccionarAsync_ArticuloNoDerivado_LanzaDomainException()
    {
        // Arrange
        PrepararTienda(CrearOrigen());

        // Act
        var act = () => _sut.FraccionarAsync(new FraccionarDto { IdArticuloDerivado = 1, CantidadOrigen = 1 });

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*no es una presentación derivada*");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FraccionarAsync_OrigenDadoDeBaja_LanzaDomainException()
    {
        // Arrange
        var origen = CrearOrigen();
        var derivado = CrearDerivado(origen);
        origen.MarkAsDeleted();
        PrepararTienda(origen, derivado);

        // Act
        var act = () => _sut.FraccionarAsync(new FraccionarDto { IdArticuloDerivado = 2, CantidadOrigen = 1 });

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*dado de baja*");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FraccionarAsync_CantidadMenorAUno_LanzaValidationException()
    {
        // Act
        var act = () => _sut.FraccionarAsync(new FraccionarDto { IdArticuloDerivado = 2, CantidadOrigen = 0 });

        // Assert
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task BajaArticuloAsync_OrigenConPresentacionesActivas_LanzaDomainExceptionSinGuardar()
    {
        // Arrange
        var origen = CrearOrigen();
        PrepararTienda(origen, CrearDerivado(origen));

        // Act
        var act = () => _sut.BajaArticuloAsync(1);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*Sobre manila (unidad)*");
        origen.IsDeleted.Should().BeFalse();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaArticuloAsync_PresentacionDerivada_LaDaDeBaja()
    {
        // Arrange
        var origen = CrearOrigen();
        var derivado = CrearDerivado(origen);
        PrepararTienda(origen, derivado);

        // Act
        await _sut.BajaArticuloAsync(2);

        // Assert
        derivado.IsDeleted.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarArticuloAsync_CambiaCostoDelOrigen_PropagaASusPresentaciones()
    {
        // Arrange
        var origen = CrearOrigen();
        var derivado = CrearDerivado(origen);
        PrepararTienda(origen, derivado);

        // Act
        await _sut.ActualizarArticuloAsync(EdicionDe(origen, costo: 2000m, esServicio: false));

        // Assert
        derivado.CostoReposicion.Should().Be(20m);
        derivado.PrecioVenta.Should().Be(30m);
        await _articuloRepository.Received(1).UpdateAsync(derivado, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarArticuloAsync_OrigenConPresentacionesPasaAServicio_LanzaDomainException()
    {
        // Arrange
        var origen = CrearOrigen();
        PrepararTienda(origen, CrearDerivado(origen));

        // Act
        var act = () => _sut.ActualizarArticuloAsync(EdicionDe(origen, costo: 1000m, esServicio: true));

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*servicio*");
        origen.EsServicio.Should().BeFalse();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarCostoYPrecioAsync_OrigenConPresentaciones_PropagaElCosto()
    {
        // Arrange
        var origen = CrearOrigen();
        var derivado = CrearDerivado(origen);
        PrepararTienda(origen, derivado);

        // Act
        await _sut.ActualizarCostoYPrecioAsync(1, 4500m);

        // Assert
        derivado.CostoReposicion.Should().Be(45m);
        derivado.PrecioVenta.Should().Be(67.5m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Simula la tabla de artículos en memoria: <c>GetByIdAsync</c> ignora los dados de baja y <c>FindAsync</c>
    /// evalúa el predicado recibido, así el test verifica la condición del filtro (no su traducción a SQL).
    /// </summary>
    private void PrepararTienda(params Articulo[] articulos)
    {
        _articuloRepository.GetByIdAsync(Arg.Any<int>(), false, Arg.Any<CancellationToken>())
            .Returns(llamada => articulos.FirstOrDefault(a => a.Id == llamada.ArgAt<int>(0) && !a.IsDeleted));

        _articuloRepository.FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(llamada => articulos
                .Where(a => !a.IsDeleted)
                .Where(llamada.Arg<Expression<Func<Articulo, bool>>>().Compile())
                .ToList());
    }

    private static Articulo CrearOrigen(int stock = 5)
    {
        return new Articulo
        {
            Id = 1,
            Descripcion = "Sobre manila (pack x100)",
            IdCategoria = 3,
            IdMarca = 4,
            CostoReposicion = 1000m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 1400m,
            StockActual = stock,
            StockMinimo = 1
        };
    }

    private static Articulo CrearDerivado(Articulo origen, int stock = 0)
    {
        var derivado = new Articulo
        {
            Id = 2,
            Descripcion = "Sobre manila (unidad)",
            PorcentajeGanancia = 50m,
            StockActual = stock
        };
        derivado.DefinirComoPresentacionDe(origen, 100);
        return derivado;
    }

    private static CrearPresentacionDto PresentacionDe(int idOrigen)
    {
        return new CrearPresentacionDto
        {
            IdArticuloOrigen = idOrigen,
            UnidadesPorOrigen = 10,
            Descripcion = "Presentación",
            PorcentajeGanancia = 50m,
            StockMinimo = 0
        };
    }

    private static ActualizarArticuloDto EdicionDe(Articulo articulo, decimal costo, bool esServicio)
    {
        return new ActualizarArticuloDto
        {
            IdArticulo = articulo.Id,
            Descripcion = articulo.Descripcion,
            IdCategoria = articulo.IdCategoria,
            IdMarca = articulo.IdMarca,
            CostoReposicion = costo,
            PorcentajeGanancia = articulo.PorcentajeGanancia,
            StockActual = articulo.StockActual,
            StockMinimo = articulo.StockMinimo,
            EsServicio = esServicio
        };
    }
}
