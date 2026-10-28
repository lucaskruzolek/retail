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
/// Casos de uso de las equivalencias de un proveedor (Track 6.6, M-13, M-32): se modifican solo a través de la raíz
/// <see cref="Proveedor"/> (Ley 2), con autorización D-5 y destino activo.
/// </summary>
public class EquivalenciaClasificacionServiceTests
{
    private const int IdGerente = 1;
    private const int IdCajero = 3;
    private const int IdProveedor = 20;
    private const int IdCategoria = 7;
    private const int IdMarca = 9;

    private readonly IRepository<Proveedor> _proveedores = Substitute.For<IRepository<Proveedor>>();
    private readonly IRepository<Categoria> _categorias = Substitute.For<IRepository<Categoria>>();
    private readonly IRepository<Marca> _marcas = Substitute.For<IRepository<Marca>>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IClasificacionQueryService _consultas = Substitute.For<IClasificacionQueryService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Proveedor _proveedor;
    private readonly EquivalenciaClasificacionService _servicio;

    public EquivalenciaClasificacionServiceTests()
    {
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);

        var categoria = Categoria.Crear("Cuadernos", 1);
        categoria.Id = IdCategoria;
        _categorias.GetByIdAsync(IdCategoria, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(categoria);
        var marca = Marca.Crear("Rivadavia");
        marca.Id = IdMarca;
        _marcas.GetByIdAsync(IdMarca, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(marca);

        _proveedor = Proveedor.Crear("Distribuidora Valida", "20111111112");
        _proveedor.Id = IdProveedor;
        _proveedores.GetByIdWithIncludesAsync(
                IdProveedor,
                Arg.Any<CancellationToken>(),
                Arg.Any<Expression<Func<Proveedor, object>>[]>())
            .Returns(_proveedor);

        _servicio = new EquivalenciaClasificacionService(
            _proveedores,
            _categorias,
            _marcas,
            _usuarios,
            _consultas,
            _unitOfWork,
            new AsignarEquivalenciaValidator(),
            new QuitarEquivalenciaValidator());
    }

    [Fact]
    public async Task Listar_DelegaEnElServicioDeConsultas()
    {
        IReadOnlyList<EquivalenciaDto> esperado =
            [new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "RIVA", IdMarca = IdMarca }];
        _consultas.ListarEquivalenciasAsync(IdProveedor, TipoEquivalenciaEnum.Marca, Arg.Any<CancellationToken>())
            .Returns(esperado);

        var resultado = await _servicio.ListarAsync(IdProveedor, TipoEquivalenciaEnum.Marca);

        resultado.Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task Asignar_ConActorSinAutoridad_LanzaOperacionNoAutorizadaSinCargarElProveedor()
    {
        var accion = () => _servicio.AsignarAsync(IdCajero, EquivalenciaDeCategoria(IdCategoria));

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _proveedores.DidNotReceive().GetByIdWithIncludesAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<Expression<Func<Proveedor, object>>[]>());
    }

