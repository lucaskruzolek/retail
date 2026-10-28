using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Clasificacion;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

/// <summary>
/// Casos de uso de la tabla de términos (Track 6.6, M-16, M-27): autorización D-5, registro o reasignación de un
/// término único en el catálogo, término ambiguo y baja; y de sus pares (Fase 5, M-48, P-3, P-4).
/// </summary>
public class TerminoClasificacionServiceTests
{
    private const int IdGerente = 1;
    private const int IdCajero = 3;
    private const int IdCategoria = 7;
    private const int IdOtraCategoria = 8;
    private const int IdTermino = 30;
    private const int IdOtroTermino = 31;
    private const int IdRubro = 4;
    private const int IdCategoriaNueva = 99;

    private static readonly string[] SoloKit = { "KIT" };

    private readonly IRepository<TerminoClasificacion> _repository = Substitute.For<IRepository<TerminoClasificacion>>();
    private readonly IRepository<Categoria> _categorias = Substitute.For<IRepository<Categoria>>();
    private readonly IRepository<Rubro> _rubros = Substitute.For<IRepository<Rubro>>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IClasificacionQueryService _consultas = Substitute.For<IClasificacionQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<Categoria> _categoriasActivas = [];
    private readonly TerminoClasificacionService _servicio;

    public TerminoClasificacionServiceTests()
    {
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);
        RegistrarCategoria(IdCategoria);
        RegistrarCategoria(IdOtraCategoria);
        RegistrarRubro(IdRubro);
        ConfigurarExistentes();

