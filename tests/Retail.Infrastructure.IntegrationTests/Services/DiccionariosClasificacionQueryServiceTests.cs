using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Services;
using Retail.Application.Validators.Clasificacion;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Clasificacion;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Lectura de los diccionarios del motor de catalogación contra SQL Server Express (Track 6.6, Fase 4a): filtros por
/// lote ejecutados en el motor, datos dados de baja excluidos y equivalencias solo del proveedor pedido. Incluye un
/// recorrido completo de <see cref="ClasificacionService"/> sobre la base real.
/// </summary>
public class DiccionariosClasificacionQueryServiceTests : IAsyncLifetime, IDisposable
{
    private static readonly string[] TerminosDelLote = { "CUADERNO", "SET", "INEXISTENTE" };
    private static readonly string[] PrefijosDelLote = { "7798100", "7790001" };

    private readonly string _dbName = $"RetailDb_Test_DicLote_{Guid.NewGuid():N}";
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

    [Fact]
    public async Task ObtenerParaLote_ConTerminosYPrefijosDelLote_TraeSoloEsosYLosActivos()
    {
        var datos = await SembrarAsync();

        var resultado = await ConsultarAsync(datos.IdProveedor, TerminosDelLote, PrefijosDelLote);

        resultado.Terminos.Select(t => (t.Termino, t.IdCategoria)).Should().BeEquivalentTo(new (string, int?)[]
        {
            ("CUADERNO", datos.IdCuadernos),
            ("SET", null)
        }, "MOCHILA no es del lote y BORRADO está dado de baja");
        resultado.Prefijos.Should().ContainSingle().Which.Should().Be(new PrefijoDiccionarioDto { Prefijo = "7798100", IdMarca = datos.IdRivadavia });
    }

    [Fact]
    public async Task ObtenerParaLote_Marcas_TraeTodasLasActivasConSusAliasVigentes()
    {
        var datos = await SembrarAsync();

        var resultado = await ConsultarAsync(datos.IdProveedor, Array.Empty<string>(), Array.Empty<string>());

        var rivadavia = resultado.Marcas.Should().ContainSingle(m => m.IdMarca == datos.IdRivadavia).Subject;
        rivadavia.NombreMarca.Should().Be("Rivadavia");
        rivadavia.Alias.Should().Equal("RIVA");
        resultado.Marcas.Should().NotContain(m => m.NombreMarca == "Marca de baja");
    }

    [Fact]
    public async Task ObtenerParaLote_Equivalencias_TraeSoloLasVigentesDelProveedorPedido()
    {
        var datos = await SembrarAsync();

        var resultado = await ConsultarAsync(datos.IdProveedor, Array.Empty<string>(), Array.Empty<string>());

        resultado.Equivalencias.Select(e => (e.Tipo, e.TextoRubro, e.Texto, e.IdCategoria)).Should().BeEquivalentTo(new (TipoEquivalenciaEnum, string?, string, int?)[]
        {
            (TipoEquivalenciaEnum.Categoria, "ESCOLAR", "CUADERNOS T/D", datos.IdCuadernos),
            (TipoEquivalenciaEnum.Categoria, null, "VARIOS", null)
        });
        resultado.Categorias.Select(c => c.NombreCategoria).Should().BeEquivalentTo("Cuadernos", "Mochilas");
    }

