using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Articulos;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class CategoriaServiceTests
{
    private const int IdGerente = 1;
    private const int IdEncargado = 2;
    private const int IdCajero = 3;
    private const int IdAdministrador = 4;
    private const int IdInexistente = 99;
    private const int IdCategoriaExistente = 7;
    private const int IdRubroExistente = 3;
    private const int IdRubroInexistente = 55;

    private readonly IRepository<Categoria> _repository = Substitute.For<IRepository<Categoria>>();
    private readonly IRepository<Rubro> _rubros = Substitute.For<IRepository<Rubro>>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IClasificacionQueryService _consultas = Substitute.For<IClasificacionQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CategoriaService _servicio;

    public CategoriaServiceTests()
    {
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdEncargado, RolUsuarioEnum.Encargado);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);
        RegistrarUsuario(IdAdministrador, RolUsuarioEnum.Administrador);

        ConfigurarDuplicados();
        ConfigurarRubro(IdRubroExistente, "Librería");

        _servicio = new CategoriaService(
            _repository,
            _rubros,
            _usuarios,
            _consultas,
            _unitOfWork,
            new CrearCategoriaValidator(),
            new ActualizarCategoriaValidator());
    }

    private void RegistrarUsuario(int id, RolUsuarioEnum rol)
    {
        var email = rol == RolUsuarioEnum.Cajero ? null : $"usuario{id}@libreria.com";
        var usuario = Usuario.Crear($"usuario{id}", "Juan", "Pérez", "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    private void ConfigurarDuplicados(params Categoria[] existentes)
    {
        IReadOnlyList<Categoria> lista = existentes.ToList();
        _repository.FindAsync(Arg.Any<Expression<Func<Categoria, bool>>>(), false, Arg.Any<CancellationToken>()).Returns(lista);
    }

    private void ConfigurarRubro(int id, string nombre)
    {
        var rubro = Rubro.Crear(nombre);
        rubro.Id = id;
        _rubros.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(rubro);
    }

    private static Categoria CrearCategoriaConId(int id, string nombre)
    {
        var entidad = Categoria.Crear(nombre, IdRubroExistente);
        entidad.Id = id;
        return entidad;
    }

    private void ConfigurarExistente(Categoria entidad)
    {
        _repository.GetByIdAsync(entidad.Id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(entidad);
    }

    // ---------- Listar ----------

    [Fact]
    public async Task Listar_ConFiltro_DelegaEnElServicioDeConsultasConElMismoFiltro()
    {
        IReadOnlyList<CategoriaDto> esperado = new List<CategoriaDto>
        {
            new() { IdCategoria = 1, NombreCategoria = "Uno", CantidadEnUso = 2 }
        };
        _consultas.ListarCategoriasAsync("un", null, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _servicio.ListarAsync("un", null);

        resultado.Should().BeSameAs(esperado);
        await _consultas.Received(1).ListarCategoriasAsync("un", null, Arg.Any<CancellationToken>());
    }

    // ---------- Crear ----------

    [Theory]
    [InlineData(IdCajero)]
    [InlineData(IdAdministrador)]
    [InlineData(IdInexistente)]
    [InlineData(0)]
    public async Task Crear_ConActorSinAutoridad_LanzaOperacionNoAutorizadaYNoAgrega(int idActor)
    {
        var accion = () => _servicio.CrearAsync(idActor, new CrearCategoriaDto { Nombre = "Nuevo", IdRubro = IdRubroExistente });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _repository.DidNotReceive().AddAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_ConActorSinAutoridadYNombreInvalido_LanzaOperacionNoAutorizadaAntesQueValidar()
    {
        var accion = () => _servicio.CrearAsync(IdCajero, new CrearCategoriaDto { Nombre = "", IdRubro = IdRubroExistente });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
    }

    [Theory]
    [InlineData(IdGerente)]
    [InlineData(IdEncargado)]
    public async Task Crear_ConGerenteOEncargado_CreaConNombreNormalizadoYGuardaUnaVez(int idActor)
    {
        var resultado = await _servicio.CrearAsync(idActor, new CrearCategoriaDto { Nombre = "  Nombre   compuesto ", IdRubro = IdRubroExistente });

        resultado.NombreCategoria.Should().Be("Nombre compuesto");
        resultado.IdRubro.Should().Be(IdRubroExistente);
        resultado.NombreRubro.Should().Be("Librería");
        resultado.CantidadEnUso.Should().Be(0);
        await _repository.Received(1).AddAsync(
            Arg.Is<Categoria>(x => x.NombreCategoria == "Nombre compuesto" && x.IdRubro == IdRubroExistente),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_ConNombreInvalido_LanzaValidationExceptionYNoAgrega()
    {
        var accion = () => _servicio.CrearAsync(IdGerente, new CrearCategoriaDto { Nombre = "   ", IdRubro = IdRubroExistente });

        await accion.Should().ThrowAsync<ValidationException>();
        await _repository.DidNotReceive().AddAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_ConRubroInexistente_LanzaDomainExceptionYNoAgrega()
    {
        var accion = () => _servicio.CrearAsync(IdGerente, new CrearCategoriaDto { Nombre = "Nuevo", IdRubro = IdRubroInexistente });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró el rubro con ID 55.");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_ConRubroNoPositivo_LanzaValidationExceptionYNoAgrega()
    {
        var accion = () => _servicio.CrearAsync(IdGerente, new CrearCategoriaDto { Nombre = "Nuevo", IdRubro = 0 });

        await accion.Should().ThrowAsync<ValidationException>();
        await _repository.DidNotReceive().AddAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_ConNombreDuplicado_LanzaDomainExceptionYNoAgrega()
    {
        ConfigurarDuplicados(CrearCategoriaConId(IdCategoriaExistente, "Repetido"));

        var accion = () => _servicio.CrearAsync(IdGerente, new CrearCategoriaDto { Nombre = "Repetido", IdRubro = IdRubroExistente });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("Ya existe una categoría activa con el nombre 'Repetido'.");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Actualizar ----------

    [Theory]
    [InlineData(IdCajero)]
    [InlineData(IdAdministrador)]
    [InlineData(IdInexistente)]
    [InlineData(0)]
    public async Task Actualizar_ConActorSinAutoridad_LanzaOperacionNoAutorizadaYNoGuarda(int idActor)
    {
        var accion = () => _servicio.ActualizarAsync(idActor, new ActualizarCategoriaDto { IdCategoria = IdCategoriaExistente, Nombre = "Otro", IdRubro = IdRubroExistente });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Actualizar_ConNombreInvalido_LanzaValidationException()
    {
        var accion = () => _servicio.ActualizarAsync(IdGerente, new ActualizarCategoriaDto { IdCategoria = IdCategoriaExistente, Nombre = "", IdRubro = IdRubroExistente });

        await accion.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Actualizar_ConElementoInexistente_LanzaDomainException()
    {
        var accion = () => _servicio.ActualizarAsync(IdGerente, new ActualizarCategoriaDto { IdCategoria = IdCategoriaExistente, Nombre = "Otro", IdRubro = IdRubroExistente });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la categoría con ID 7.");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Actualizar_ConNombreDeOtroElemento_LanzaDomainExceptionYNoGuarda()
    {
        ConfigurarExistente(CrearCategoriaConId(IdCategoriaExistente, "Original"));
        ConfigurarDuplicados(CrearCategoriaConId(8, "Ocupado"));

        var accion = () => _servicio.ActualizarAsync(IdGerente, new ActualizarCategoriaDto { IdCategoria = IdCategoriaExistente, Nombre = "Ocupado", IdRubro = IdRubroExistente });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("Ya existe una categoría activa con el nombre 'Ocupado'.");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Actualizar_AlBuscarDuplicados_ExcluyeAlPropioElementoPorId()
    {
        ConfigurarExistente(CrearCategoriaConId(IdCategoriaExistente, "Original"));
        Expression<Func<Categoria, bool>>? predicado = null;
        _repository.FindAsync(
                Arg.Do<Expression<Func<Categoria, bool>>>(p => predicado = p),
                false,
                Arg.Any<CancellationToken>())
            .Returns(new List<Categoria>());

        await _servicio.ActualizarAsync(IdGerente, new ActualizarCategoriaDto { IdCategoria = IdCategoriaExistente, Nombre = "Original", IdRubro = IdRubroExistente });

        predicado.Should().NotBeNull();
        var evaluar = predicado!.Compile();
        evaluar(CrearCategoriaConId(IdCategoriaExistente, "Original")).Should().BeFalse("el propio elemento no cuenta como duplicado");
        evaluar(CrearCategoriaConId(8, "Original")).Should().BeTrue("otro elemento con el mismo nombre sí es duplicado");
    }

    [Theory]
    [InlineData(IdGerente)]
    [InlineData(IdEncargado)]
    public async Task Actualizar_ConDatosValidos_CambiaElNombreYGuarda(int idActor)
    {
        var entidad = CrearCategoriaConId(IdCategoriaExistente, "Original");
        ConfigurarExistente(entidad);

        await _servicio.ActualizarAsync(idActor, new ActualizarCategoriaDto { IdCategoria = IdCategoriaExistente, Nombre = "  Nuevo   nombre ", IdRubro = IdRubroExistente });

        entidad.NombreCategoria.Should().Be("Nuevo nombre");
        await _repository.Received(1).UpdateAsync(entidad, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Actualizar_ConRubroInexistente_LanzaDomainExceptionYNoGuarda()
    {
        var entidad = CrearCategoriaConId(IdCategoriaExistente, "Original");
        ConfigurarExistente(entidad);

        var accion = () => _servicio.ActualizarAsync(
            IdGerente,
            new ActualizarCategoriaDto { IdCategoria = IdCategoriaExistente, Nombre = "Original", IdRubro = IdRubroInexistente });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró el rubro con ID 55.");
        entidad.IdRubro.Should().Be(IdRubroExistente);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Actualizar_ConOtroRubro_MueveLaCategoriaYGuarda()
    {
        const int idRubroDestino = 9;
        ConfigurarRubro(idRubroDestino, "Bazar");
        var entidad = CrearCategoriaConId(IdCategoriaExistente, "Original");
        ConfigurarExistente(entidad);

        await _servicio.ActualizarAsync(
            IdGerente,
            new ActualizarCategoriaDto { IdCategoria = IdCategoriaExistente, Nombre = "Original", IdRubro = idRubroDestino });

        entidad.IdRubro.Should().Be(idRubroDestino);
        await _repository.Received(1).UpdateAsync(entidad, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Baja ----------

    [Theory]
    [InlineData(IdCajero)]
    [InlineData(IdAdministrador)]
    [InlineData(IdInexistente)]
    [InlineData(0)]
    public async Task Baja_ConActorSinAutoridad_LanzaOperacionNoAutorizadaSinConsultarElConteo(int idActor)
    {
        var accion = () => _servicio.BajaAsync(idActor, IdCategoriaExistente);

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _consultas.DidNotReceive().ContarArticulosPorCategoriaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Baja_ConElementoInexistente_LanzaDomainException()
    {
        var accion = () => _servicio.BajaAsync(IdGerente, IdCategoriaExistente);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la categoría con ID 7.");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Baja_ConElementoEnUso_LanzaClasificacionEnUsoYNoDaDeBaja()
    {
        var entidad = CrearCategoriaConId(IdCategoriaExistente, "Ocupado");
        ConfigurarExistente(entidad);
        _consultas.ContarArticulosPorCategoriaAsync(IdCategoriaExistente, Arg.Any<CancellationToken>()).Returns(3);

        var accion = () => _servicio.BajaAsync(IdGerente, IdCategoriaExistente);

        var excepcion = (await accion.Should().ThrowAsync<ClasificacionEnUsoException>()).Which;
        excepcion.CantidadEnUso.Should().Be(3);
        excepcion.Nombre.Should().Be("Ocupado");
        excepcion.Tipo.Should().Be("la categoría");
        excepcion.Dependientes.Should().Be("artículos");
        entidad.IsDeleted.Should().BeFalse();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Categoria>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(IdGerente)]
    [InlineData(IdEncargado)]
    public async Task Baja_ConElementoLibre_LoMarcaComoBorradoYGuardaUnaVez(int idActor)
    {
        var entidad = CrearCategoriaConId(IdCategoriaExistente, "Libre");
        ConfigurarExistente(entidad);
        _consultas.ContarArticulosPorCategoriaAsync(IdCategoriaExistente, Arg.Any<CancellationToken>()).Returns(0);

        await _servicio.BajaAsync(idActor, IdCategoriaExistente);

        entidad.IsDeleted.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(entidad, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
