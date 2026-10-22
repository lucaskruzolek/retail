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

public class MarcaServiceTests
{
    private const int IdGerente = 1;
    private const int IdEncargado = 2;
    private const int IdCajero = 3;
    private const int IdAdministrador = 4;
    private const int IdInexistente = 99;
    private const int IdMarcaExistente = 7;

    private readonly IRepository<Marca> _repository = Substitute.For<IRepository<Marca>>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IClasificacionQueryService _consultas = Substitute.For<IClasificacionQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly MarcaService _servicio;

    public MarcaServiceTests()
    {
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdEncargado, RolUsuarioEnum.Encargado);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);
        RegistrarUsuario(IdAdministrador, RolUsuarioEnum.Administrador);

        ConfigurarDuplicados();

        _servicio = new MarcaService(
            _repository,
            _usuarios,
            _consultas,
            _unitOfWork,
            new CrearMarcaValidator(),
            new RenombrarMarcaValidator());
    }

    private void RegistrarUsuario(int id, RolUsuarioEnum rol)
    {
        var email = rol == RolUsuarioEnum.Cajero ? null : $"usuario{id}@libreria.com";
        var usuario = Usuario.Crear($"usuario{id}", "Juan", "Pérez", "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    private void ConfigurarDuplicados(params Marca[] existentes)
    {
        IReadOnlyList<Marca> lista = existentes.ToList();
        _repository.FindAsync(Arg.Any<Expression<Func<Marca, bool>>>(), false, Arg.Any<CancellationToken>()).Returns(lista);
    }

    private static Marca CrearMarcaConId(int id, string nombre)
    {
        var entidad = Marca.Crear(nombre);
        entidad.Id = id;
        return entidad;
    }

    private void ConfigurarExistente(Marca entidad)
    {
        _repository.GetByIdAsync(entidad.Id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(entidad);
        _repository.GetByIdWithIncludesAsync(
                entidad.Id,
                Arg.Any<CancellationToken>(),
                Arg.Any<Expression<Func<Marca, object>>[]>())
            .Returns(entidad);
    }

    // ---------- Listar ----------

    [Fact]
    public async Task Listar_ConFiltro_DelegaEnElServicioDeConsultasConElMismoFiltro()
    {
        IReadOnlyList<MarcaDto> esperado = new List<MarcaDto>
        {
            new() { IdMarca = 1, NombreMarca = "Uno", CantidadEnUso = 2 }
        };
        _consultas.ListarMarcasAsync("un", Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _servicio.ListarAsync("un");

        resultado.Should().BeSameAs(esperado);
        await _consultas.Received(1).ListarMarcasAsync("un", Arg.Any<CancellationToken>());
    }

    // ---------- Crear ----------

    [Theory]
    [InlineData(IdCajero)]
    [InlineData(IdAdministrador)]
    [InlineData(IdInexistente)]
    [InlineData(0)]
    public async Task Crear_ConActorSinAutoridad_LanzaOperacionNoAutorizadaYNoAgrega(int idActor)
    {
        var accion = () => _servicio.CrearAsync(idActor, new CrearMarcaDto { Nombre = "Nuevo" });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _repository.DidNotReceive().AddAsync(Arg.Any<Marca>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_ConActorSinAutoridadYNombreInvalido_LanzaOperacionNoAutorizadaAntesQueValidar()
    {
        var accion = () => _servicio.CrearAsync(IdCajero, new CrearMarcaDto { Nombre = "" });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
    }

    [Theory]
    [InlineData(IdGerente)]
    [InlineData(IdEncargado)]
    public async Task Crear_ConGerenteOEncargado_CreaConNombreNormalizadoYGuardaUnaVez(int idActor)
    {
        var resultado = await _servicio.CrearAsync(idActor, new CrearMarcaDto { Nombre = "  Nombre   compuesto " });

        resultado.NombreMarca.Should().Be("Nombre compuesto");
        resultado.CantidadEnUso.Should().Be(0);
        await _repository.Received(1).AddAsync(
            Arg.Is<Marca>(x => x.NombreMarca == "Nombre compuesto"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_ConNombreInvalido_LanzaValidationExceptionYNoAgrega()
    {
        var accion = () => _servicio.CrearAsync(IdGerente, new CrearMarcaDto { Nombre = "   " });

        await accion.Should().ThrowAsync<ValidationException>();
        await _repository.DidNotReceive().AddAsync(Arg.Any<Marca>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_ConNombreDuplicado_LanzaDomainExceptionYNoAgrega()
    {
        ConfigurarDuplicados(CrearMarcaConId(IdMarcaExistente, "Repetido"));

        var accion = () => _servicio.CrearAsync(IdGerente, new CrearMarcaDto { Nombre = "Repetido" });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("Ya existe una marca activa con el nombre 'Repetido'.");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Marca>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Renombrar ----------

    [Theory]
    [InlineData(IdCajero)]
    [InlineData(IdAdministrador)]
    [InlineData(IdInexistente)]
    [InlineData(0)]
    public async Task Renombrar_ConActorSinAutoridad_LanzaOperacionNoAutorizadaYNoGuarda(int idActor)
    {
        var accion = () => _servicio.RenombrarAsync(idActor, new RenombrarMarcaDto { IdMarca = IdMarcaExistente, Nombre = "Otro" });

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renombrar_ConNombreInvalido_LanzaValidationException()
    {
        var accion = () => _servicio.RenombrarAsync(IdGerente, new RenombrarMarcaDto { IdMarca = IdMarcaExistente, Nombre = "" });

        await accion.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Renombrar_ConElementoInexistente_LanzaDomainException()
    {
        var accion = () => _servicio.RenombrarAsync(IdGerente, new RenombrarMarcaDto { IdMarca = IdMarcaExistente, Nombre = "Otro" });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la marca con ID 7.");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renombrar_ConNombreDeOtroElemento_LanzaDomainExceptionYNoGuarda()
    {
        ConfigurarExistente(CrearMarcaConId(IdMarcaExistente, "Original"));
        ConfigurarDuplicados(CrearMarcaConId(8, "Ocupado"));

        var accion = () => _servicio.RenombrarAsync(IdGerente, new RenombrarMarcaDto { IdMarca = IdMarcaExistente, Nombre = "Ocupado" });

        await accion.Should().ThrowAsync<DomainException>().WithMessage("Ya existe una marca activa con el nombre 'Ocupado'.");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Marca>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renombrar_AlBuscarDuplicados_ExcluyeAlPropioElementoPorId()
    {
        ConfigurarExistente(CrearMarcaConId(IdMarcaExistente, "Original"));
        Expression<Func<Marca, bool>>? predicado = null;
        _repository.FindAsync(
                Arg.Do<Expression<Func<Marca, bool>>>(p => predicado = p),
                false,
                Arg.Any<CancellationToken>())
            .Returns(new List<Marca>());

        await _servicio.RenombrarAsync(IdGerente, new RenombrarMarcaDto { IdMarca = IdMarcaExistente, Nombre = "Original" });

        predicado.Should().NotBeNull();
        var evaluar = predicado!.Compile();
        evaluar(CrearMarcaConId(IdMarcaExistente, "Original")).Should().BeFalse("el propio elemento no cuenta como duplicado");
        evaluar(CrearMarcaConId(8, "Original")).Should().BeTrue("otro elemento con el mismo nombre sí es duplicado");
    }

    [Theory]
    [InlineData(IdGerente)]
    [InlineData(IdEncargado)]
    public async Task Renombrar_ConDatosValidos_CambiaElNombreYGuarda(int idActor)
    {
        var entidad = CrearMarcaConId(IdMarcaExistente, "Original");
        ConfigurarExistente(entidad);

        await _servicio.RenombrarAsync(idActor, new RenombrarMarcaDto { IdMarca = IdMarcaExistente, Nombre = "  Nuevo   nombre " });

        entidad.NombreMarca.Should().Be("Nuevo nombre");
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
        var accion = () => _servicio.BajaAsync(idActor, IdMarcaExistente);

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _consultas.DidNotReceive().ContarArticulosPorMarcaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Baja_ConElementoInexistente_LanzaDomainException()
    {
        var accion = () => _servicio.BajaAsync(IdGerente, IdMarcaExistente);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la marca con ID 7.");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Baja_ConElementoEnUso_LanzaClasificacionEnUsoYNoDaDeBaja()
    {
        var entidad = CrearMarcaConId(IdMarcaExistente, "Ocupado");
        ConfigurarExistente(entidad);
        _consultas.ContarArticulosPorMarcaAsync(IdMarcaExistente, Arg.Any<CancellationToken>()).Returns(3);

        var accion = () => _servicio.BajaAsync(IdGerente, IdMarcaExistente);

        var excepcion = (await accion.Should().ThrowAsync<ClasificacionEnUsoException>()).Which;
        excepcion.CantidadEnUso.Should().Be(3);
        excepcion.Nombre.Should().Be("Ocupado");
        excepcion.Tipo.Should().Be("la marca");
        excepcion.Dependientes.Should().Be("artículos");
        entidad.IsDeleted.Should().BeFalse();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Marca>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(IdGerente)]
    [InlineData(IdEncargado)]
    public async Task Baja_ConElementoLibre_LoMarcaComoBorradoYGuardaUnaVez(int idActor)
    {
        var entidad = CrearMarcaConId(IdMarcaExistente, "Libre");
        ConfigurarExistente(entidad);
        _consultas.ContarArticulosPorMarcaAsync(IdMarcaExistente, Arg.Any<CancellationToken>()).Returns(0);

        await _servicio.BajaAsync(idActor, IdMarcaExistente);

        entidad.IsDeleted.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(entidad, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Baja_ConAliasYPrefijos_LosCargaYLosDaDeBajaConLaMarca()
    {
        var entidad = CrearMarcaConId(IdMarcaExistente, "Mooving");
        var alias = entidad.AgregarAlias("MOOV");
        var prefijo = entidad.AgregarPrefijo("7798100");
        ConfigurarExistente(entidad);
        _consultas.ContarArticulosPorMarcaAsync(IdMarcaExistente, Arg.Any<CancellationToken>()).Returns(0);

        await _servicio.BajaAsync(IdGerente, IdMarcaExistente);

        alias.IsDeleted.Should().BeTrue();
        prefijo.IsDeleted.Should().BeTrue();
        await _repository.Received(1).GetByIdWithIncludesAsync(
            IdMarcaExistente,
            Arg.Any<CancellationToken>(),
            Arg.Is<Expression<Func<Marca, object>>[]>(includes => includes.Length == 2));
    }
}
