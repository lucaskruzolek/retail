using FluentAssertions;
using NSubstitute;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.UnitTests.TestData;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

/// <summary>
/// Clasificación pasiva del catálogo (Track 6.6, Fase 8; M-72 a M-77). Las reglas ya están cubiertas por
/// <see cref="ClasificacionServiceTests"/>: acá se verifica lo que agrega este servicio, que es qué ítems procesa,
/// qué guarda en ellos y cómo cierra la unidad de trabajo.
/// </summary>
public class ClasificacionCatalogoServiceTests
{
    private const int IdProveedor = 1;
    private const int Cuadernos = 10;
    private const int Juegos = 12;
    private const int Rivadavia = 21;

    private readonly IRepository<Proveedor> _proveedores = Substitute.For<IRepository<Proveedor>>();
    private readonly ICatalogoProveedorQueryService _catalogo = Substitute.For<ICatalogoProveedorQueryService>();
    private readonly IDiccionariosClasificacionQueryService _diccionarios = Substitute.For<IDiccionariosClasificacionQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ClasificacionCatalogoService _servicio;

    private readonly Proveedor _proveedor = ProveedoresDePrueba.ConId(IdProveedor, Proveedor.Crear("Distribuidora Mayorista", "30712345671"));
    private readonly List<CatalogoProveedor> _items = new();
    private readonly List<TerminoDiccionarioDto> _terminos = new();
    private readonly List<ParDiccionarioDto> _pares = new();
    private readonly List<EquivalenciaDiccionarioDto> _equivalencias = new();
    private readonly List<ValorDelProveedorDto> _valores = new();
    private readonly List<CategoriaDto> _categorias = new()
    {
        new CategoriaDto { IdCategoria = Cuadernos, NombreCategoria = "Cuadernos" },
        new CategoriaDto { IdCategoria = Juegos, NombreCategoria = "Juegos" }
    };

    private readonly List<MarcaDiccionarioDto> _marcas = new()
    {
        new MarcaDiccionarioDto { IdMarca = Rivadavia, NombreMarca = "Rivadavia", Alias = Array.Empty<string>() }
    };

    public ClasificacionCatalogoServiceTests()
    {
        _proveedores.GetByIdAsync(IdProveedor, false, Arg.Any<CancellationToken>()).Returns(_proveedor);
        _catalogo.ObtenerParaClasificarAsync(IdProveedor, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(llamada => llamada.ArgAt<bool>(1) ? _items.Where(i => i.ClasificacionPendiente).ToList() : _items.ToList());
        _catalogo.ObtenerValoresDeClasificacionAsync(IdProveedor, Arg.Any<CancellationToken>())
            .Returns(_ => _valores.ToList());
        _diccionarios.ObtenerParaLoteAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new DiccionariosClasificacionDto
            {
                Terminos = _terminos,
                Pares = _pares,
                Marcas = _marcas,
                Prefijos = Array.Empty<PrefijoDiccionarioDto>(),
                Equivalencias = _equivalencias,
                Categorias = _categorias
            });

        _servicio = new ClasificacionCatalogoService(_proveedores, _catalogo, _diccionarios, _unitOfWork);
    }

    // ---------- ClasificarAsync ----------

