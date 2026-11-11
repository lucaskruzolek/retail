using FluentAssertions;
using NSubstitute;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

/// <summary>Política global de redondeo del precio de venta (D-38): lectura para todos, cambio solo del Gerente.</summary>
public class ConfiguracionComercialServiceTests
{
    private const int IdGerente = 1;
    private const int IdEncargado = 2;

    private readonly IRepository<ConfiguracionComercial> _configuraciones = Substitute.For<IRepository<ConfiguracionComercial>>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ConfiguracionComercialService _sut;

    public ConfiguracionComercialServiceTests()
    {
        RegistrarUsuario(IdGerente, "gerente", RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdEncargado, "encargado", RolUsuarioEnum.Encargado);
        _sut = new ConfiguracionComercialService(_configuraciones, _usuarios, _unitOfWork);
    }

    private void RegistrarUsuario(int id, string nombreUsuario, RolUsuarioEnum rol)
    {
        var email = rol == RolUsuarioEnum.Gerente ? $"{nombreUsuario}@libreria.com" : null;
        var usuario = Usuario.Crear(nombreUsuario, "Juan", "Pérez", "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, false, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    private ConfiguracionComercial RegistrarConfiguracion(RedondeoPrecioEnum redondeo)
    {
        var configuracion = new ConfiguracionComercial { Id = ConfiguracionComercial.IdUnico };
        configuracion.CambiarRedondeoPrecio(redondeo);
        _configuraciones.GetByIdAsync(ConfiguracionComercial.IdUnico, Arg.Any<CancellationToken>()).Returns(configuracion);
        return configuracion;
    }

    [Fact]
    public async Task ObtenerRedondeoPrecioAsync_ConConfiguracion_DevuelveLaPoliticaGuardada()
    {
        RegistrarConfiguracion(RedondeoPrecioEnum.MultiploDe10Superior);

        var redondeo = await _sut.ObtenerRedondeoPrecioAsync();

        redondeo.Should().Be(RedondeoPrecioEnum.MultiploDe10Superior);
    }

    [Fact]
    public async Task ObtenerRedondeoPrecioAsync_SinFilaDeConfiguracion_DevuelveEnteroMasCercano()
    {
        var redondeo = await _sut.ObtenerRedondeoPrecioAsync();

        redondeo.Should().Be(RedondeoPrecioEnum.EnteroMasCercano);
    }

    [Fact]
    public async Task CambiarRedondeoPrecioAsync_PorUnGerente_CambiaLaPoliticaYGuarda()
    {
        var configuracion = RegistrarConfiguracion(RedondeoPrecioEnum.EnteroMasCercano);

        await _sut.CambiarRedondeoPrecioAsync(IdGerente, RedondeoPrecioEnum.MultiploDe100Inferior);

        configuracion.RedondeoPrecio.Should().Be(RedondeoPrecioEnum.MultiploDe100Inferior);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarRedondeoPrecioAsync_PorUnEncargado_LanzaOperacionNoAutorizadaSinGuardar()
    {
        var configuracion = RegistrarConfiguracion(RedondeoPrecioEnum.EnteroMasCercano);

        var accion = () => _sut.CambiarRedondeoPrecioAsync(IdEncargado, RedondeoPrecioEnum.MultiploDe5Superior);

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        configuracion.RedondeoPrecio.Should().Be(RedondeoPrecioEnum.EnteroMasCercano);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarRedondeoPrecioAsync_ActorInexistente_LanzaOperacionNoAutorizada()
    {
        var accion = () => _sut.CambiarRedondeoPrecioAsync(99, RedondeoPrecioEnum.MultiploDe5Superior);

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
    }

    [Fact]
    public async Task CambiarRedondeoPrecioAsync_SinFilaDeConfiguracion_LaCreaConElIdUnico()
    {
        await _sut.CambiarRedondeoPrecioAsync(IdGerente, RedondeoPrecioEnum.MultiploDe5Inferior);

        await _configuraciones.Received(1).AddAsync(
            Arg.Is<ConfiguracionComercial>(c => c.Id == ConfiguracionComercial.IdUnico
                && c.RedondeoPrecio == RedondeoPrecioEnum.MultiploDe5Inferior),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
