using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.Application.DTOs.Terminales;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Terminales;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class TerminalServiceTests
{
    private const int IdGerente = 1;
    private const int IdCajero = 2;
    private const string EquipoLocal = "CAJA-01";

    private readonly IRepository<Terminal> _terminales = Substitute.For<IRepository<Terminal>>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly ITerminalLocal _terminalLocal = Substitute.For<ITerminalLocal>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TerminalService _sut;

    public TerminalServiceTests()
    {
        _terminalLocal.NombreEquipo.Returns(EquipoLocal);
        RegistrarUsuario(IdGerente, RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdCajero, RolUsuarioEnum.Cajero);

        _sut = new TerminalService(_terminales, _usuarios, _terminalLocal, _unitOfWork, new NuevaTerminalValidator());
    }

    // --- VerificarAsync -------------------------------------------------------------------------------------

    [Fact]
    public async Task VerificarAsync_SinIdGuardado_EsNoActivadaSinConsultarLaBase()
    {
        // Arrange
        _terminalLocal.IdTerminal.Returns((int?)null);

        // Act
        var estado = await _sut.VerificarAsync();

        // Assert
        estado.Estado.Should().Be(EstadoTerminalEnum.NoActivada);
        estado.Terminal.Should().BeNull();
        await _terminales.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default(CancellationToken));
    }

    [Fact]
    public async Task VerificarAsync_IdQueNoExisteEnLaBase_EsInexistente()
    {
        // Arrange
        _terminalLocal.IdTerminal.Returns(4);
        _terminales.GetByIdAsync(4, Arg.Any<CancellationToken>()).Returns((Terminal?)null);

        // Act
        var estado = await _sut.VerificarAsync();

        // Assert
        estado.Estado.Should().Be(EstadoTerminalEnum.Inexistente);
        estado.Terminal.Should().BeNull();
    }

    [Fact]
    public async Task VerificarAsync_TerminalConElMismoEquipo_EsActiva()
    {
        // Arrange: la comparación no distingue mayúsculas (Windows tampoco).
        _terminalLocal.IdTerminal.Returns(4);
        _terminales.GetByIdAsync(4, Arg.Any<CancellationToken>()).Returns(NuevaTerminal(4, "Mostrador 1", "caja-01"));

        // Act
        var estado = await _sut.VerificarAsync();

        // Assert
        estado.Estado.Should().Be(EstadoTerminalEnum.Activa);
        estado.Terminal!.Nombre.Should().Be("Mostrador 1");
    }

    [Fact]
    public async Task VerificarAsync_TerminalRegistradaConOtroEquipo_EsEquipoDistinto()
    {
        // Arrange
        _terminalLocal.IdTerminal.Returns(4);
        _terminales.GetByIdAsync(4, Arg.Any<CancellationToken>()).Returns(NuevaTerminal(4, "Mostrador 1", "OTRA-PC"));

        // Act
        var estado = await _sut.VerificarAsync();

        // Assert
        estado.Estado.Should().Be(EstadoTerminalEnum.EquipoDistinto);
        estado.Terminal!.NombreEquipo.Should().Be("OTRA-PC");
    }

    // --- ListarAsync ----------------------------------------------------------------------------------------

    [Fact]
    public async Task ListarAsync_VariasTerminales_LasDevuelveOrdenadasPorNombre()
    {
        // Arrange
        _terminales.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns([NuevaTerminal(2, "Mostrador 2", "B"), NuevaTerminal(1, "Mostrador 1", "A")]);

        // Act
        var lista = await _sut.ListarAsync();

        // Assert
        lista.Select(t => t.Nombre).Should().Equal("Mostrador 1", "Mostrador 2");
    }

    // --- ActivarNuevaAsync ----------------------------------------------------------------------------------

    [Fact]
    public async Task ActivarNuevaAsync_Gerente_CreaLaTerminalConElEquipoLocalYGuardaLaIdentidad()
    {
        // Arrange
        _terminales.AddAsync(Arg.Do<Terminal>(t => t.Id = 7), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<Terminal>());

        // Act
        var dto = await _sut.ActivarNuevaAsync(IdGerente, "  Mostrador   3 ");

        // Assert
        dto.Id.Should().Be(7);
        dto.Nombre.Should().Be("Mostrador 3");
        dto.NombreEquipo.Should().Be(EquipoLocal);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _terminalLocal.Received(1).GuardarAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivarNuevaAsync_Cajero_LanzaOperacionNoAutorizadaSinCrearNada()
    {
        // Act
        var act = async () => await _sut.ActivarNuevaAsync(IdCajero, "Mostrador 3");

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _terminales.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _terminalLocal.DidNotReceiveWithAnyArgs().GuardarAsync(default, default);
    }

    [Fact]
    public async Task ActivarNuevaAsync_ActorInexistente_LanzaOperacionNoAutorizada()
    {
        // Act
        var act = async () => await _sut.ActivarNuevaAsync(404, "Mostrador 3");

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>().WithMessage("*inexistente o dado de baja*");
    }

    [Fact]
    public async Task ActivarNuevaAsync_NombreVacio_LanzaValidacionSinGuardar()
    {
        // Act
        var act = async () => await _sut.ActivarNuevaAsync(IdGerente, "   ");

        // Assert
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ActivarNuevaAsync_NombreRepetido_TraduceLaUnicidadAUnMensajeDeDominioYNoGuardaLaIdentidad()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new RestriccionDeUnicidadException());

        // Act
        var act = async () => await _sut.ActivarNuevaAsync(IdGerente, "Mostrador  1");

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("Ya existe una terminal llamada «Mostrador 1».");
        _unitOfWork.Received(1).DescartarCambios();
        await _terminalLocal.DidNotReceiveWithAnyArgs().GuardarAsync(default, default);
    }

    // --- ActivarReemplazoAsync ------------------------------------------------------------------------------

    [Fact]
    public async Task ActivarReemplazoAsync_Gerente_AsignaElEquipoLocalConservaElNombreYGuardaLaIdentidad()
    {
        // Arrange
        var terminal = NuevaTerminal(3, "Mostrador 1", "PC-ROTA");
        _terminales.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(terminal);

        // Act
        var dto = await _sut.ActivarReemplazoAsync(IdGerente, 3);

        // Assert
        dto.Nombre.Should().Be("Mostrador 1");
        dto.NombreEquipo.Should().Be(EquipoLocal);
        terminal.NombreEquipo.Should().Be(EquipoLocal);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _terminalLocal.Received(1).GuardarAsync(3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivarReemplazoAsync_Cajero_LanzaOperacionNoAutorizada()
    {
        // Act
        var act = async () => await _sut.ActivarReemplazoAsync(IdCajero, 3);

        // Assert
        await act.Should().ThrowAsync<OperacionNoAutorizadaException>();
        await _terminalLocal.DidNotReceiveWithAnyArgs().GuardarAsync(default, default);
    }

    [Fact]
    public async Task ActivarReemplazoAsync_TerminalInexistente_LanzaDomainException()
    {
        // Arrange
        _terminales.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns((Terminal?)null);

        // Act
        var act = async () => await _sut.ActivarReemplazoAsync(IdGerente, 3);

        // Assert
        await act.Should().ThrowAsync<DomainException>().WithMessage("*No se encontró la terminal 3*");
    }

    private void RegistrarUsuario(int id, RolUsuarioEnum rol)
    {
        var usuario = Usuario.Crear($"usuario.{id}", "Nombre", "Apellido", "hash", rol, rol == RolUsuarioEnum.Gerente ? "gerente@example.com" : null);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted: false, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    private static Terminal NuevaTerminal(int id, string nombre, string equipo)
    {
        var terminal = Terminal.Crear(nombre, equipo);
        terminal.Id = id;
        return terminal;
    }
}
