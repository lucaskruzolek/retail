using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Persistencia de los pares "término ambiguo + segunda palabra" (Track 6.6, Fase 5, M-48, P-3, P-4) contra SQL Server
/// Express: el agregado completo con AutoInclude (M-57), el índice único filtrado, el CHECK de forma y las FK.
/// </summary>
public class ParesClasificacionPersistenciaTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Pares_{Guid.NewGuid():N}";
    private DbContextOptions<RetailDbContext> _options = null!;
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(_options);
        await _context.Database.CrearDesdePlantillaAsync();
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
    public async Task GuardarTermino_ConPares_LosRecuperaSinIncludeExplicito()
    {
        var idCategoria = await CrearCategoriaAsync();
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        termino.AsignarPar("GEOMETRIA", idCategoria);
        termino.AsignarPar("REGALO", null);
        _context.TerminosClasificacion.Add(termino);
        await _context.SaveChangesAsync();

        // Contexto nuevo y sin Include: AutoInclude (M-57) trae el agregado completo.
        await using var lectura = new RetailDbContext(_options);
        var leido = await lectura.TerminosClasificacion.SingleAsync(t => t.Id == termino.Id);

        leido.Pares.Select(p => (p.SegundaPalabra, p.IdCategoria, p.EsIgnorado))
            .Should().BeEquivalentTo(new (string, int?, bool)[]
            {
                ("GEOMETRIA", idCategoria, false),
                ("REGALO", null, true)
            });
    }

    [Fact]
    public async Task AsignarCategoria_TerminoCargadoSinInclude_DaDeBajaSusParesEnLaBase()
    {
        var idCategoria = await CrearCategoriaAsync();
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        termino.AsignarPar("GEOMETRIA", idCategoria);
        _context.TerminosClasificacion.Add(termino);
        await _context.SaveChangesAsync();

        // Como lo cargan hoy TerminoClasificacionService y CategoriaService: por Id o por predicado, sin Include.
        await using (var escritura = new RetailDbContext(_options))
        {
            var cargado = await escritura.TerminosClasificacion.SingleAsync(t => t.Termino == "SET");
            cargado.AsignarCategoria(idCategoria);
            await escritura.SaveChangesAsync();
        }

        await using var verificacion = new RetailDbContext(_options);
        var par = await verificacion.Set<ParClasificacion>().IgnoreQueryFilters().AsNoTracking().SingleAsync();
        par.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task BajaTermino_TerminoCargadoSinInclude_DaDeBajaSusParesEnLaBase()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        termino.AsignarPar("REGALO", null);
        _context.TerminosClasificacion.Add(termino);
        await _context.SaveChangesAsync();

        await using (var escritura = new RetailDbContext(_options))
        {
            var cargado = await escritura.TerminosClasificacion.SingleAsync(t => t.Id == termino.Id);
            cargado.MarkAsDeleted();
            await escritura.SaveChangesAsync();
        }

        await using var verificacion = new RetailDbContext(_options);
        var par = await verificacion.Set<ParClasificacion>().IgnoreQueryFilters().AsNoTracking().SingleAsync();
        par.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task InsertarPar_ClaveVigenteRepetida_LaRechazaElIndiceUnico()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        termino.AsignarPar("GEOMETRIA", null);
        _context.TerminosClasificacion.Add(termino);
        await _context.SaveChangesAsync();

        // Fuera del dominio, que ya lo impide con BuscarParVigente: el índice es la defensa en profundidad.
        var accion = () => _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PARES_CLASIFICACION (id_termino, segunda_palabra, created_at) VALUES ({termino.Id}, 'GEOMETRIA', SYSUTCDATETIME())");

        await accion.Should().ThrowAsync<SqlException>()
            .Where(e => e.Message.Contains("IX_PARES_CLASIFICACION_id_termino_segunda_palabra"));
    }

    [Fact]
    public async Task QuitarPar_YVolverAAsignarlo_ElIndiceFiltradoLiberaLaClave()
    {
        var idCategoria = await CrearCategoriaAsync();
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        termino.AsignarPar("GEOMETRIA", null);
        _context.TerminosClasificacion.Add(termino);
        await _context.SaveChangesAsync();

        termino.QuitarPar("GEOMETRIA");
        termino.AsignarPar("GEOMETRIA", idCategoria);
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().NotThrowAsync();
        var filas = await _context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM PARES_CLASIFICACION")
            .SingleAsync();
        filas.Should().Be(2);
    }

    [Fact]
    public async Task GuardarPares_MismaSegundaPalabraEnDosTerminos_EstaPermitido()
    {
        var set = TerminoClasificacion.CrearAmbiguo("SET");
        set.AsignarPar("ESCOLAR", null);
        var kit = TerminoClasificacion.CrearAmbiguo("KIT");
        kit.AsignarPar("ESCOLAR", null);
        _context.TerminosClasificacion.AddRange(set, kit);

        var accion = () => _context.SaveChangesAsync();

        await accion.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    [InlineData("TAPA DURA")]
    public async Task InsertarPar_SegundaPalabraSinFormaValida_LaRechazaElCheck(string segundaPalabra)
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        _context.TerminosClasificacion.Add(termino);
        await _context.SaveChangesAsync();

        var accion = () => _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PARES_CLASIFICACION (id_termino, segunda_palabra, created_at) VALUES ({termino.Id}, {segundaPalabra}, SYSUTCDATETIME())");

        await accion.Should().ThrowAsync<SqlException>()
            .Where(e => e.Message.Contains("CK_PARES_CLASIFICACION_SegundaPalabra"));
    }

    [Fact]
    public async Task GuardarPar_ConCategoriaInexistente_LanzaDbUpdateException()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        termino.AsignarPar("GEOMETRIA", 9999);
        _context.TerminosClasificacion.Add(termino);

        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    private async Task<int> CrearCategoriaAsync()
    {
        var rubro = Rubro.Crear("Librería");
        _context.Rubros.Add(rubro);
        await _context.SaveChangesAsync();
        var categoria = Categoria.Crear("Geometría", rubro.Id);
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        return categoria.Id;
    }
}