        _servicio = new TerminoClasificacionService(
            _repository,
            _categorias,
            _rubros,
            _usuarios,
            _consultas,
            _unitOfWork,
            new RegistrarTerminoValidator(),
            new RegistrarTerminoConCategoriaNuevaValidator(),
            new AsignarParesValidator(),
            new QuitarParValidator());
    }

    [Fact]
    public async Task Listar_DelegaEnElServicioDeConsultasConLosMismosFiltros()
    {
        IReadOnlyList<TerminoDto> esperado = [new TerminoDto { IdTermino = IdTermino, Termino = "SET" }];
        _consultas.ListarTerminosAsync("se", null, true, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _servicio.ListarAsync("se", null, soloAmbiguos: true);

        resultado.Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task Registrar_ConActorSinAutoridad_LanzaOperacionNoAutorizadaAntesQueValidar()
    {
        var accion = () => _servicio.RegistrarAsync(IdCajero, new RegistrarTerminoDto { Termino = "" });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_TerminoNuevoConCategoria_LoCreaNormalizadoYGuardaUnaVez()
    {
        await _servicio.RegistrarAsync(IdGerente, new RegistrarTerminoDto { Termino = " móchila ", IdCategoria = IdCategoria });

        await _repository.Received(1).AddAsync(
            Arg.Is<TerminoClasificacion>(t => t.Termino == "MOCHILA" && t.IdCategoria == IdCategoria),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_TerminoNuevoSinCategoria_LoCreaAmbiguo()
    {
        await _servicio.RegistrarAsync(IdGerente, new RegistrarTerminoDto { Termino = "SET" });

        await _repository.Received(1).AddAsync(
            Arg.Is<TerminoClasificacion>(t => t.Termino == "SET" && t.EsAmbiguo),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_TerminoExistente_LoReasignaSinCrearOtro()
    {
        var existente = TerminoClasificacion.CrearConCategoria("MOCHILA", IdCategoria);
        ConfigurarExistentes(existente);

        await _servicio.RegistrarAsync(IdGerente, new RegistrarTerminoDto { Termino = "Mochila", IdCategoria = IdOtraCategoria });

        existente.IdCategoria.Should().Be(IdOtraCategoria);
        await _repository.DidNotReceive().AddAsync(Arg.Any<TerminoClasificacion>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(existente, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_TerminoExistenteSinCategoria_LoMarcaAmbiguo()
    {
        var existente = TerminoClasificacion.CrearConCategoria("CINTA", IdCategoria);
        ConfigurarExistentes(existente);

        await _servicio.RegistrarAsync(IdGerente, new RegistrarTerminoDto { Termino = "CINTA", IdCategoria = null });

        existente.EsAmbiguo.Should().BeTrue();
    }

    [Fact]
    public async Task Registrar_ConCategoriaInexistente_LanzaDomainExceptionYNoGuarda()
    {
        var accion = () => _servicio.RegistrarAsync(IdGerente, new RegistrarTerminoDto { Termino = "MOCHILA", IdCategoria = 55 });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la categoría con ID 55.");
        await _repository.DidNotReceive().AddAsync(Arg.Any<TerminoClasificacion>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_ConTerminoDeVariasPalabras_LanzaValidationException()
    {
        var accion = () => _servicio.RegistrarAsync(IdGerente, new RegistrarTerminoDto { Termino = "DOS PALABRAS" });

        await accion.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Registrar_ConCarreraEnElIndiceUnico_TraduceAUnMensajeDeNegocio()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new RestriccionDeUnicidadException());

        var accion = () => _servicio.RegistrarAsync(IdGerente, new RegistrarTerminoDto { Termino = "MOCHILA", IdCategoria = IdCategoria });

        (await accion.Should().ThrowAsync<DomainException>().WithMessage("*término 'MOCHILA' al mismo tiempo*"))
            .WithInnerException<RestriccionDeUnicidadException>();
    }

    [Fact]
    public async Task Baja_ConActorSinAutoridad_LanzaOperacionNoAutorizadaYNoGuarda()
    {
        var accion = () => _servicio.BajaAsync(IdCajero, IdTermino);

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Baja_ConTerminoInexistente_LanzaDomainException()
    {
        var accion = () => _servicio.BajaAsync(IdGerente, IdTermino);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró el término con ID 30.");
    }

    [Fact]
    public async Task Baja_ConTerminoExistente_LoMarcaComoBorradoYGuarda()
    {
        var termino = TerminoClasificacion.CrearAmbiguo("SET");
        termino.Id = IdTermino;
        _repository.GetByIdAsync(IdTermino, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(termino);

        await _servicio.BajaAsync(IdGerente, IdTermino);

        termino.IsDeleted.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(termino, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_CategoriaParaTerminoAmbiguoConPares_DaDeBajaSusPares()
    {
        var existente = TerminoAmbiguoConId(IdTermino, "SET");
        var par = existente.AsignarPar("GEOMETRIA", IdOtraCategoria);
        ConfigurarExistentes(existente);

        await _servicio.RegistrarAsync(IdGerente, new RegistrarTerminoDto { Termino = "SET", IdCategoria = IdCategoria });

        existente.IdCategoria.Should().Be(IdCategoria);
        par.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task ListarPares_DelegaEnElServicioDeConsultasConLosMismosFiltros()
    {
        IReadOnlyList<ParDto> esperado =
            [new ParDto { IdTermino = IdTermino, Termino = "SET", SegundaPalabra = "GEOMETRIA" }];
        _consultas.ListarParesAsync(IdTermino, "geo", true, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _servicio.ListarParesAsync(IdTermino, "geo", incluirIgnorados: true);

        resultado.Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task AsignarPares_ConActorSinAutoridad_LanzaOperacionNoAutorizadaSinConsultarTerminos()
    {
        var accion = () => _servicio.AsignarParesAsync(IdCajero, Lote(Par(IdTermino, "GEOMETRIA", IdCategoria)));

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _repository.DidNotReceive().FindAsync(
            Arg.Any<Expression<Func<TerminoClasificacion, bool>>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarPar_TerminoAmbiguo_CreaElParNormalizadoYGuardaUnaVez()
    {
        var termino = TerminoAmbiguoConId(IdTermino, "SET");
        ConfigurarExistentes(termino);

        await _servicio.AsignarParAsync(IdGerente, Par(IdTermino, "geometría.", IdCategoria));

        termino.Pares.Should().ContainSingle()
            .Which.Should().Match<ParClasificacion>(p => p.SegundaPalabra == "GEOMETRIA" && p.IdCategoria == IdCategoria);
        await _repository.Received(1).UpdateAsync(termino, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarPares_LoteDeDosTerminosConUnIgnorado_LosAplicaYGuardaUnaSolaVez()
    {
        var set = TerminoAmbiguoConId(IdTermino, "SET");
        var kit = TerminoAmbiguoConId(IdOtroTermino, "KIT");
        ConfigurarExistentes(set, kit);

        await _servicio.AsignarParesAsync(
            IdGerente,
            Lote(
                Par(IdTermino, "GEOMETRIA", IdCategoria),
                Par(IdTermino, "REGALO", null),
                Par(IdOtroTermino, "ARTE", IdOtraCategoria)));

        set.Pares.Should().HaveCount(2);
        set.Pares.Single(p => p.SegundaPalabra == "REGALO").EsIgnorado.Should().BeTrue();
        kit.Pares.Should().ContainSingle().Which.IdCategoria.Should().Be(IdOtraCategoria);
        await _repository.Received(1).UpdateAsync(set, Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(kit, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarPares_TerminoInexistente_LanzaDomainExceptionSinModificarLosDemas()
    {
        var set = TerminoAmbiguoConId(IdTermino, "SET");
        ConfigurarExistentes(set);

        var accion = () => _servicio.AsignarParesAsync(
            IdGerente,
            Lote(Par(IdTermino, "GEOMETRIA", IdCategoria), Par(99, "ARTE", IdCategoria)));

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró el término con ID 99.");
        set.Pares.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarPares_TerminoConCategoriaEnElLote_LanzaDomainExceptionSinModificarLosDemas()
    {
        var set = TerminoAmbiguoConId(IdTermino, "SET");
        var mochila = TerminoClasificacion.CrearConCategoria("MOCHILA", IdCategoria);
        mochila.Id = IdOtroTermino;
        ConfigurarExistentes(set, mochila);

        var accion = () => _servicio.AsignarParesAsync(
            IdGerente,
            Lote(Par(IdTermino, "GEOMETRIA", IdCategoria), Par(IdOtroTermino, "ESCOLAR", IdCategoria)));

        await accion.Should().ThrowAsync<DomainException>().WithMessage("*'MOCHILA'*ambiguo*");
        set.Pares.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarPares_CategoriaInexistente_LanzaDomainExceptionSinModificarNada()
    {
        var set = TerminoAmbiguoConId(IdTermino, "SET");
        ConfigurarExistentes(set);

        var accion = () => _servicio.AsignarParesAsync(
            IdGerente,
            Lote(Par(IdTermino, "GEOMETRIA", IdCategoria), Par(IdTermino, "ESCOLAR", 55)));

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la categoría con ID 55.");
        set.Pares.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AsignarPares_MismoParEscritoDeDosFormas_LanzaValidationException()
    {
        var accion = () => _servicio.AsignarParesAsync(
            IdGerente,
            Lote(Par(IdTermino, "geometria", IdCategoria), Par(IdTermino, "GEOMETRÍA.", IdOtraCategoria)));

        await accion.Should().ThrowAsync<ValidationException>().WithMessage("*repite un par*");
    }

    [Fact]
    public async Task AsignarPares_ConCarreraEnElIndiceUnico_TraduceAUnMensajeDeNegocio()
    {
        ConfigurarExistentes(TerminoAmbiguoConId(IdTermino, "SET"));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new RestriccionDeUnicidadException());

        var accion = () => _servicio.AsignarParAsync(IdGerente, Par(IdTermino, "GEOMETRIA", IdCategoria));

        (await accion.Should().ThrowAsync<DomainException>().WithMessage("*pares al mismo tiempo*"))
            .WithInnerException<RestriccionDeUnicidadException>();
    }

    [Fact]
    public async Task QuitarPar_ParExistente_LoDaDeBajaYGuarda()
    {
        var termino = TerminoAmbiguoConId(IdTermino, "SET");
        var par = termino.AsignarPar("GEOMETRIA", IdCategoria);
        _repository.GetByIdAsync(IdTermino, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(termino);

        await _servicio.QuitarParAsync(IdGerente, new QuitarParDto { IdTermino = IdTermino, SegundaPalabra = "geometria" });

        par.IsDeleted.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(termino, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuitarPar_TerminoInexistente_LanzaDomainException()
    {
        var accion = () => _servicio.QuitarParAsync(
            IdGerente,
            new QuitarParDto { IdTermino = IdTermino, SegundaPalabra = "GEOMETRIA" });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró el término con ID 30.");
    }

    [Fact]
    public async Task QuitarPar_ParInexistente_LanzaDomainExceptionYNoGuarda()
    {
        var termino = TerminoAmbiguoConId(IdTermino, "SET");
        _repository.GetByIdAsync(IdTermino, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(termino);

        var accion = () => _servicio.QuitarParAsync(
            IdGerente,
            new QuitarParDto { IdTermino = IdTermino, SegundaPalabra = "GEOMETRIA" });

        await accion.Should().ThrowAsync<DomainException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Pares propuestos (Fase 5, M-49, P-5) ----------

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task ListarParesPropuestos_SoporteEnElBorde_ProponeDesdeDosArticulos(int articulos, bool propone)
    {
        ConfigurarPropuestas(
            [AmbiguoListado(IdTermino, "SET")],
            [],
            Enumerable.Range(1, articulos).Select(i => Articulo($"SET GEOMETRIA {i}", IdCategoria)).ToArray());

        var propuestas = await _servicio.ListarParesPropuestosAsync(null);

        propuestas.Should().HaveCount(propone ? 1 : 0);
    }

    [Theory]
    [InlineData(4, 1, true)]
    [InlineData(3, 1, false)]
    public async Task ListarParesPropuestos_PurezaEnElBorde_ProponeDesdeCeroComaOcho(int mayoria, int otra, bool propone)
    {
        var articulos = Enumerable.Range(1, mayoria).Select(i => Articulo($"SET GEOMETRIA {i}", IdCategoria))
            .Concat(Enumerable.Range(1, otra).Select(i => Articulo($"SET GEOMETRIA OTRA {i}", IdOtraCategoria)))
            .ToArray();
        ConfigurarPropuestas([AmbiguoListado(IdTermino, "SET")], [], articulos);

        var propuestas = await _servicio.ListarParesPropuestosAsync(null);

        if (propone)
        {
            propuestas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                IdTermino,
                Termino = "SET",
                SegundaPalabra = "GEOMETRIA",
                IdCategoria,
                Coincidencias = mayoria,
                Total = mayoria + otra
            });
        }
        else
        {
            propuestas.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ListarParesPropuestos_ParesYaEnElDiccionario_NoLosProponeAunqueEstenIgnorados()
    {
        ConfigurarPropuestas(
            [AmbiguoListado(IdTermino, "SET")],
            [ParListado("SET", "GEOMETRIA", IdCategoria), ParListado("SET", "REGALO", null)],
            Articulo("SET GEOMETRIA 1", IdCategoria),
            Articulo("SET GEOMETRIA 2", IdCategoria),
            Articulo("SET REGALO 1", IdCategoria),
            Articulo("SET REGALO 2", IdCategoria));

        (await _servicio.ListarParesPropuestosAsync(null)).Should().BeEmpty();
    }

    [Fact]
    public async Task ListarParesPropuestos_DescripcionesConPrefijoTildesYSobranteDelPreFiltro_AplicaLaReglaExacta()
    {
        ConfigurarPropuestas(
            [AmbiguoListado(IdTermino, "SET"), AmbiguoListado(IdOtroTermino, "NANDU")],
            [],
            Articulo("**set de geometría.", IdCategoria),
            Articulo("SET  GEOMETRIA 4 PIEZAS", IdCategoria),
            Articulo("CASETE GEOMETRIA", IdOtraCategoria),
            Articulo("CASETE GEOMETRIA", IdOtraCategoria),
            Articulo("Ñandú escolar", IdOtraCategoria),
            Articulo("ÑANDU ESCOLAR 2", IdOtraCategoria));

        var propuestas = await _servicio.ListarParesPropuestosAsync(null);

        propuestas.Select(p => (p.Termino, p.SegundaPalabra, p.Coincidencias, p.Total))
            .Should().BeEquivalentTo(new[] { ("NANDU", "ESCOLAR", 2, 2), ("SET", "GEOMETRIA", 2, 2) });
    }

    [Fact]
    public async Task ListarParesPropuestos_DescripcionesSinSegundaPalabra_NoCuentan()
    {
        ConfigurarPropuestas(
            [AmbiguoListado(IdTermino, "SET")],
            [],
            Articulo("SET", IdCategoria),
            Articulo("SET DE", IdCategoria),
            Articulo("SET DE LA", IdCategoria));

        (await _servicio.ListarParesPropuestosAsync(null)).Should().BeEmpty();
    }

    [Fact]
    public async Task ListarParesPropuestos_ConIdTermino_ConsultaSoloLosArticulosDeEseTermino()
    {
        ConfigurarPropuestas(
            [AmbiguoListado(IdTermino, "SET"), AmbiguoListado(IdOtroTermino, "KIT")],
            [],
            Articulo("SET GEOMETRIA 1", IdCategoria),
            Articulo("SET GEOMETRIA 2", IdCategoria));

        await _servicio.ListarParesPropuestosAsync(IdOtroTermino);

        await _consultas.Received(1).ListarArticulosConTerminosAsync(
            Arg.Is<IReadOnlyCollection<string>>(t => t.SequenceEqual(SoloKit)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarParesPropuestos_SinTerminosAmbiguos_DevuelveVacioSinConsultarArticulos()
    {
        ConfigurarPropuestas([], [], Articulo("SET GEOMETRIA", IdCategoria));

        (await _servicio.ListarParesPropuestosAsync(null)).Should().BeEmpty();
        await _consultas.DidNotReceive().ListarArticulosConTerminosAsync(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarParesPropuestos_VariasPropuestas_OrdenaPorEvidenciaYMuestraHastaTresEjemplosDeLaMayoria()
    {
        ConfigurarPropuestas(
            [AmbiguoListado(IdTermino, "SET")],
            [],
            Articulo("SET ESCOLAR 1", IdOtraCategoria),
            Articulo("SET ESCOLAR 2", IdOtraCategoria),
            Articulo("SET GEOMETRIA 1", IdCategoria),
            Articulo("SET GEOMETRIA 2", IdCategoria),
            Articulo("SET GEOMETRIA 3", IdCategoria),
            Articulo("SET GEOMETRIA 4", IdCategoria),
            Articulo("SET GEOMETRIA 5", IdOtraCategoria));

        var propuestas = await _servicio.ListarParesPropuestosAsync(null);

        propuestas.Select(p => p.SegundaPalabra).Should().Equal("GEOMETRIA", "ESCOLAR");
        propuestas[0].Ejemplos.Should().Equal("SET GEOMETRIA 1", "SET GEOMETRIA 2", "SET GEOMETRIA 3");
        propuestas[0].NombreCategoria.Should().Be($"Categoría {IdCategoria}");
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_ConActorSinAutoridad_LanzaOperacionNoAutorizadaYNoAbreTransaccion()
    {
        var accion = () => _servicio.RegistrarConCategoriaNuevaAsync(IdCajero, ConCategoriaNueva("PELOTERO", "Pelotas"));

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_SinRubro_LanzaValidationExceptionYNoAbreTransaccion()
    {
        var dto = ConCategoriaNueva("PELOTERO", "Pelotas") with { IdRubro = 0 };

        var accion = () => _servicio.RegistrarConCategoriaNuevaAsync(IdGerente, dto);

        await accion.Should().ThrowAsync<ValidationException>().WithMessage("*rubro*");
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_TerminoNuevo_CreaLaCategoriaYElTerminoQueLaApuntaYConfirma()
    {
        AsignarIdAlGuardarLaCategoria();

        var categoria = await _servicio.RegistrarConCategoriaNuevaAsync(
            IdGerente,
            ConCategoriaNueva(" pelotero ", " Pelotas "));

        categoria.IdCategoria.Should().Be(IdCategoriaNueva);
        categoria.NombreCategoria.Should().Be("Pelotas");
        categoria.IdRubro.Should().Be(IdRubro);
        await _repository.Received(1).AddAsync(
            Arg.Is<TerminoClasificacion>(t => t.Termino == "PELOTERO" && t.IdCategoria == IdCategoriaNueva),
            Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            _ = _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _ = _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _ = _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _ = _unitOfWork.CommitTransactionAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_TerminoAmbiguoExistente_LoReasignaALaCategoriaNueva()
    {
        AsignarIdAlGuardarLaCategoria();
        var existente = TerminoAmbiguoConId(IdTermino, "PELOTERO");
        existente.AsignarPar("PLAYERO", IdCategoria);
        ConfigurarExistentes(existente);

        await _servicio.RegistrarConCategoriaNuevaAsync(IdGerente, ConCategoriaNueva("PELOTERO", "Pelotas"));

        existente.IdCategoria.Should().Be(IdCategoriaNueva);
        existente.Pares.Should().OnlyContain(p => p.IsDeleted, "un término con categoría no tiene pares (P-3)");
        await _repository.Received(1).UpdateAsync(existente, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<TerminoClasificacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_NombreDeCategoriaRepetido_LanzaDomainExceptionYDeshace()
    {
        var accion = () => _servicio.RegistrarConCategoriaNuevaAsync(
            IdGerente,
            ConCategoriaNueva("PELOTERO", $"Categoría {IdCategoria}"));

        await accion.Should().ThrowAsync<DomainException>().WithMessage("Ya existe una categoría activa*");
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_RubroInexistente_LanzaDomainExceptionYDeshace()
    {
        var dto = ConCategoriaNueva("PELOTERO", "Pelotas") with { IdRubro = 55 };

        var accion = () => _servicio.RegistrarConCategoriaNuevaAsync(IdGerente, dto);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró el rubro con ID 55.");
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _categorias.DidNotReceive().AddAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarConCategoriaNueva_OtroUsuarioRegistraElTerminoAlMismoTiempo_TraduceLaCarreraYDeshace()
    {
        Categoria? agregada = null;
        _categorias.When(x => x.AddAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>()))
            .Do(llamada => agregada = llamada.Arg<Categoria>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(
            _ =>
            {
                agregada!.Id = IdCategoriaNueva;
                return 1;
            },
            _ => throw new RestriccionDeUnicidadException("índice único", new InvalidOperationException()));

        var accion = () => _servicio.RegistrarConCategoriaNuevaAsync(IdGerente, ConCategoriaNueva("PELOTERO", "Pelotas"));

        await accion.Should().ThrowAsync<DomainException>()
            .WithMessage("Otro usuario registró el término 'PELOTERO' al mismo tiempo*");
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        _unitOfWork.Received(1).DescartarCambios();
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    private void ConfigurarPropuestas(
        TerminoDto[] ambiguos,
        ParDto[] enElDiccionario,
        params DescripcionClasificadaDto[] articulos)
    {
        _consultas.ListarTerminosAsync(null, null, true, Arg.Any<CancellationToken>()).Returns(ambiguos);
        _consultas.ListarParesAsync(Arg.Any<int?>(), null, true, Arg.Any<CancellationToken>()).Returns(enElDiccionario);
        _consultas.ListarArticulosConTerminosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(articulos);
    }

    private static TerminoDto AmbiguoListado(int id, string termino)
    {
        return new TerminoDto { IdTermino = id, Termino = termino };
    }

    private static ParDto ParListado(string termino, string segundaPalabra, int? idCategoria)
    {
        return new ParDto { IdTermino = IdTermino, Termino = termino, SegundaPalabra = segundaPalabra, IdCategoria = idCategoria };
    }

    private static DescripcionClasificadaDto Articulo(string descripcion, int idCategoria)
    {
        return new DescripcionClasificadaDto
        {
            Descripcion = descripcion,
            IdCategoria = idCategoria,
            NombreCategoria = $"Categoría {idCategoria}"
        };
    }

    private static AsignarParDto Par(int idTermino, string segundaPalabra, int? idCategoria)
    {
        return new AsignarParDto { IdTermino = idTermino, SegundaPalabra = segundaPalabra, IdCategoria = idCategoria };
    }

    private static AsignarParesDto Lote(params AsignarParDto[] pares)
    {
        return new AsignarParesDto { Pares = pares };
    }

    private void RegistrarUsuario(int id, RolUsuarioEnum rol)
    {
        var email = rol == RolUsuarioEnum.Cajero ? null : $"usuario{id}@libreria.com";
        var usuario = Usuario.Crear($"usuario{id}", "Juan", "Pérez", "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    private static RegistrarTerminoConCategoriaNuevaDto ConCategoriaNueva(string termino, string nombreCategoria)
    {
        return new RegistrarTerminoConCategoriaNuevaDto
        {
            Termino = termino,
            NombreCategoria = nombreCategoria,
            IdRubro = IdRubro
        };
    }

    // Lo que hace la base al guardar: la categoría nueva recibe su Id (identidad de SQL Server).
    private void AsignarIdAlGuardarLaCategoria()
    {
        Categoria? agregada = null;
        _categorias.When(x => x.AddAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>()))
            .Do(llamada => agregada = llamada.Arg<Categoria>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (agregada is { Id: 0 })
            {
                agregada.Id = IdCategoriaNueva;
            }

            return 1;
        });
    }

    private void RegistrarRubro(int id)
    {
        var rubro = Rubro.Crear("Juguetería");
        rubro.Id = id;
        _rubros.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(rubro);
    }

    private void RegistrarCategoria(int id)
    {
        var categoria = Categoria.Crear($"Categoría {id}", 1);
        categoria.Id = id;
        _categorias.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(categoria);
        _categoriasActivas.Add(categoria);
        _categorias.FindAsync(Arg.Any<Expression<Func<Categoria, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(llamada => (IReadOnlyList<Categoria>)Filtrar(_categoriasActivas, llamada.Arg<Expression<Func<Categoria, bool>>>()));
    }

    // Aplica el predicado como lo haría el repositorio: el registro busca por texto y los pares, por Id.
    private void ConfigurarExistentes(params TerminoClasificacion[] existentes)
    {
        _repository.FindAsync(Arg.Any<Expression<Func<TerminoClasificacion, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(llamada => (IReadOnlyList<TerminoClasificacion>)Filtrar(existentes, llamada.Arg<Expression<Func<TerminoClasificacion, bool>>>()));
    }

    private static TerminoClasificacion TerminoAmbiguoConId(int id, string termino)
    {
        var entidad = TerminoClasificacion.CrearAmbiguo(termino);
        entidad.Id = id;
        return entidad;
    }

    private static List<T> Filtrar<T>(IEnumerable<T> elementos, Expression<Func<T, bool>> predicado)
    {
        return elementos.Where(predicado.Compile()).ToList();
    }
}
