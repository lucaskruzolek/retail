using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Articulos;
using Retail.Application.Validators.Clasificacion;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Comandos de administración de los diccionarios del motor de catalogación (Track 6.6, Fase 4b) de punta a punta
/// contra SQL Server: la cascada de M-35 en una sola transacción, los índices únicos filtrados que liberan lo dado de
/// baja, el cotejamiento de la forma de comparación (M-38, M-39) y la traducción de una carrera.
/// </summary>
public class DiccionariosClasificacionIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_DicServ_{Guid.NewGuid():N}";
    private DbContextOptions<RetailDbContext> _options = null!;
    private RetailDbContext _context = null!;
    private int _idGerente;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(_options);
        await _context.Database.MigrateAsync();

        var gerente = Usuario.Crear("gerente", "Ana", "Gómez", "hash", RolUsuarioEnum.Gerente, "gerente@libreria.com");
        _context.Usuarios.Add(gerente);
        await _context.SaveChangesAsync();
        _idGerente = gerente.Id;
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
    public async Task BajaCategoria_ConTerminosYEquivalencias_LosDaDeBajaEnLaBaseYLiberaElTermino()
    {
        var rubro = await GuardarAsync(Rubro.Crear("Escolar"));
        var mochilas = await GuardarAsync(Categoria.Crear("Mochilas", rubro.Id));
        var cuadernos = await GuardarAsync(Categoria.Crear("Cuadernos", rubro.Id));
        var mochila = await GuardarAsync(TerminoClasificacion.CrearConCategoria("MOCHILA", mochilas.Id));
        var cuaderno = await GuardarAsync(TerminoClasificacion.CrearConCategoria("CUADERNO", cuadernos.Id));
        var proveedor = Proveedor.Crear("Distribuidora Valida", "20111111112");
        proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "MOCHILAS", mochilas.Id);
        proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS", cuadernos.Id);
        await GuardarAsync(proveedor);

        await CrearCategoriaService().BajaAsync(_idGerente, mochilas.Id);

        // Se lee con un contexto nuevo: lo que importa es lo que quedó en la base, no lo que hay en memoria.
        await using var verificacion = new RetailDbContext(_options);
        var terminos = await verificacion.TerminosClasificacion.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        terminos.Single(t => t.Id == mochila.Id).IsDeleted.Should().BeTrue();
        terminos.Single(t => t.Id == cuaderno.Id).IsDeleted.Should().BeFalse();
        var equivalencias = await verificacion.Set<EquivalenciaClasificacion>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync();
        equivalencias.Single(e => e.Texto == "MOCHILAS").IsDeleted.Should().BeTrue();
        equivalencias.Single(e => e.Texto == "CUADERNOS").IsDeleted.Should().BeFalse();
        (await verificacion.Categorias.IgnoreQueryFilters().SingleAsync(c => c.Id == mochilas.Id)).IsDeleted.Should().BeTrue();

        // El término pasó a desconocido: el índice único filtrado deja registrarlo de nuevo en otra categoría.
        await CrearTerminoService().RegistrarAsync(
            _idGerente,
            new RegistrarTerminoDto { Termino = "MOCHILA", IdCategoria = cuadernos.Id });
        var activos = await new ClasificacionQueryService(_context).ListarTerminosAsync("MOCHILA", null, false);
        activos.Should().ContainSingle().Which.IdCategoria.Should().Be(cuadernos.Id);
    }

    [Fact]
    public async Task BajaMarca_ConAliasPrefijosYEquivalencias_LosLiberaParaOtraMarca()
    {
        var vieja = Marca.Crear("Mooving");
        vieja.AgregarAlias("MOOV");
        vieja.AgregarPrefijo("7798100");
        await GuardarAsync(vieja);
        var nueva = await GuardarAsync(Marca.Crear("Mooving Nueva"));
        var proveedor = Proveedor.Crear("Distribuidora Valida", "20111111112");
        proveedor.AsignarEquivalenciaMarca("MOOVING", vieja.Id);
        await GuardarAsync(proveedor);
        var servicio = CrearMarcaService(new ClasificacionQueryService(_context));

        await servicio.BajaAsync(_idGerente, vieja.Id);
        await servicio.AgregarAliasAsync(_idGerente, new AliasMarcaDto { IdMarca = nueva.Id, Alias = "moov" });
        await servicio.AgregarPrefijoAsync(_idGerente, new PrefijoMarcaDto { IdMarca = nueva.Id, Prefijo = "7798100" });

        await using var verificacion = new RetailDbContext(_options);
        var equivalencia = await verificacion.Set<EquivalenciaClasificacion>().IgnoreQueryFilters().SingleAsync();
        equivalencia.IsDeleted.Should().BeTrue();
        var alias = await new ClasificacionQueryService(verificacion).ListarAliasAsync(null, idMarca: null);
        alias.Should().ContainSingle().Which.IdMarca.Should().Be(nueva.Id);
        var prefijos = await new ClasificacionQueryService(verificacion).ListarPrefijosAsync(null, idMarca: null);
        prefijos.Should().ContainSingle().Which.IdMarca.Should().Be(nueva.Id);
    }

    [Fact]
    public async Task AgregarAlias_IgualAlNombreConEnieDeOtraMarca_LoRechazaSinGuardar()
    {
        await GuardarAsync(Marca.Crear("Ñandú"));
        var otra = await GuardarAsync(Marca.Crear("Pinturas Del Sur"));

        var accion = () => CrearMarcaService(new ClasificacionQueryService(_context))
            .AgregarAliasAsync(_idGerente, new AliasMarcaDto { IdMarca = otra.Id, Alias = "nandu" });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("El alias 'NANDU' es el nombre de la marca 'Ñandú'.");
        (await _context.Set<AliasMarca>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CrearMarca_QueSoloDifiereEnLaEnie_LaRechazaAunqueElIndiceDeNombresLaAcepte()
    {
        // El índice único de nombres usa Modern_Spanish_CI_AI y aceptaría "Nandu" junto a "Ñandú" (D-8); M-39 lo
        // impide porque el motor de catalogación las vería como la misma marca.
        await GuardarAsync(Marca.Crear("Ñandú"));

        var accion = () => CrearMarcaService(new ClasificacionQueryService(_context))
            .CrearAsync(_idGerente, new CrearMarcaDto { Nombre = "Nandu" });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("Ya existe la marca 'Ñandú'*");
        (await _context.Marcas.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CrearMarca_ConNombreQueEsAliasDeOtra_LaRechaza()
    {
        var husares = Marca.Crear("Husares");
        husares.AgregarAlias("HUSAR");
        await GuardarAsync(husares);

        var accion = () => CrearMarcaService(new ClasificacionQueryService(_context))
            .CrearAsync(_idGerente, new CrearMarcaDto { Nombre = "Húsar" });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("*ya es un alias de la marca 'Husares'.");
    }

    [Fact]
    public async Task AgregarAlias_EnCarreraConOtraMarca_ElIndiceUnicoLoFrenaYLlegaTraducido()
    {
        // Simula la carrera: la comprobación previa no ve el alias (otro usuario lo guardó justo después), así que la
        // única defensa es el índice único filtrado de la base. La cadena completa es SqlException 2601 →
        // RestriccionDeUnicidadException (UnitOfWork) → DomainException con mensaje de negocio (servicio).
        var husares = Marca.Crear("Husares");
        husares.AgregarAlias("HUSAR");
        await GuardarAsync(husares);
        var otra = await GuardarAsync(Marca.Crear("Otra"));
        var consultasQueNoVen = Substitute.For<IClasificacionQueryService>();
        consultasQueNoVen.BuscarMarcaPorAliasAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        consultasQueNoVen.BuscarMarcaPorNombreAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var accion = () => CrearMarcaService(consultasQueNoVen)
            .AgregarAliasAsync(_idGerente, new AliasMarcaDto { IdMarca = otra.Id, Alias = "HUSAR" });

        (await accion.Should().ThrowAsync<DomainException>().WithMessage("*alias 'HUSAR' al mismo tiempo*"))
            .WithInnerException<RestriccionDeUnicidadException>();
        await using var verificacion = new RetailDbContext(_options);
        (await verificacion.Set<AliasMarca>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AsignarEquivalencia_YQuitarla_PersisteSoloAtravesDeLaRaiz()
    {
        var rubro = await GuardarAsync(Rubro.Crear("Escolar"));
        var categoria = await GuardarAsync(Categoria.Crear("Cuadernos", rubro.Id));
        var proveedor = await GuardarAsync(Proveedor.Crear("Distribuidora Valida", "20111111112"));
        var servicio = CrearEquivalenciaService();

        await servicio.AsignarAsync(_idGerente, new AsignarEquivalenciaDto
        {
            IdProveedor = proveedor.Id,
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = "Escolar",
            Texto = "Cuadernos  t/d",
            IdDestino = categoria.Id
        });
        var asignadas = await servicio.ListarAsync(proveedor.Id, tipo: null);
        await servicio.QuitarAsync(_idGerente, new QuitarEquivalenciaDto
        {
            IdProveedor = proveedor.Id,
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = "ESCOLAR",
            Texto = "CUADERNOS T/D"
        });
        var restantes = await servicio.ListarAsync(proveedor.Id, tipo: null);

        asignadas.Should().ContainSingle().Which.NombreCategoria.Should().Be("Cuadernos");
        restantes.Should().BeEmpty();
    }

    [Fact]
    public async Task BajaCategoria_ConParesDeTerminosAmbiguos_DaDeBajaSoloLosParesHaciaElla()
    {
        var rubro = await GuardarAsync(Rubro.Crear("Librería"));
        var geometria = await GuardarAsync(Categoria.Crear("Geometría", rubro.Id));
        var escolar = await GuardarAsync(Categoria.Crear("Útiles Escolares", rubro.Id));
        var set = TerminoClasificacion.CrearAmbiguo("SET");
        set.AsignarPar("GEOMETRIA", geometria.Id);
        set.AsignarPar("ESCOLAR", escolar.Id);
        set.AsignarPar("REGALO", null);
        await GuardarAsync(set);

        await CrearCategoriaService().BajaAsync(_idGerente, geometria.Id);

        await using var verificacion = new RetailDbContext(_options);
        var pares = await verificacion.Set<ParClasificacion>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
        pares.Single(p => p.SegundaPalabra == "GEOMETRIA").IsDeleted.Should().BeTrue();
        pares.Single(p => p.SegundaPalabra == "ESCOLAR").IsDeleted.Should().BeFalse();
        pares.Single(p => p.SegundaPalabra == "REGALO").IsDeleted.Should().BeFalse();
        (await verificacion.TerminosClasificacion.SingleAsync(t => t.Id == set.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task AsignarPares_LoteDeDosTerminos_LosGuardaYLaConsultaLosListaConSuCategoria()
    {
        var rubro = await GuardarAsync(Rubro.Crear("Librería"));
        var geometria = await GuardarAsync(Categoria.Crear("Geometría", rubro.Id));
        var set = await GuardarAsync(TerminoClasificacion.CrearAmbiguo("SET"));
        var kit = await GuardarAsync(TerminoClasificacion.CrearAmbiguo("KIT"));

        await CrearTerminoService().AsignarParesAsync(
            _idGerente,
            new AsignarParesDto
            {
                Pares =
                [
                    new AsignarParDto { IdTermino = set.Id, SegundaPalabra = "geometría", IdCategoria = geometria.Id },
                    new AsignarParDto { IdTermino = set.Id, SegundaPalabra = "REGALO" },
                    new AsignarParDto { IdTermino = kit.Id, SegundaPalabra = "COMPAS", IdCategoria = geometria.Id }
                ]
            });

        await using var verificacion = new RetailDbContext(_options);
        var consultas = new ClasificacionQueryService(verificacion);
        var conCategoria = await consultas.ListarParesAsync(null, null, incluirIgnorados: false);
        conCategoria.Select(p => (p.Termino, p.SegundaPalabra, p.NombreCategoria, p.NombreRubro))
            .Should().Equal(
                ("KIT", "COMPAS", "Geometría", "Librería"),
                ("SET", "GEOMETRIA", "Geometría", "Librería"));
        var todosDelSet = await consultas.ListarParesAsync(set.Id, null, incluirIgnorados: true);
        todosDelSet.Select(p => (p.SegundaPalabra, p.EsIgnorado))
            .Should().Equal(("GEOMETRIA", false), ("REGALO", true));
    }

    [Fact]
    public async Task ListarPares_FiltroYTerminoDadoDeBaja_FiltraEnTerminoYSegundaPalabraYExcluyeLoDadoDeBaja()
    {
        var set = TerminoClasificacion.CrearAmbiguo("SET");
        set.AsignarPar("GEOMETRIA", null);
        set.AsignarPar("ESCOLAR", null);
        var kit = TerminoClasificacion.CrearAmbiguo("KIT");
        kit.AsignarPar("GEOMETRIA", null);
        var repuesto = TerminoClasificacion.CrearAmbiguo("REPUESTO");
        repuesto.AsignarPar("GEOMETRIA", null);
        await GuardarAsync(set);
        await GuardarAsync(kit);
        await GuardarAsync(repuesto);
        await CrearTerminoService().BajaAsync(_idGerente, repuesto.Id);
        var consultas = new ClasificacionQueryService(_context);

        var porSegundaPalabra = await consultas.ListarParesAsync(null, "geométr", incluirIgnorados: true);
        var porTermino = await consultas.ListarParesAsync(null, "set", incluirIgnorados: true);

        porSegundaPalabra.Select(p => p.Termino).Should().Equal("KIT", "SET");
        porTermino.Select(p => p.SegundaPalabra).Should().Equal("ESCOLAR", "GEOMETRIA");
    }

    [Fact]
    public async Task ParesPropuestos_AceptarUnoYDescartarOtro_YaNoSeVuelvenAProponer()
    {
        var rubro = await GuardarAsync(Rubro.Crear("Librería"));
        var geometria = await GuardarAsync(Categoria.Crear("Geometría", rubro.Id));
        var escolar = await GuardarAsync(Categoria.Crear("Útiles Escolares", rubro.Id));
        var set = await GuardarAsync(TerminoClasificacion.CrearAmbiguo("SET"));
        await GuardarArticulosAsync(geometria.Id, "**SET DE GEOMETRIA 4 PIEZAS", "SET GEOMETRÍA ESCOLAR", "set geometria x3");
        await GuardarArticulosAsync(escolar.Id, "SET ESCOLAR RAYADO", "SET ESCOLAR LISO", "CASETE ESCOLAR");
        var servicio = CrearTerminoService();

        var propuestas = await servicio.ListarParesPropuestosAsync(null);

        propuestas.Select(p => (p.SegundaPalabra, p.NombreCategoria, p.Coincidencias, p.Total))
            .Should().Equal(("GEOMETRIA", "Geometría", 3, 3), ("ESCOLAR", "Útiles Escolares", 2, 2));

        // Aceptar GEOMETRIA y descartar ESCOLAR en una sola operación, como lo hará la Fase 6.
        await servicio.AsignarParesAsync(
            _idGerente,
            new AsignarParesDto
            {
                Pares =
                [
                    new AsignarParDto { IdTermino = set.Id, SegundaPalabra = "GEOMETRIA", IdCategoria = geometria.Id },
                    new AsignarParDto { IdTermino = set.Id, SegundaPalabra = "ESCOLAR" }
                ]
            });

        (await servicio.ListarParesPropuestosAsync(null)).Should().BeEmpty();
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_Exito_LaCategoriaYElTerminoQuedanEnLaBase()
    {
        var rubro = await GuardarAsync(Rubro.Crear("Juguetería"));

        var categoria = await CrearTerminoService().RegistrarConCategoriaNuevaAsync(
            _idGerente,
            new RegistrarTerminoConCategoriaNuevaDto { Termino = "PELOTERO", NombreCategoria = "Pelotas", IdRubro = rubro.Id });

        await using var lectura = new RetailDbContext(_options);
        var guardada = await lectura.Categorias.SingleAsync(c => c.NombreCategoria == "Pelotas");
        guardada.Id.Should().Be(categoria.IdCategoria);
        guardada.IdRubro.Should().Be(rubro.Id);
        (await lectura.TerminosClasificacion.SingleAsync(t => t.Termino == "PELOTERO")).IdCategoria
            .Should().Be(guardada.Id);
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_FallaAlGuardarElTermino_NoQuedaLaCategoriaYElContextoSigueUsable()
    {
        var rubro = await GuardarAsync(Rubro.Crear("Juguetería"));
        // La categoría se guarda (primer guardado, dentro de la transacción) y la base falla al guardar el término.
        await using var contexto = new RetailDbContext(
            new DbContextOptionsBuilder<RetailDbContext>(_options).AddInterceptors(new FallaEnElGuardado(2)).Options);
        var servicio = CrearTerminoService(contexto);

        var accion = () => servicio.RegistrarConCategoriaNuevaAsync(
            _idGerente,
            new RegistrarTerminoConCategoriaNuevaDto { Termino = "PELOTERO", NombreCategoria = "Pelotas", IdRubro = rubro.Id });

        await accion.Should().ThrowAsync<InvalidOperationException>();

        // El mismo contexto (vive lo que dura la pantalla, D-15) se usa para la operación siguiente: si la categoría
        // revertida hubiera quedado en el Change Tracker, se guardaría acá.
        await servicio.RegistrarAsync(_idGerente, new RegistrarTerminoDto { Termino = "GLOBO" });

        await using var lectura = new RetailDbContext(_options);
        (await lectura.Categorias.AnyAsync(c => c.NombreCategoria == "Pelotas")).Should().BeFalse();
        (await lectura.TerminosClasificacion.AnyAsync(t => t.Termino == "PELOTERO")).Should().BeFalse();
        (await lectura.TerminosClasificacion.AnyAsync(t => t.Termino == "GLOBO")).Should().BeTrue();
    }

    private async Task GuardarArticulosAsync(int idCategoria, params string[] descripciones)
    {
        foreach (var descripcion in descripciones)
        {
            var articulo = new Articulo
            {
                Descripcion = descripcion,
                CostoReposicion = 100,
                PorcentajeGanancia = 50,
                PrecioVenta = 150,
                StockActual = 1
            };
            articulo.Clasificar(idCategoria, OrigenClasificacionEnum.SugerenciaAceptada, null, null);
            _context.Articulos.Add(articulo);
        }

        await _context.SaveChangesAsync();
    }

    private CategoriaService CrearCategoriaService()
    {
        return new CategoriaService(
            new Repository<Categoria>(_context),
            new Repository<Rubro>(_context),
            new Repository<TerminoClasificacion>(_context),
            new Repository<Proveedor>(_context),
            new Repository<Usuario>(_context),
            new ClasificacionQueryService(_context),
            new UnitOfWork(_context),
            new CrearCategoriaValidator(),
            new ActualizarCategoriaValidator());
    }

    private MarcaService CrearMarcaService(IClasificacionQueryService consultas)
    {
        return new MarcaService(
            new Repository<Marca>(_context),
            new Repository<Proveedor>(_context),
            new Repository<Usuario>(_context),
            consultas,
            new UnitOfWork(_context),
            new CrearMarcaValidator(),
            new RenombrarMarcaValidator(),
            new AliasMarcaValidator(),
            new PrefijoMarcaValidator());
    }

    private TerminoClasificacionService CrearTerminoService(RetailDbContext? contexto = null)
    {
        contexto ??= _context;
        return new TerminoClasificacionService(
            new Repository<TerminoClasificacion>(contexto),
            new Repository<Categoria>(contexto),
            new Repository<Rubro>(contexto),
            new Repository<Usuario>(contexto),
            new ClasificacionQueryService(contexto),
            new UnitOfWork(contexto),
            new RegistrarTerminoValidator(),
            new RegistrarTerminoConCategoriaNuevaValidator(),
            new AsignarParesValidator(),
            new QuitarParValidator());
    }

    private EquivalenciaClasificacionService CrearEquivalenciaService()
    {
        return new EquivalenciaClasificacionService(
            new Repository<Proveedor>(_context),
            new Repository<Categoria>(_context),
            new Repository<Marca>(_context),
            new Repository<Usuario>(_context),
            new ClasificacionQueryService(_context),
            new UnitOfWork(_context),
            new AsignarEquivalenciaValidator(),
            new QuitarEquivalenciaValidator());
    }

    private async Task<T> GuardarAsync<T>(T entidad)
        where T : class
    {
        _context.Add(entidad);
        await _context.SaveChangesAsync();
        return entidad;
    }
}
