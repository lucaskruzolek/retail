using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express la carga de la semilla de diccionarios (Track 6.6, paso 6.1, M-60): aditiva,
/// idempotente, sin pisar lo que el comercio editó ni resucitar lo que dio de baja (S-2), y todo o nada. La semilla es
/// inventada; las verificaciones leen con un contexto nuevo para no confundir el Change Tracker con la base.
/// </summary>
public class SemilleroDiccionariosTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Diccionarios_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        _context = CrearContexto();
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
    public async Task CargarAsync_BaseRecienMigrada_CreaTodosLosDiccionarios()
    {
        var resultado = await SemilleroDiccionarios.CargarAsync(_context, Semilla());

        resultado.Conflictos.Should().BeEmpty();
        resultado.Rubros.Creados.Should().Be(2);
        resultado.Categorias.Creados.Should().Be(3);
        resultado.Marcas.Creados.Should().Be(2);
        resultado.Alias.Creados.Should().Be(1);
        resultado.Prefijos.Creados.Should().Be(1);
        resultado.Terminos.Creados.Should().Be(2);
        resultado.Pares.Creados.Should().Be(1);

        await using var lectura = CrearContexto();
        var cuadernos = await lectura.Categorias.SingleAsync(c => c.NombreCategoria == "Cuadernos");
        var pinturas = await lectura.Categorias.SingleAsync(c => c.NombreCategoria == "Pinturas");
        (await lectura.TerminosClasificacion.SingleAsync(t => t.Termino == "CUADERNO")).IdCategoria
            .Should().Be(cuadernos.Id);
        var terminoBase = await lectura.TerminosClasificacion.SingleAsync(t => t.Termino == "BASE");
        terminoBase.EsAmbiguo.Should().BeTrue();
        terminoBase.Pares.Should().ContainSingle(p => p.SegundaPalabra == "ACRILICA" && p.IdCategoria == pinturas.Id);
        var pelikan = await lectura.Marcas
            .Include(m => m.Alias)
            .Include(m => m.Prefijos)
            .SingleAsync(m => m.NombreMarca == "Pelikan");
        pelikan.Alias.Should().ContainSingle(a => a.Alias == "PELIKAN ARG");
        pelikan.Prefijos.Should().ContainSingle(p => p.Prefijo == "0071402");
    }

    [Fact]
    public async Task CargarAsync_SegundaCorrida_NoCreaNadaYCuentaTodoComoExistente()
    {
        await SemilleroDiccionarios.CargarAsync(_context, Semilla());

        await using var otraCorrida = CrearContexto();
        var resultado = await SemilleroDiccionarios.CargarAsync(otraCorrida, Semilla());

        resultado.TotalCreados.Should().Be(0);
        resultado.Conflictos.Should().BeEmpty();
        resultado.Categorias.Existentes.Should().Be(3);
        resultado.Alias.Existentes.Should().Be(1);
        resultado.Prefijos.Existentes.Should().Be(1);
        resultado.Pares.Existentes.Should().Be(1);
    }

    [Fact]
    public async Task CargarAsync_MarcaConOtraGrafia_NoLaDuplica()
    {
        // La base tiene "Pelikan" como la cargó el comercio y "Ñandú"; la semilla las trae en mayúsculas y sin eñe.
        _context.Marcas.Add(Marca.Crear("Pelikan"));
        _context.Marcas.Add(Marca.Crear("Ñandú"));
        await _context.SaveChangesAsync();

        var semilla = Semilla() with
        {
            Marcas = [new MarcaSemilla("PELIKAN", "PELIKAN ARG"), new MarcaSemilla("NANDU", null)],
            Prefijos = [new PrefijoSemilla("0071402", "PELIKAN")]
        };
        await using var carga = CrearContexto();
        var resultado = await SemilleroDiccionarios.CargarAsync(carga, semilla);

        resultado.Marcas.Existentes.Should().Be(2);
        resultado.Marcas.Creados.Should().Be(0);
        resultado.Alias.Creados.Should().Be(1);
        await using var lectura = CrearContexto();
        (await lectura.Marcas.Select(m => m.NombreMarca).ToListAsync())
            .Should().BeEquivalentTo("Pelikan", "Ñandú");
    }

    [Fact]
    public async Task CargarAsync_CategoriaRenombradaPorElComercio_NoLaPisaYSusTerminosSiguenApuntandola()
    {
        await SemilleroDiccionarios.CargarAsync(_context, Semilla());
        var cuadernos = await _context.Categorias.SingleAsync(c => c.NombreCategoria == "Cuadernos");
        cuadernos.Renombrar("Cuadernos Escolares");
        await _context.SaveChangesAsync();

        await using var otraCorrida = CrearContexto();
        var resultado = await SemilleroDiccionarios.CargarAsync(otraCorrida, Semilla());

        await using var lectura = CrearContexto();
        (await lectura.Categorias.AnyAsync(c => c.NombreCategoria == "Cuadernos Escolares")).Should().BeTrue();
        (await lectura.TerminosClasificacion.SingleAsync(t => t.Termino == "CUADERNO")).IdCategoria
            .Should().Be(cuadernos.Id);
        resultado.Conflictos.Should().ContainSingle(c => c.StartsWith("Término 'CUADERNO'"));
        // La semilla identifica las categorías por su nombre: la de la semilla vuelve a crearse, vacía.
        resultado.Categorias.Creados.Should().Be(1);
    }

    [Fact]
    public async Task CargarAsync_CategoriaEnOtroRubro_UsaLaExistenteYAvisa()
    {
        var bazar = Rubro.Crear("Bazar");
        _context.Rubros.Add(bazar);
        await _context.SaveChangesAsync();
        _context.Categorias.Add(Categoria.Crear("Cuadernos", bazar.Id));
        await _context.SaveChangesAsync();

        await using var carga = CrearContexto();
        var resultado = await SemilleroDiccionarios.CargarAsync(carga, Semilla());

        resultado.Categorias.Existentes.Should().Be(1);
        resultado.Conflictos.Should().ContainSingle(c => c.Contains("ya existe en el rubro 'Bazar'"));
        await using var lectura = CrearContexto();
        var cuadernos = await lectura.Categorias.SingleAsync(c => c.NombreCategoria == "Cuadernos");
        cuadernos.IdRubro.Should().Be(bazar.Id);
        (await lectura.TerminosClasificacion.SingleAsync(t => t.Termino == "CUADERNO")).IdCategoria
            .Should().Be(cuadernos.Id);
    }

    [Fact]
    public async Task CargarAsync_AliasQueYaEsDeOtraMarca_InformaConflictoYCargaElResto()
    {
        var maped = Marca.Crear("Maped");
        maped.AgregarAlias("PELIKAN ARG");
        _context.Marcas.Add(maped);
        await _context.SaveChangesAsync();

        await using var carga = CrearContexto();
        var resultado = await SemilleroDiccionarios.CargarAsync(carga, Semilla());

        resultado.Alias.Omitidos.Should().Be(1);
        resultado.Conflictos.Should().ContainSingle(c => c.StartsWith("Alias 'PELIKAN ARG'"));
        resultado.Marcas.Creados.Should().Be(2);
        resultado.Terminos.Creados.Should().Be(2);
        await using var lectura = CrearContexto();
        (await lectura.Set<AliasMarca>().SingleAsync()).IdMarca.Should().Be(maped.Id);
    }

    [Fact]
    public async Task CargarAsync_TerminoQueElComercioClasifico_NoLoPisaYOmiteSusPares()
    {
        await SemilleroDiccionarios.CargarAsync(_context, Semilla() with { Pares = [] });
        var pinturas = await _context.Categorias.SingleAsync(c => c.NombreCategoria == "Pinturas");
        var terminoBase = await _context.TerminosClasificacion.SingleAsync(t => t.Termino == "BASE");
        terminoBase.AsignarCategoria(pinturas.Id);
        await _context.SaveChangesAsync();

        await using var otraCorrida = CrearContexto();
        var resultado = await SemilleroDiccionarios.CargarAsync(otraCorrida, Semilla());

        resultado.Terminos.Omitidos.Should().Be(1);
        resultado.Pares.Omitidos.Should().Be(1);
        resultado.Conflictos.Should().Contain(c => c.StartsWith("Par 'BASE ACRILICA'"));
        await using var lectura = CrearContexto();
        (await lectura.TerminosClasificacion.SingleAsync(t => t.Termino == "BASE")).IdCategoria
            .Should().Be(pinturas.Id);
    }

    [Fact]
    public async Task CargarAsync_LoQueElComercioDioDeBaja_NoLoVuelveACrear()
    {
        await SemilleroDiccionarios.CargarAsync(_context, Semilla());
        var oficina = await _context.Categorias.SingleAsync(c => c.NombreCategoria == "Oficina");
        oficina.MarkAsDeleted();
        var pelikan = await _context.Marcas.Include(m => m.Prefijos).SingleAsync(m => m.NombreMarca == "Pelikan");
        pelikan.QuitarPrefijo("0071402");
        var terminoBase = await _context.TerminosClasificacion.SingleAsync(t => t.Termino == "BASE");
        terminoBase.QuitarPar("ACRILICA");
        await _context.SaveChangesAsync();

        await using var otraCorrida = CrearContexto();
        var resultado = await SemilleroDiccionarios.CargarAsync(otraCorrida, Semilla());

        resultado.TotalCreados.Should().Be(0);
        resultado.Categorias.DadosDeBaja.Should().Be(1);
        resultado.Prefijos.DadosDeBaja.Should().Be(1);
        resultado.Pares.DadosDeBaja.Should().Be(1);
        await using var lectura = CrearContexto();
        (await lectura.Categorias.AnyAsync(c => c.NombreCategoria == "Oficina")).Should().BeFalse();
        (await lectura.Set<PrefijoEanMarca>().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CargarAsync_FallaDespuesDeGuardarLosPrimerosNiveles_NoDejaNadaGuardado()
    {
        // Falla en el tercer SaveChanges (términos y pares), cuando rubros, categorías y marcas ya se escribieron.
        await using var carga = CrearContexto(new FallaEnElGuardado(numeroDeGuardado: 3));

        var cargar = () => SemilleroDiccionarios.CargarAsync(carga, Semilla());

        await cargar.Should().ThrowAsync<InvalidOperationException>();
        await using var lectura = CrearContexto();
        (await lectura.Rubros.AnyAsync()).Should().BeFalse();
        (await lectura.Categorias.AnyAsync()).Should().BeFalse();
        (await lectura.Marcas.AnyAsync()).Should().BeFalse();
        (await lectura.TerminosClasificacion.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CargarAsync_SemillaVersionadaSobreBaseVacia_CargaTodoSinConflictos()
    {
        // S-4: detecta una copia rota o una inconsistencia entre los archivos de src/Retail.Cli/Semilla/ antes de
        // distribuirlos. Desde M-59 la semilla es pública: no es la lista del distribuidor.
        var semilla = LectorSemillaDiccionarios.Leer(Path.Combine(AppContext.BaseDirectory, "Semilla"));

        var resultado = await SemilleroDiccionarios.CargarAsync(_context, semilla);

        resultado.Conflictos.Should().BeEmpty();
        resultado.Categorias.Creados.Should().Be(semilla.Categorias.Count);
        resultado.Marcas.Creados.Should().Be(semilla.Marcas.Count);
        resultado.Alias.Creados.Should().Be(semilla.Marcas.Count(m => m.Alias is not null));
        resultado.Prefijos.Creados.Should().Be(semilla.Prefijos.Count);
        resultado.Terminos.Creados.Should().Be(semilla.Terminos.Count);
        resultado.Pares.Creados.Should().Be(semilla.Pares.Count);
    }

    private static SemillaDiccionarios Semilla()
    {
        return new SemillaDiccionarios(
            Categorias:
            [
                new CategoriaSemilla("Librería", "Cuadernos"),
                new CategoriaSemilla("Librería", "Oficina"),
                new CategoriaSemilla("Arte", "Pinturas")
            ],
            Terminos: [new TerminoSemilla("CUADERNO", "Cuadernos"), new TerminoSemilla("BASE", null)],
            Marcas: [new MarcaSemilla("Pelikan", "PELIKAN ARG"), new MarcaSemilla("Ñandú", null)],
            Prefijos: [new PrefijoSemilla("0071402", "Pelikan")],
            Pares: [new ParSemilla("BASE", "ACRILICA", "Pinturas")]);
    }

    private RetailDbContext CrearContexto(params IInterceptor[] interceptores)
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(interceptores)
            .Options;
        return new RetailDbContext(options);
    }
}