    [Fact]
    public async Task ClasificarAsync_ProveedorInexistente_LanzaDomainException()
    {
        _proveedores.GetByIdAsync(99, false, Arg.Any<CancellationToken>()).Returns((Proveedor?)null);

        var accion = () => _servicio.ClasificarAsync(99, soloPendientes: true);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("*99*");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClasificarAsync_SoloPendientes_LePideAlPuertoSoloLosPendientesYNoTocaLosClasificados()
    {
        var pendiente = AgregarItem(1, "CUADERNO RAYADO");
        var clasificado = AgregarItem(2, "CUADERNO LISO");
        clasificado.RegistrarClasificacionSugerida(Juegos, null);
        _terminos.Add(Termino("CUADERNO", Cuadernos));

        var resultado = await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        await _catalogo.Received(1).ObtenerParaClasificarAsync(IdProveedor, true, Arg.Any<CancellationToken>());
        pendiente.IdCategoriaSugerida.Should().Be(Cuadernos);
        clasificado.IdCategoriaSugerida.Should().Be(Juegos);
        resultado.ItemsClasificados.Should().Be(1);
    }

    [Fact]
    public async Task ClasificarAsync_TodosLosItems_ReclasificaConLasReglasVigentes()
    {
        var clasificado = AgregarItem(1, "CUADERNO LISO");
        clasificado.RegistrarClasificacionSugerida(Juegos, null);
        _terminos.Add(Termino("CUADERNO", Cuadernos));

        await _servicio.ClasificarAsync(IdProveedor, soloPendientes: false);

        await _catalogo.Received(1).ObtenerParaClasificarAsync(IdProveedor, false, Arg.Any<CancellationToken>());
        clasificado.IdCategoriaSugerida.Should().Be(Cuadernos);
    }

    [Fact]
    public async Task ClasificarAsync_TerminoConocido_RegistraLaCategoriaYLaFecha()
    {
        var item = AgregarItem(1, "CUADERNO RAYADO");
        _terminos.Add(Termino("CUADERNO", Cuadernos));

        var resultado = await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        item.IdCategoriaSugerida.Should().Be(Cuadernos);
        item.ClasificacionPendiente.Should().BeFalse();
        resultado.Should().BeEquivalentTo(new { ItemsClasificados = 1, ConCategoria = 1, ConMarca = 0, SinCategoria = 0 });
    }

    [Fact]
    public async Task ClasificarAsync_TerminoDesconocido_QuedaFechadoSinCategoriaYNoLanza()
    {
        var item = AgregarItem(1, "COSA RARA");

        var resultado = await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        item.IdCategoriaSugerida.Should().BeNull();
        item.ClasificacionPendiente.Should().BeFalse();
        resultado.Should().BeEquivalentTo(new { ItemsClasificados = 1, ConCategoria = 0, SinCategoria = 1 });
    }

    [Fact]
    public async Task ClasificarAsync_EquivalenciaDelProveedor_PrecedeAlTermino()
    {
        var item = AgregarItem(1, "CUADERNO ESPIRAL", rubro: "Escolar", categoria: "Cuadernos T/D");
        _terminos.Add(Termino("CUADERNO", Juegos));
        _equivalencias.Add(new EquivalenciaDiccionarioDto
        {
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = "ESCOLAR",
            Texto = "CUADERNOS T/D",
            IdCategoria = Cuadernos
        });

        await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        item.IdCategoriaSugerida.Should().Be(Cuadernos);
    }

    [Fact]
    public async Task ClasificarAsync_TerminoAmbiguoConPar_UsaLaCategoriaDelPar()
    {
        var item = AgregarItem(1, "CUADERNO RAYADO");
        _terminos.Add(Termino("CUADERNO", null));
        _pares.Add(new ParDiccionarioDto { Termino = "CUADERNO", SegundaPalabra = "RAYADO", IdCategoria = Juegos });

        await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        item.IdCategoriaSugerida.Should().Be(Juegos);
    }

    [Fact]
    public async Task ClasificarAsync_MarcaEnLaDescripcion_RegistraLaMarca()
    {
        var item = AgregarItem(1, "CUADERNO RIVADAVIA RAYADO");

        var resultado = await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        item.IdMarcaSugerida.Should().Be(Rivadavia);
        resultado.ConMarca.Should().Be(1);
    }

    [Fact]
    public async Task ClasificarAsync_VariosItems_ConsultaLosDiccionariosUnaVezYGuardaUnaVez()
    {
        AgregarItem(1, "CUADERNO A");
        AgregarItem(2, "MOCHILA B");
        AgregarItem(3, "CUADERNO C");

        await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        await _diccionarios.Received(1).ObtenerParaLoteAsync(
            IdProveedor,
            Arg.Is<IReadOnlyCollection<string>>(t => t.Count == 2 && t.Contains("CUADERNO") && t.Contains("MOCHILA")),
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClasificarAsync_SinItemsPendientes_NoConsultaLosDiccionariosYGuardaIgual()
    {
        var resultado = await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        resultado.ItemsClasificados.Should().Be(0);
        await _diccionarios.DidNotReceiveWithAnyArgs().ObtenerParaLoteAsync(default, default!, default!, default);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClasificarAsync_SoloPendientesFalso_ActivaLaPreferenciaDelProveedor()
    {
        _proveedor.ClasificaCatalogo.Should().BeFalse();

        await _servicio.ClasificarAsync(IdProveedor, soloPendientes: false);

        _proveedor.ClasificaCatalogo.Should().BeTrue();
        await _proveedores.Received(1).UpdateAsync(_proveedor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClasificarAsync_SoloPendientes_NoCambiaLaPreferenciaDelProveedor()
    {
        await _servicio.ClasificarAsync(IdProveedor, soloPendientes: true);

        _proveedor.ClasificaCatalogo.Should().BeFalse();
        await _proveedores.DidNotReceive().UpdateAsync(Arg.Any<Proveedor>(), Arg.Any<CancellationToken>());
    }

    // ---------- ObtenerValoresSinTraducirAsync ----------

    [Fact]
    public async Task ObtenerValoresSinTraducirAsync_ValorConEquivalencia_NoSeOfrece()
    {
        _valores.Add(ValorCategoria("Escolar", "Cuadernos T/D", 4));
        _valores.Add(ValorCategoria("Escolar", "Mochilas", 2));
        _equivalencias.Add(new EquivalenciaDiccionarioDto
        {
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = "ESCOLAR",
            Texto = "CUADERNOS T/D",
            IdCategoria = Cuadernos
        });

        var resultado = await _servicio.ObtenerValoresSinTraducirAsync(IdProveedor);

        resultado.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "ESCOLAR", Texto = "MOCHILAS", CantidadDeItems = 2 });
    }

    [Fact]
    public async Task ObtenerValoresSinTraducirAsync_MarcaQueCoincideConUnaDelComercio_NoSeOfrece()
    {
        _valores.Add(new ValorDelProveedorDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "Rivadavia", CantidadDeItems = 3 });
        _valores.Add(new ValorDelProveedorDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "Marca Desconocida", CantidadDeItems = 5 });

        var resultado = await _servicio.ObtenerValoresSinTraducirAsync(IdProveedor);

        resultado.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Tipo = TipoEquivalenciaEnum.Marca, Texto = "MARCA DESCONOCIDA", CantidadDeItems = 5 });
    }

