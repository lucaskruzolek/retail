using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Clasificacion;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

/// <summary>
/// Sugerencia de clasificación por niveles (Track 6.6, sección 4, M-14, M-25, M-26; pares de la Fase 5, M-48). Los
/// diccionarios se arman por test. Sin modelo desde la Fase 5 (M-45): lo que ninguna regla resuelve no se sugiere.
/// </summary>
public class ClasificacionServiceTests
{
    private const int IdProveedor = 1;
    private const int Cuadernos = 10;
    private const int Mochilas = 11;
    private const int Juegos = 12;
    private const int Bic = 20;
    private const int Rivadavia = 21;
    private const int Filgo = 22;


    private readonly ICatalogoProveedorQueryService _catalogo = Substitute.For<ICatalogoProveedorQueryService>();
    private readonly IDiccionariosClasificacionQueryService _diccionarios = Substitute.For<IDiccionariosClasificacionQueryService>();
    private readonly ClasificacionService _servicio;

    private readonly List<CatalogoProveedor> _items = new();
    private readonly List<TerminoDiccionarioDto> _terminos = new();
    private readonly List<ParDiccionarioDto> _pares = new();
    private readonly List<PrefijoDiccionarioDto> _prefijos = new();
    private readonly List<EquivalenciaDiccionarioDto> _equivalencias = new();
    private readonly List<CategoriaDto> _categorias = new()
    {
        Categoria(Cuadernos, "Cuadernos"),
        Categoria(Mochilas, "Mochilas"),
        Categoria(Juegos, "Juegos")
    };

    private readonly List<MarcaDiccionarioDto> _marcas = new()
    {
        new MarcaDiccionarioDto { IdMarca = Bic, NombreMarca = "Bic", Alias = Array.Empty<string>() },
        new MarcaDiccionarioDto { IdMarca = Rivadavia, NombreMarca = "Rivadavia", Alias = new[] { "RIVA" } },
        new MarcaDiccionarioDto { IdMarca = Filgo, NombreMarca = "Filgo", Alias = Array.Empty<string>() }
    };