    [Fact]
    public async Task Sugerir_ContraLaBaseReal_RecorreEquivalenciaTerminoYMarcaSinModelo()
    {
        var datos = await SembrarAsync();
        var catalogo = new CatalogoProveedor
        {
            IdProveedor = datos.IdProveedor,
            CodigoProveedor = "C-1",
            DescripcionProveedor = "**CUADERNO RIVA TAPA DURA",
            CodigoBarras = "7798100123455",
            CostoReposicion = 100m
        };
        catalogo.ActualizarClasificacionDelProveedor("Escolar", "Cuadernos T/D", null);
        _context.CatalogosProveedores.Add(catalogo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var servicio = new ClasificacionService(
            new CatalogoProveedorQueryService(_context),
            new DiccionariosClasificacionQueryService(_context),
            new ModeloCategoriaNoDisponible(),
            new SugerirClasificacionValidator());

        var resultado = await servicio.SugerirAsync(new SugerirClasificacionDto { IdProveedor = datos.IdProveedor, IdsCatalogo = new[] { catalogo.Id } });

        var item = resultado.Items.Single();
        item.Categoria.Should().BeEquivalentTo(new { Id = datos.IdCuadernos, Motivo = MotivoSugerenciaEnum.EquivalenciaDelProveedor });
        item.Marca.Should().BeEquivalentTo(new { Id = datos.IdRivadavia, Motivo = MotivoSugerenciaEnum.MarcaEnLaDescripcion });
        resultado.ModeloDisponible.Should().BeFalse();
    }

    private Task<DiccionariosClasificacionDto> ConsultarAsync(int idProveedor, string[] terminos, string[] prefijos)
    {
        return new DiccionariosClasificacionQueryService(_context).ObtenerParaLoteAsync(idProveedor, terminos, prefijos);
    }

    private sealed record DatosSembrados(int IdProveedor, int IdCuadernos, int IdRivadavia);

    /// <summary>
    /// Dos categorías activas y una dada de baja; términos activos y uno dado de baja; una marca con alias vigente y
    /// otro quitado, otra marca dada de baja; prefijos de ambas; equivalencias de dos proveedores, una quitada.
    /// </summary>
    private async Task<DatosSembrados> SembrarAsync()
    {
        var rubro = Rubro.Crear("Librería");
        _context.Rubros.Add(rubro);
        await _context.SaveChangesAsync();

        var cuadernos = Categoria.Crear("Cuadernos", rubro.Id);
        var mochilas = Categoria.Crear("Mochilas", rubro.Id);
        var deBaja = Categoria.Crear("Categoría de baja", rubro.Id);
        _context.Categorias.AddRange(cuadernos, mochilas, deBaja);

        var rivadavia = Marca.Crear("Rivadavia");
        rivadavia.AgregarAlias("RIVA");
        rivadavia.AgregarAlias("RIVADAV");
        rivadavia.AgregarPrefijo("7798100");
        var marcaDeBaja = Marca.Crear("Marca de baja");
        marcaDeBaja.AgregarPrefijo("7790001");
        _context.Marcas.AddRange(rivadavia, marcaDeBaja);

        var proveedor = Proveedor.Crear("Distribuidora Valida", "20111111112");
        var otroProveedor = Proveedor.Crear("Otra Distribuidora", "20123456786");
        _context.Proveedores.AddRange(proveedor, otroProveedor);
        await _context.SaveChangesAsync();

        var borrado = TerminoClasificacion.CrearConCategoria("BORRADO", cuadernos.Id);
        _context.TerminosClasificacion.AddRange(
            TerminoClasificacion.CrearConCategoria("CUADERNO", cuadernos.Id),
            TerminoClasificacion.CrearConCategoria("MOCHILA", mochilas.Id),
            TerminoClasificacion.CrearAmbiguo("SET"),
            borrado);

        proveedor.AsignarEquivalenciaCategoria("Escolar", "Cuadernos T/D", cuadernos.Id);
        proveedor.AsignarEquivalenciaCategoria(null, "Varios", null);
        proveedor.AsignarEquivalenciaCategoria(null, "Quitada", mochilas.Id);
        otroProveedor.AsignarEquivalenciaCategoria(null, "Ajena", mochilas.Id);
        await _context.SaveChangesAsync();

        borrado.MarkAsDeleted();
        deBaja.MarkAsDeleted();
        rivadavia.QuitarAlias("RIVADAV");
        proveedor.QuitarEquivalencia(TipoEquivalenciaEnum.Categoria, null, "Quitada");
        marcaDeBaja.MarkAsDeleted();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        return new DatosSembrados(proveedor.Id, cuadernos.Id, rivadavia.Id);
    }
}
