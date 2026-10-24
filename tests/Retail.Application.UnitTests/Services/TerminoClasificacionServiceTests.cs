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
/// término único en el catálogo, término ambiguo y baja.
/// </summary>
public class TerminoClasificacionServiceTests
{
    private const int IdGerente = 1;
    private const int IdCajero = 3;
    private const int IdCategoria = 7;
    private const int IdOtraCategoria = 8;
    private const int IdTermino = 30;

    private readonly IRepository<TerminoClasificacion> _repository = Substitute.For<IRepository<TerminoClasificacion>>();
    private readonly IRepository<Categoria> _categorias = Substitute.For<IRepository<Categoria>>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IClasificacionQueryService _consultas = Substitute.For<IClasificacionQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TerminoClasificacionService _servicio;

    public TerminoClasificacionServiceTests()
    {
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);
        RegistrarCategoria(IdCategoria);
        RegistrarCategoria(IdOtraCategoria);
        ConfigurarExistentes();

        _servicio = new TerminoClasificacionService(
            _repository,
            _categorias,
            _usuarios,
            _consultas,
            _unitOfWork,
            new RegistrarTerminoValidator());
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

    private void RegistrarUsuario(int id, RolUsuarioEnum rol)
    {
        var email = rol == RolUsuarioEnum.Cajero ? null : $"usuario{id}@libreria.com";
        var usuario = Usuario.Crear($"usuario{id}", "Juan", "Pérez", "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    private void RegistrarCategoria(int id)
    {
        var categoria = Categoria.Crear($"Categoría {id}", 1);
        categoria.Id = id;
        _categorias.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(categoria);
    }

    private void ConfigurarExistentes(params TerminoClasificacion[] existentes)
    {
        IReadOnlyList<TerminoClasificacion> lista = existentes.ToList();
        _repository.FindAsync(Arg.Any<Expression<Func<TerminoClasificacion, bool>>>(), false, Arg.Any<CancellationToken>())
            .Returns(lista);
    }
}