    [Fact]
    public async Task Asignar_DeCategoria_LaCreaEnLaRaizConClaveNormalizadaYGuardaUnaVez()
    {
        await _servicio.AsignarAsync(IdGerente, EquivalenciaDeCategoria(IdCategoria));

        var equivalencia = _proveedor.Equivalencias.Should().ContainSingle().Subject;
        equivalencia.TextoRubro.Should().Be("ESCOLAR");
        equivalencia.Texto.Should().Be("CUADERNOS T/D");
        equivalencia.IdCategoria.Should().Be(IdCategoria);
        await _proveedores.Received(1).UpdateAsync(_proveedor, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Asignar_DeMarcaSinDestino_LaDejaIgnoradaSinBuscarDestino()
    {
        var dto = new AsignarEquivalenciaDto { IdProveedor = IdProveedor, Tipo = TipoEquivalenciaEnum.Marca, Texto = "Genérico" };

        await _servicio.AsignarAsync(IdGerente, dto);

        _proveedor.Equivalencias.Should().ContainSingle().Which.EsIgnorada.Should().BeTrue();
        await _marcas.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Asignar_ExistenteConOtroDestino_ReemplazaElDestino()
    {
        var existente = _proveedor.AsignarEquivalenciaMarca("RIVA", null);
        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = IdProveedor,
            Tipo = TipoEquivalenciaEnum.Marca,
            Texto = "riva",
            IdDestino = IdMarca
        };

        await _servicio.AsignarAsync(IdGerente, dto);

        _proveedor.Equivalencias.Should().ContainSingle();
        existente.IdMarca.Should().Be(IdMarca);
    }

    [Fact]
    public async Task Asignar_ConCategoriaInexistente_LanzaDomainExceptionYNoGuarda()
    {
        var accion = () => _servicio.AsignarAsync(IdGerente, EquivalenciaDeCategoria(55));

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la categoría con ID 55.");
        _proveedor.Equivalencias.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Asignar_ConMarcaInexistente_LanzaDomainException()
    {
        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = IdProveedor,
            Tipo = TipoEquivalenciaEnum.Marca,
            Texto = "RIVA",
            IdDestino = 55
        };

        var accion = () => _servicio.AsignarAsync(IdGerente, dto);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró la marca con ID 55.");
    }

    [Fact]
    public async Task Asignar_ConProveedorInexistente_LanzaDomainException()
    {
        var dto = EquivalenciaDeCategoria(IdCategoria) with { IdProveedor = 99 };

        var accion = () => _servicio.AsignarAsync(IdGerente, dto);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("No se encontró el proveedor con ID 99.");
    }

    [Fact]
    public async Task Asignar_DeMarcaConRubro_LanzaValidationException()
    {
        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = IdProveedor,
            Tipo = TipoEquivalenciaEnum.Marca,
            TextoRubro = "ESCOLAR",
            Texto = "RIVA"
        };

        var accion = () => _servicio.AsignarAsync(IdGerente, dto);

        await accion.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Asignar_ConCarreraEnElIndiceUnico_TraduceAUnMensajeDeNegocio()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new RestriccionDeUnicidadException());

        var accion = () => _servicio.AsignarAsync(IdGerente, EquivalenciaDeCategoria(IdCategoria));

        (await accion.Should().ThrowAsync<DomainException>().WithMessage("*'CUADERNOS T/D' al mismo tiempo*"))
            .WithInnerException<RestriccionDeUnicidadException>();
    }

    [Fact]
    public async Task Quitar_Existente_LaDaDeBajaPorLaRaizYGuarda()
    {
        var existente = _proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS T/D", IdCategoria);
        var dto = new QuitarEquivalenciaDto
        {
            IdProveedor = IdProveedor,
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = "escolar",
            Texto = "Cuadernos t/d"
        };

        await _servicio.QuitarAsync(IdGerente, dto);

        existente.IsDeleted.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Quitar_Inexistente_LanzaDomainExceptionYNoGuarda()
    {
        var dto = new QuitarEquivalenciaDto { IdProveedor = IdProveedor, Tipo = TipoEquivalenciaEnum.Marca, Texto = "RIVA" };

        var accion = () => _servicio.QuitarAsync(IdGerente, dto);

        await accion.Should().ThrowAsync<DomainException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static AsignarEquivalenciaDto EquivalenciaDeCategoria(int idDestino)
    {
        return new AsignarEquivalenciaDto
        {
            IdProveedor = IdProveedor,
            Tipo = TipoEquivalenciaEnum.Categoria,
            TextoRubro = "Escolar",
            Texto = "Cuadernos  t/d",
            IdDestino = idDestino
        };
    }

    private void RegistrarUsuario(int id, RolUsuarioEnum rol)
    {
        var email = rol == RolUsuarioEnum.Cajero ? null : $"usuario{id}@libreria.com";
        var usuario = Usuario.Crear($"usuario{id}", "Juan", "Pérez", "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }
}