    public ClasificacionServiceTests()
    {
        _catalogo.ObtenerPorIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(llamada => _items.Where(i => llamada.Arg<IEnumerable<int>>().Contains(i.Id)).ToList());
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
                Prefijos = _prefijos,
                Equivalencias = _equivalencias,
                Categorias = _categorias
            });

        _servicio = new ClasificacionService(_catalogo, _diccionarios, new SugerirClasificacionValidator());
    }

    // ---------- Validación ----------

    [Fact]
    public async Task Sugerir_SinItems_LanzaValidationException()
    {
        var accion = () => _servicio.SugerirAsync(new SugerirClasificacionDto { IdProveedor = IdProveedor, IdsCatalogo = Array.Empty<int>() });

        await accion.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Sugerir_ItemInexistente_LanzaDomainException()
    {
        AgregarItem(1, "CUADERNO RAYADO");

        var accion = () => SugerirAsync(1, 99);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("*99*");
    }

    [Fact]
    public async Task Sugerir_ItemDeOtroProveedor_LanzaDomainException()
    {
        AgregarItem(1, "CUADERNO RAYADO").IdProveedor = 2;

        var accion = () => SugerirAsync(1);

        await accion.Should().ThrowAsync<DomainException>();
    }

    // ---------- Categoría: equivalencias y términos ----------

    [Fact]
    public async Task Sugerir_TerminoNoAmbiguo_SugiereSuCategoriaPorTermino()
    {
        AgregarItem(1, "**cuaderno tapa dura");
        _terminos.Add(Termino("CUADERNO", Cuadernos));

        var item = (await SugerirAsync(1)).Items.Single();

        item.Termino.Should().Be("CUADERNO");
        item.Categoria.Should().BeEquivalentTo(new { Id = Cuadernos, Nombre = "Cuadernos", Motivo = MotivoSugerenciaEnum.Termino });
    }

    [Fact]
    public async Task Sugerir_EquivalenciaDelProveedor_PrecedeAlTermino()
    {
        AgregarItem(1, "CUADERNO ESPIRAL", rubro: "Escolar", categoria: "Cuadernos T/D");
        _terminos.Add(Termino("CUADERNO", Juegos));
        _equivalencias.Add(EquivalenciaCategoria("ESCOLAR", "CUADERNOS T/D", Cuadernos));

        var item = (await SugerirAsync(1)).Items.Single();

        item.Categoria!.Id.Should().Be(Cuadernos);
        item.Categoria.Motivo.Should().Be(MotivoSugerenciaEnum.EquivalenciaDelProveedor);
    }

    [Fact]
    public async Task Sugerir_EquivalenciaIgnorada_SigueConElTerminoSinInformarlaComoSinTraducir()
    {
        AgregarItem(1, "CUADERNO ESPIRAL", categoria: "Varios");
        _terminos.Add(Termino("CUADERNO", Cuadernos));
        _equivalencias.Add(EquivalenciaCategoria(null, "VARIOS", null));

        var resultado = await SugerirAsync(1);

        resultado.Items.Single().Categoria!.Motivo.Should().Be(MotivoSugerenciaEnum.Termino);
        resultado.ValoresSinTraducir.Should().BeEmpty();
    }

    [Fact]
    public async Task Sugerir_ValoresSinEquivalencia_LosInformaUnaVezPorValorConSuCantidad()
    {
        AgregarItem(1, "CUADERNO A", rubro: "Escolar", categoria: "Cuadernos");
        AgregarItem(2, "CUADERNO B", rubro: "escolar", categoria: "CUADERNOS");
        AgregarItem(3, "MOCHILA C", rubro: "Escolar");
        _terminos.Add(Termino("CUADERNO", Cuadernos));

        var resultado = await SugerirAsync(1, 2, 3);

        resultado.ValoresSinTraducir.Should().BeEquivalentTo(new[]
        {
            new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "ESCOLAR", Texto = "CUADERNOS", CantidadDeItems = 2 },
            new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = null, Texto = "ESCOLAR", CantidadDeItems = 1 }
        }, opciones => opciones.WithStrictOrdering());
        resultado.Items[0].Categoria!.Motivo.Should().Be(MotivoSugerenciaEnum.Termino, "sin equivalencia sigue con los otros niveles");
    }

    [Fact]
    public async Task Sugerir_TerminoConCategoriaDadaDeBaja_SeComportaComoAmbiguoYNoSugiere()
    {
        AgregarItem(1, "CUADERNO ESPIRAL");
        _terminos.Add(Termino("CUADERNO", 999));

        var resultado = await SugerirAsync(1);

        resultado.Items.Single().Categoria.Should().BeNull();
        resultado.Bloques.Should().BeEmpty("el término es conocido: no es un término nuevo para decidir en bloque");
    }

    // ---------- Categoría: pares del diccionario (Fase 5) ----------

    [Fact]
    public async Task Sugerir_TerminoAmbiguoConParDelDiccionario_SugiereLaCategoriaDelPar()
    {
        AgregarItem(1, "SET DE GEOMETRÍA. 4 PIEZAS");
        _terminos.Add(Termino("SET", null));
        _pares.Add(Par("SET", "GEOMETRIA", Juegos));

        var item = (await SugerirAsync(1)).Items.Single();

        item.Categoria.Should().BeEquivalentTo(
            new { Id = Juegos, Nombre = "Juegos", Motivo = MotivoSugerenciaEnum.ParDelDiccionario });
    }

    [Theory]
    [InlineData("SET ESCOLAR X 6")]
    [InlineData("SET")]
    [InlineData("SET DE LA")]
    public async Task Sugerir_TerminoAmbiguoSinParParaSuSegundaPalabra_NoSugiere(string descripcion)
    {
        AgregarItem(1, descripcion);
        _terminos.Add(Termino("SET", null));
        _pares.Add(Par("SET", "GEOMETRIA", Juegos));

        (await SugerirAsync(1)).Items.Single().Categoria.Should().BeNull();
    }

    [Fact]
    public async Task Sugerir_ParHaciaCategoriaDadaDeBaja_NoSugiere()
    {
        AgregarItem(1, "SET GEOMETRIA");
        _terminos.Add(Termino("SET", null));
        _pares.Add(Par("SET", "GEOMETRIA", 999));

        (await SugerirAsync(1)).Items.Single().Categoria.Should().BeNull();
    }

    [Fact]
    public async Task Sugerir_EquivalenciaDelProveedor_PrecedeAlPar()
    {
        AgregarItem(1, "SET GEOMETRIA", categoria: "Escolares");
        _terminos.Add(Termino("SET", null));
        _pares.Add(Par("SET", "GEOMETRIA", Juegos));
        _equivalencias.Add(EquivalenciaCategoria(null, "ESCOLARES", Cuadernos));

        var item = (await SugerirAsync(1)).Items.Single();

        item.Categoria.Should().BeEquivalentTo(new { Id = Cuadernos, Motivo = MotivoSugerenciaEnum.EquivalenciaDelProveedor });
    }

    [Fact]
    public async Task Sugerir_TerminoConCategoria_NoConsultaElPar()
    {
        AgregarItem(1, "SET GEOMETRIA");
        _terminos.Add(Termino("SET", Cuadernos));
        _pares.Add(Par("SET", "GEOMETRIA", Juegos));

        var item = (await SugerirAsync(1)).Items.Single();

        item.Categoria.Should().BeEquivalentTo(new { Id = Cuadernos, Motivo = MotivoSugerenciaEnum.Termino });
    }

    // ---------- Bloques de términos nuevos (M-26) ----------

    [Fact]
    public async Task Sugerir_TresItemsConUnTerminoNuevo_LosAgrupaEnUnBloqueSinSugerenciaPorItem()
    {
        var ids = Enumerable.Range(1, ClasificacionService.TamanoMinimoDeBloque).ToArray();
        foreach (var id in ids)
        {
            AgregarItem(id, $"PELOTERO MODELO {id}");
        }

        var resultado = await SugerirAsync(ids);

        var bloque = resultado.Bloques.Single();
        bloque.Termino.Should().Be("PELOTERO");
        bloque.IdsCatalogo.Should().Equal(ids);
        bloque.Ejemplos.Should().HaveCount(3);
        resultado.Items.Should().OnlyContain(i => i.PerteneceABloque && i.Categoria == null);
    }

    [Fact]
    public async Task Sugerir_DosItemsConUnTerminoNuevo_NoFormaBloqueNiSugiere()
    {
        var ids = Enumerable.Range(1, ClasificacionService.TamanoMinimoDeBloque - 1).ToArray();
        foreach (var id in ids)
        {
            AgregarItem(id, $"PELOTERO MODELO {id}");
        }

        var resultado = await SugerirAsync(ids);

        resultado.Bloques.Should().BeEmpty();
        resultado.Items.Should().OnlyContain(i => !i.PerteneceABloque && i.Categoria == null);
    }

    // ---------- Marca ----------

    [Fact]
    public async Task Sugerir_UnaMarcaEnLaDescripcion_LaSugierePorTexto()
    {
        AgregarItem(1, "REPUESTO RIVA N°3 C/E");

        var marca = (await SugerirAsync(1)).Items.Single().Marca;

        marca.Should().BeEquivalentTo(new { Id = Rivadavia, Nombre = "Rivadavia", Motivo = MotivoSugerenciaEnum.MarcaEnLaDescripcion });
    }

    [Fact]
    public async Task Sugerir_SinMarcaEnElTextoConPrefijoRegistrado_NoSugiereMarca()
    {
        AgregarItem(1, "LAPIZ NEGRO HB", codigoBarras: "7798100123455");
        _prefijos.Add(new PrefijoDiccionarioDto { Prefijo = "7798100", IdMarca = Filgo });

        (await SugerirAsync(1)).Items.Single().Marca.Should().BeNull();
    }

    [Fact]
    public async Task Sugerir_TextoYPrefijoDistintos_GanaElTexto()
    {
        AgregarItem(1, "LAPIZ NEGRO BIC", codigoBarras: "7798100123455");
        _prefijos.Add(new PrefijoDiccionarioDto { Prefijo = "7798100", IdMarca = Filgo });

        (await SugerirAsync(1)).Items.Single().Marca!.Id.Should().Be(Bic);
    }

    [Theory]
    [InlineData(Filgo, Filgo)]
    [InlineData(Rivadavia, null)]
    public async Task Sugerir_VariasMarcasEnElTexto_ElPrefijoDesempataSoloSiEsUnaDeEllas(int marcaDelPrefijo, int? esperada)
    {
        AgregarItem(1, "SET BIC FILGO X 3", codigoBarras: "7798100123455");
        _prefijos.Add(new PrefijoDiccionarioDto { Prefijo = "7798100", IdMarca = marcaDelPrefijo });

        (await SugerirAsync(1)).Items.Single().Marca?.Id.Should().Be(esperada);
    }

    [Fact]
    public async Task Sugerir_CodigoDeBarrasIgualAlCodigoInterno_NoUsaElPrefijo()
    {
        AgregarItem(1, "SET BIC FILGO X 3", codigoBarras: "7798100123455").CodigoProveedor = "7798100123455";
        _prefijos.Add(new PrefijoDiccionarioDto { Prefijo = "7798100", IdMarca = Filgo });

        (await SugerirAsync(1)).Items.Single().Marca.Should().BeNull();
    }

    [Fact]
    public async Task Sugerir_MarcaInformadaPorElProveedorQueExiste_PrecedeALaDescripcion()
    {
        AgregarItem(1, "LAPIZ NEGRO BIC", marca: "rivadavia");

        var marca = (await SugerirAsync(1)).Items.Single().Marca;

        marca!.Id.Should().Be(Rivadavia);
        marca.Motivo.Should().Be(MotivoSugerenciaEnum.MarcaInformadaPorElProveedor);
    }

    [Fact]
    public async Task Sugerir_MarcaInformadaConEquivalencia_LaTraduce()
    {
        AgregarItem(1, "LAPIZ NEGRO", marca: "Riv. Hnos.");
        _equivalencias.Add(new EquivalenciaDiccionarioDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "RIV. HNOS.", IdMarca = Rivadavia });

        var marca = (await SugerirAsync(1)).Items.Single().Marca;

        marca!.Id.Should().Be(Rivadavia);
        marca.Motivo.Should().Be(MotivoSugerenciaEnum.EquivalenciaDelProveedor);
    }

    [Fact]
    public async Task Sugerir_MarcaInformadaSinEquivalencia_LaInformaSinTraducirYSigueConLaDescripcion()
    {
        AgregarItem(1, "LAPIZ NEGRO BIC", marca: "Desconocida");

        var resultado = await SugerirAsync(1);

        resultado.Items.Single().Marca!.Motivo.Should().Be(MotivoSugerenciaEnum.MarcaEnLaDescripcion);
        resultado.ValoresSinTraducir.Should().ContainSingle(v => v.Tipo == TipoEquivalenciaEnum.Marca && v.Texto == "DESCONOCIDA");
    }

    // ---------- Lote ----------

    [Fact]
    public async Task Sugerir_VariosItems_RespetaElOrdenPedidoYConsultaLosDiccionariosUnaVez()
    {
        AgregarItem(1, "CUADERNO A");
        AgregarItem(2, "MOCHILA B");
        AgregarItem(3, "CUADERNO C");

        var resultado = await SugerirAsync(3, 1, 2);

        resultado.Items.Select(i => i.IdCatalogo).Should().Equal(3, 1, 2);
        await _diccionarios.Received(1).ObtenerParaLoteAsync(
            IdProveedor,
            Arg.Is<IReadOnlyCollection<string>>(t => t.Count == 2 && t.Contains("CUADERNO") && t.Contains("MOCHILA")),
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>());
    }

    // ---------- Ayudas ----------

    private Task<SugerenciasClasificacionDto> SugerirAsync(params int[] ids)
    {
        return _servicio.SugerirAsync(new SugerirClasificacionDto { IdProveedor = IdProveedor, IdsCatalogo = ids });
    }

    private CatalogoProveedor AgregarItem(
        int id,
        string descripcion,
        string? codigoBarras = null,
        string? rubro = null,
        string? categoria = null,
        string? marca = null)
    {
        var item = new CatalogoProveedor
        {
            Id = id,
            IdProveedor = IdProveedor,
            CodigoProveedor = $"P-{id}",
            DescripcionProveedor = descripcion,
            CodigoBarras = codigoBarras,
            CostoReposicion = 100m
        };
        item.ActualizarPrecio(100m, rubroProveedor: rubro, categoriaProveedor: categoria, marcaProveedor: marca);
        _items.Add(item);
        return item;
    }

    private static TerminoDiccionarioDto Termino(string termino, int? idCategoria)
    {
        return new TerminoDiccionarioDto { Termino = termino, IdCategoria = idCategoria };
    }

    private static ParDiccionarioDto Par(string termino, string segundaPalabra, int idCategoria)
    {
        return new ParDiccionarioDto { Termino = termino, SegundaPalabra = segundaPalabra, IdCategoria = idCategoria };
    }

    private static EquivalenciaDiccionarioDto EquivalenciaCategoria(string? rubro, string texto, int? idCategoria)
    {
        return new EquivalenciaDiccionarioDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = rubro, Texto = texto, IdCategoria = idCategoria };
    }

    private static CategoriaDto Categoria(int id, string nombre)
    {
        return new CategoriaDto { IdCategoria = id, NombreCategoria = nombre };
    }
}