    [Fact]
    public async Task ObtenerValoresSinTraducirAsync_SoloRubro_UsaElRubroComoClaveYConservaLaCantidad()
    {
        _valores.Add(ValorCategoria("Papelería", null, 7));

        var resultado = await _servicio.ObtenerValoresSinTraducirAsync(IdProveedor);

        resultado.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { TextoRubro = (string?)null, Texto = "PAPELERIA", CantidadDeItems = 7 });
    }

    [Fact]
    public async Task ObtenerValoresSinTraducirAsync_VariasEscriturasDelMismoValor_SuMaLasCantidades()
    {
        _valores.Add(ValorCategoria("Escolar", "Cuadernos", 2));
        _valores.Add(ValorCategoria("ESCOLAR", "cuadernos", 3));

        var resultado = await _servicio.ObtenerValoresSinTraducirAsync(IdProveedor);

        resultado.Should().ContainSingle().Which.CantidadDeItems.Should().Be(5);
    }

    [Fact]
    public async Task ObtenerValoresSinTraducirAsync_SinValores_NoConsultaLosDiccionarios()
    {
        var resultado = await _servicio.ObtenerValoresSinTraducirAsync(IdProveedor);

        resultado.Should().BeEmpty();
        await _diccionarios.DidNotReceiveWithAnyArgs().ObtenerParaLoteAsync(default, default!, default!, default);
    }

    // ---------- Ayudas ----------

    private CatalogoProveedor AgregarItem(int id, string descripcion, string? rubro = null, string? categoria = null)
    {
        var item = new CatalogoProveedor
        {
            Id = id,
            IdProveedor = IdProveedor,
            CodigoProveedor = $"P-{id}",
            DescripcionProveedor = descripcion,
            CostoReposicion = 100m
        };
        item.ActualizarClasificacionDelProveedor(rubro, categoria, null);
        _items.Add(item);
        return item;
    }

    private static TerminoDiccionarioDto Termino(string termino, int? idCategoria)
    {
        return new TerminoDiccionarioDto { Termino = termino, IdCategoria = idCategoria };
    }

    private static ValorDelProveedorDto ValorCategoria(string? rubro, string? categoria, int cantidad)
    {
        return new ValorDelProveedorDto { Tipo = TipoEquivalenciaEnum.Categoria, Rubro = rubro, Texto = categoria, CantidadDeItems = cantidad };
    }
}
