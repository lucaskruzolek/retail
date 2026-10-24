using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Consultas de administración de los diccionarios del motor de catalogación (Track 6.6, Fase 4b) contra SQL Server:
/// los filtros globales de borrado lógico, el push-down de los filtros (Ley 8) y el cotejamiento de la forma de
/// comparación solo se pueden verificar con el motor real.
/// </summary>
public class ClasificacionDiccionariosQueryServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_ClasDic_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private ClasificacionQueryService _sut = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        _sut = new ClasificacionQueryService(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ListarTerminosAsync_ConFiltroSinNormalizar_DevuelveCoincidenciasConCategoriaYRubro()
    {
        var categoria = await CrearCategoriaAsync("Mochilas", "Escolar");
        await GuardarAsync(
            TerminoClasificacion.CrearConCategoria("MOCHILA", categoria.Id),
            TerminoClasificacion.CrearConCategoria("CARTUCHERA", categoria.Id));

        var resultado = await _sut.ListarTerminosAsync(" móchi ", idCategoria: null, soloAmbiguos: false);

        var termino = resultado.Should().ContainSingle().Subject;
        termino.Termino.Should().Be("MOCHILA");
        termino.IdCategoria.Should().Be(categoria.Id);
        termino.NombreCategoria.Should().Be("Mochilas");
        termino.NombreRubro.Should().Be("Escolar");
        termino.EsAmbiguo.Should().BeFalse();
    }

    [Fact]
    public async Task ListarTerminosAsync_SoloAmbiguos_ExcluyeLosConCategoriaYLosDadosDeBaja()
    {
        var categoria = await CrearCategoriaAsync("Mochilas", "Escolar");
        var deBaja = TerminoClasificacion.CrearAmbiguo("REPUESTO");
        deBaja.MarkAsDeleted();
        await GuardarAsync(
            TerminoClasificacion.CrearAmbiguo("SET"),
            TerminoClasificacion.CrearConCategoria("MOCHILA", categoria.Id),
            deBaja);

        var resultado = await _sut.ListarTerminosAsync(null, idCategoria: null, soloAmbiguos: true);

        var termino = resultado.Should().ContainSingle().Subject;
        termino.Termino.Should().Be("SET");
        termino.EsAmbiguo.Should().BeTrue();
        termino.NombreCategoria.Should().BeNull();
    }

    [Fact]
    public async Task ListarTerminosAsync_PorCategoria_DevuelveSoloLosDeEsaCategoriaOrdenados()
    {
        var mochilas = await CrearCategoriaAsync("Mochilas", "Escolar");
        var cuadernos = await CrearCategoriaAsync("Cuadernos", "Escolar");
        await GuardarAsync(
            TerminoClasificacion.CrearConCategoria("MOCHILA", mochilas.Id),
            TerminoClasificacion.CrearConCategoria("BOLSO", mochilas.Id),
            TerminoClasificacion.CrearConCategoria("CUADERNO", cuadernos.Id));

        var resultado = await _sut.ListarTerminosAsync(null, mochilas.Id, soloAmbiguos: false);

        resultado.Select(t => t.Termino).Should().Equal("BOLSO", "MOCHILA");
    }

    [Fact]
    public async Task ListarAliasAsync_ExcluyeAliasDadosDeBajaYDeMarcasDadasDeBaja()
    {
        var husares = Marca.Crear("Husares");
        husares.AgregarAlias("HUSAR");
        husares.AgregarAlias("HUZAR");
        husares.QuitarAlias("HUZAR");
        var vieja = Marca.Crear("Vieja");
        vieja.AgregarAlias("VIEJITA");
        await GuardarAsync(husares, vieja);
        vieja.MarkAsDeleted();
        await _context.SaveChangesAsync();

        var resultado = await _sut.ListarAliasAsync(null, idMarca: null);

        var alias = resultado.Should().ContainSingle().Subject;
        alias.Alias.Should().Be("HUSAR");
        alias.NombreMarca.Should().Be("Husares");
        alias.IdMarca.Should().Be(husares.Id);
    }

    [Fact]
    public async Task ListarAliasAsync_FiltroPorNombreDeMarcaOPorAlias_DevuelveAmbasCoincidencias()
    {
        var dibu = Marca.Crear("Artística Dibu");
        dibu.AgregarAlias("DIBU");
        var sheaffer = Marca.Crear("Sheaffer");
        sheaffer.AgregarAlias("SCHEAFFER");
        var otra = Marca.Crear("Rivadavia");
        otra.AgregarAlias("RIVA");
        await GuardarAsync(dibu, sheaffer, otra);

        var porNombre = await _sut.ListarAliasAsync("artistica", idMarca: null);
        var porAlias = await _sut.ListarAliasAsync("scheaf", idMarca: null);

        porNombre.Select(a => a.Alias).Should().Equal("DIBU");
        porAlias.Select(a => a.Alias).Should().Equal("SCHEAFFER");
    }

    [Fact]
    public async Task ListarPrefijosAsync_FiltroDeDigitos_BuscaPorComienzoYPorMarca()
    {
        var filgo = Marca.Crear("Filgo");
        filgo.AgregarPrefijo("7791234");
        filgo.AgregarPrefijo("7790001");
        var bic = Marca.Crear("Bic");
        bic.AgregarPrefijo("3086120");
        await GuardarAsync(filgo, bic);

        var porComienzo = await _sut.ListarPrefijosAsync("779", idMarca: null);
        var porMedio = await _sut.ListarPrefijosAsync("1234", idMarca: null);
        var porMarca = await _sut.ListarPrefijosAsync(null, bic.Id);

        porComienzo.Select(p => p.Prefijo).Should().Equal("7790001", "7791234");
        porMedio.Should().BeEmpty();
        porMarca.Should().ContainSingle().Which.NombreMarca.Should().Be("Bic");
    }

    [Fact]
    public async Task ListarEquivalenciasAsync_DevuelveNombresDelDestinoEIgnoradas()
    {
        var categoria = await CrearCategoriaAsync("Cuadernos", "Escolar");
        var marca = await GuardarMarcaAsync("Rivadavia");
        var proveedor = Proveedor.Crear("Distribuidora Valida", "20111111112");
        proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS", categoria.Id);
        proveedor.AsignarEquivalenciaCategoria(null, "VARIOS", null);
        proveedor.AsignarEquivalenciaMarca("RIVA", marca.Id);
        var otro = Proveedor.Crear("Otro Proveedor", "20222222223");
        otro.AsignarEquivalenciaMarca("RIVA", marca.Id);
        await GuardarAsync(proveedor, otro);

        var todas = await _sut.ListarEquivalenciasAsync(proveedor.Id, tipo: null);
        var deMarca = await _sut.ListarEquivalenciasAsync(proveedor.Id, TipoEquivalenciaEnum.Marca);

        todas.Should().HaveCount(3);
        todas.Should().ContainSingle(e => e.Texto == "CUADERNOS")
            .Which.NombreCategoria.Should().Be("Cuadernos");
        todas.Should().ContainSingle(e => e.Texto == "VARIOS")
            .Which.EsIgnorada.Should().BeTrue();
        deMarca.Should().ContainSingle().Which.NombreMarca.Should().Be("Rivadavia");
    }

    [Fact]
    public async Task BuscarMarcaPorAliasAsync_AliasDeOtraMarca_DevuelveSuNombreYExcluyeLaPropia()
    {
        var husares = Marca.Crear("Husares");
        husares.AgregarAlias("HUSAR");
        await GuardarAsync(husares);

        var deOtra = await _sut.BuscarMarcaPorAliasAsync("HUSAR", idMarcaExcluida: null);
        var propia = await _sut.BuscarMarcaPorAliasAsync("HUSAR", husares.Id);
        var inexistente = await _sut.BuscarMarcaPorAliasAsync("OTRO", idMarcaExcluida: null);

        deOtra.Should().Be("Husares");
        propia.Should().BeNull();
        inexistente.Should().BeNull();
    }

    [Fact]
    public async Task BuscarMarcaPorAliasAsync_AliasDadoDeBaja_NoLoEncuentra()
    {
        var husares = Marca.Crear("Husares");
        husares.AgregarAlias("HUSAR");
        husares.QuitarAlias("HUSAR");
        await GuardarAsync(husares);

        var resultado = await _sut.BuscarMarcaPorAliasAsync("HUSAR", idMarcaExcluida: null);

        resultado.Should().BeNull();
    }

    [Theory]
    [InlineData("NANDU")]
    [InlineData("nandú")]
    public async Task BuscarMarcaPorNombreAsync_NombreConEnie_CoincideConLaFormaDeComparacion(string texto)
    {
        // Modern_Spanish_CI_AI distingue la ñ; la forma de comparación del motor no (NormalizarTexto). El COLLATE de
        // la consulta tiene que reproducir la regla del motor, no la de la columna.
        await GuardarMarcaAsync("Ñandú");

        var resultado = await _sut.BuscarMarcaPorNombreAsync(texto, idMarcaExcluida: null);

        resultado.Should().Be("Ñandú");
    }

    [Fact]
    public async Task BuscarMarcaPorNombreAsync_MarcaExcluidaODadaDeBaja_NoLaEncuentra()
    {
        var propia = await GuardarMarcaAsync("Faber Castell");
        var deBaja = await GuardarMarcaAsync("Pelikan");
        deBaja.MarkAsDeleted();
        await _context.SaveChangesAsync();

        var excluida = await _sut.BuscarMarcaPorNombreAsync("FABER CASTELL", propia.Id);
        var baja = await _sut.BuscarMarcaPorNombreAsync("PELIKAN", idMarcaExcluida: null);

        excluida.Should().BeNull();
        baja.Should().BeNull();
    }

    [Fact]
    public async Task BuscarMarcaPorPrefijoAsync_PrefijoDeOtraMarca_DevuelveSuNombreYExcluyeLaPropia()
    {
        var filgo = Marca.Crear("Filgo");
        filgo.AgregarPrefijo("7791234");
        await GuardarAsync(filgo);

        var deOtra = await _sut.BuscarMarcaPorPrefijoAsync("7791234", idMarcaExcluida: null);
        var propia = await _sut.BuscarMarcaPorPrefijoAsync("7791234", filgo.Id);

        deOtra.Should().Be("Filgo");
        propia.Should().BeNull();
    }

    [Fact]
    public async Task ListarProveedoresConEquivalenciasHaciaAsync_DevuelveProveedoresActivosSinRepetirNiMezclarTipos()
    {
        var categoria = await CrearCategoriaAsync("Cuadernos", "Escolar");
        var marca = await GuardarMarcaAsync("Rivadavia");
        marca.Id.Should().Be(categoria.Id, "la base es nueva: la primera fila de cada tabla tiene Id 1");
        var conDos = Proveedor.Crear("Distribuidora Valida", "20111111112");
        conDos.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS", categoria.Id);
        conDos.AsignarEquivalenciaCategoria("OFICINA", "CUADERNOS", categoria.Id);
        var soloMarcaConMismoId = Proveedor.Crear("Otro Proveedor", "20222222223");
        soloMarcaConMismoId.AsignarEquivalenciaMarca("RIVA", marca.Id);
        var deBaja = Proveedor.Crear("Proveedor De Baja", "20333333334");
        deBaja.AsignarEquivalenciaCategoria(null, "CUADERNOS", categoria.Id);
        await GuardarAsync(conDos, soloMarcaConMismoId, deBaja);
        deBaja.MarkAsDeleted();
        await _context.SaveChangesAsync();

        var resultado = await _sut.ListarProveedoresConEquivalenciasHaciaAsync(TipoEquivalenciaEnum.Categoria, categoria.Id);

        resultado.Should().Equal(conDos.Id);
    }

    private async Task<Categoria> CrearCategoriaAsync(string nombre, string nombreRubro)
    {
        var rubro = await _context.Rubros.FirstOrDefaultAsync(r => r.NombreRubro == nombreRubro);
        if (rubro is null)
        {
            rubro = Rubro.Crear(nombreRubro);
            await GuardarAsync(rubro);
        }

        var categoria = Categoria.Crear(nombre, rubro.Id);
        await GuardarAsync(categoria);
        return categoria;
    }

    private async Task<Marca> GuardarMarcaAsync(string nombre)
    {
        var marca = Marca.Crear(nombre);
        await GuardarAsync(marca);
        return marca;
    }

    private async Task GuardarAsync(params object[] entidades)
    {
        _context.AddRange(entidades);
        await _context.SaveChangesAsync();
    }
}
