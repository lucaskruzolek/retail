using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Persistencia de los diccionarios del motor de catalogación y del origen de la clasificación (Track 6.6, Fase 3,
/// M-27 a M-30) contra SQL Server Express: mapeo de los agregados, índices únicos filtrados que cruzan agregados y
/// restricciones CHECK de defensa en profundidad.
/// </summary>
public class DiccionariosCatalogacionPersistenciaTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_DicCat_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();
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

    // ---------- Marca: alias y prefijos ----------

    [Fact]
    public async Task GuardarMarca_ConAliasYPrefijos_LosRecuperaConElAgregado()
    {
        var marca = Marca.Crear("Husares");
        marca.AgregarAlias("husar");
        marca.AgregarPrefijo("7798100");
        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var leida = await _context.Marcas
            .Include(m => m.Alias)
            .Include(m => m.Prefijos)
            .SingleAsync(m => m.Id == marca.Id);

        leida.Alias.Should().ContainSingle().Which.Alias.Should().Be("HUSAR");
        leida.Prefijos.Should().ContainSingle().Which.Prefijo.Should().Be("7798100");
    }

    [Fact]
    public async Task GuardarAlias_YaUsadoPorOtraMarca_LanzaDbUpdateException()
    {
        var primera = Marca.Crear("Husares");
        primera.AgregarAlias("HUSAR");
        _context.Marcas.Add(primera);
        await _context.SaveChangesAsync();

        var segunda = Marca.Crear("Otra");
        segunda.AgregarAlias("HUSAR");
        _context.Marcas.Add(segunda);
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GuardarPrefijo_YaUsadoPorOtraMarca_LanzaDbUpdateException()
    {
        var primera = Marca.Crear("Mooving");
        primera.AgregarPrefijo("7798100");
        _context.Marcas.Add(primera);
        await _context.SaveChangesAsync();

        var segunda = Marca.Crear("Filgo");
        segunda.AgregarPrefijo("7798100");
        _context.Marcas.Add(segunda);
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task QuitarAlias_YGuardar_LoLiberaParaOtraMarcaYNoSeVuelveACargar()
    {
        var primera = Marca.Crear("Husares");
        primera.AgregarAlias("HUSAR");
        _context.Marcas.Add(primera);
        await _context.SaveChangesAsync();

        primera.QuitarAlias("HUSAR");
        var segunda = Marca.Crear("Otra");
        segunda.AgregarAlias("HUSAR");
        _context.Marcas.Add(segunda);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var leida = await _context.Marcas.Include(m => m.Alias).SingleAsync(m => m.Id == primera.Id);
        leida.Alias.Should().BeEmpty("el filtro global de borrado lógico excluye el alias dado de baja");
    }

    [Fact]
    public async Task DarDeBajaMarca_CargadaConSusDiccionarios_LiberaAliasYPrefijos()
    {
        var marca = Marca.Crear("Mooving");
        marca.AgregarAlias("MOOV");
        marca.AgregarPrefijo("7798100");
        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var cargada = await _context.Marcas
            .Include(m => m.Alias)
            .Include(m => m.Prefijos)
            .SingleAsync(m => m.Id == marca.Id);
        cargada.MarkAsDeleted();
        await _context.SaveChangesAsync();

        var nueva = Marca.Crear("Mooving Nueva");
        nueva.AgregarAlias("MOOV");
        nueva.AgregarPrefijo("7798100");
        _context.Marcas.Add(nueva);
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("2000001")]
    [InlineData("779810")]
    [InlineData("779810A")]
    public async Task InsertarPrefijo_FueraDeLaFormaCanonica_LoRechazaElCheck(string prefijo)
    {
        var marca = Marca.Crear("Mooving");
        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();

        var accion = () => _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PREFIJOS_EAN_MARCAS (id_marca, prefijo, created_at) VALUES ({marca.Id}, {prefijo}, SYSUTCDATETIME())");

        await accion.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>()
            .Where(e => e.Message.Contains("CK_PREFIJOS_EAN_MARCAS_Prefijo"));
    }

    // ---------- Términos ----------

    [Fact]
    public async Task GuardarTerminos_ConCategoriaYAmbiguo_ConservaLaAmbiguedadDerivada()
    {
        var idCategoria = await CrearCategoriaAsync("Mochilas");
        _context.TerminosClasificacion.AddRange(
            TerminoClasificacion.CrearConCategoria("MOCHILA", idCategoria),
            TerminoClasificacion.CrearAmbiguo("SET"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var terminos = await _context.TerminosClasificacion.OrderBy(t => t.Termino).ToListAsync();

        terminos.Select(t => (t.Termino, t.IdCategoria, t.EsAmbiguo)).Should().Equal(
            ("MOCHILA", idCategoria, false),
            ("SET", null, true));
    }

    [Fact]
    public async Task GuardarTermino_YaAsignadoAOtraCategoria_LanzaDbUpdateException()
    {
        var mochilas = await CrearCategoriaAsync("Mochilas");
        var bolsos = await CrearCategoriaAsync("Bolsos");
        _context.TerminosClasificacion.Add(TerminoClasificacion.CrearConCategoria("MOCHILA", mochilas));
        await _context.SaveChangesAsync();

        _context.TerminosClasificacion.Add(TerminoClasificacion.CrearConCategoria("MOCHILA", bolsos));
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GuardarTermino_ConCategoriaInexistente_LanzaDbUpdateException()
    {
        _context.TerminosClasificacion.Add(TerminoClasificacion.CrearConCategoria("MOCHILA", 9999));
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    // ---------- Origen de la clasificación en ARTICULOS ----------

    [Fact]
    public async Task GuardarArticulo_ConOrigenes_LosGuardaComoTextoYLosRecupera()
    {
        var idCategoria = await CrearCategoriaAsync("Mochilas");
        var marca = Marca.Crear("Mooving");
        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();

        var articulo = CrearArticulo();
        articulo.Clasificar(
            idCategoria,
            OrigenClasificacionEnum.SugerenciaAceptada,
            marca.Id,
            OrigenClasificacionEnum.SugerenciaCorregida);
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var leido = await _context.Articulos.SingleAsync(a => a.Id == articulo.Id);
        var texto = await _context.Database
            .SqlQuery<string>($"SELECT origen_marca AS Value FROM ARTICULOS WHERE id_articulo = {articulo.Id}")
            .SingleAsync();

        leido.OrigenCategoria.Should().Be(OrigenClasificacionEnum.SugerenciaAceptada);
        leido.OrigenMarca.Should().Be(OrigenClasificacionEnum.SugerenciaCorregida);
        texto.Should().Be("SugerenciaCorregida");
    }

    [Fact]
    public async Task GuardarArticulo_SinMarcaConfirmada_EsAceptadoPorElCheck()
    {
        var articulo = CrearArticulo();
        articulo.Clasificar(null, null, null, OrigenClasificacionEnum.Manual);
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var leido = await _context.Articulos.SingleAsync(a => a.Id == articulo.Id);
        leido.IdMarca.Should().BeNull();
        leido.OrigenMarca.Should().Be(OrigenClasificacionEnum.Manual);
    }

    [Theory]
    [InlineData("UPDATE ARTICULOS SET origen_categoria = NULL WHERE id_articulo = {0}", "CK_ARTICULOS_OrigenCategoria")]
    [InlineData("UPDATE ARTICULOS SET id_categoria = NULL WHERE id_articulo = {0}", "CK_ARTICULOS_OrigenCategoria")]
    [InlineData("UPDATE ARTICULOS SET origen_marca = NULL WHERE id_articulo = {0}", "CK_ARTICULOS_OrigenMarca")]
    [InlineData("UPDATE ARTICULOS SET origen_categoria = 'Inventado' WHERE id_articulo = {0}", "CK_ARTICULOS_OrigenCategoria")]
    public async Task ModificarArticulo_FueraDelDominio_RompiendoElOrigen_LoRechazaElCheck(string sql, string check)
    {
        var idCategoria = await CrearCategoriaAsync("Mochilas");
        var marca = Marca.Crear("Mooving");
        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();
        var articulo = CrearArticulo();
        articulo.Clasificar(idCategoria, OrigenClasificacionEnum.Manual, marca.Id, OrigenClasificacionEnum.Manual);
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();

        // {0} es un parámetro de ExecuteSqlRawAsync, no un reemplazo de texto.
        var accion = () => _context.Database.ExecuteSqlRawAsync(sql, articulo.Id);

        await accion.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>()
            .Where(e => e.Message.Contains(check));
    }

    private async Task<int> CrearCategoriaAsync(string nombre)
    {
        var rubro = await _context.Rubros.FirstOrDefaultAsync();
        if (rubro == null)
        {
            rubro = Rubro.Crear("General");
            _context.Rubros.Add(rubro);
            await _context.SaveChangesAsync();
        }

        var categoria = Categoria.Crear(nombre, rubro.Id);
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        return categoria.Id;
    }

    private static Articulo CrearArticulo()
    {
        return new Articulo
        {
            Descripcion = "Mochila escolar",
            CostoReposicion = 100m,
            PorcentajeGanancia = 50m,
            PrecioVenta = 150m,
            StockActual = 1,
            StockMinimo = 0
        };
    }
}
