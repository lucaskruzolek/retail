using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using Retail.Application.DTOs.Sistema;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class EstadoSistemaServiceTests
{
    private readonly IEsquemaBaseDatos _esquema = Substitute.For<IEsquemaBaseDatos>();
    private readonly IRepository<Usuario> _usuarioRepository = Substitute.For<IRepository<Usuario>>();
    private readonly EstadoSistemaService _sut;

    public EstadoSistemaServiceTests()
    {
        _sut = new EstadoSistemaService(_esquema, _usuarioRepository);
    }

    private static EstadoEsquemaDto Esquema(bool conecta = true, int pendientes = 0, int desconocidas = 0) => new()
    {
        PuedeConectar = conecta,
        MigracionesPendientes = Enumerable.Range(1, pendientes).Select(i => $"Pendiente{i}").ToList(),
        MigracionesDesconocidas = Enumerable.Range(1, desconocidas).Select(i => $"Desconocida{i}").ToList()
    };

    private void GerentesActivos(params Usuario[] gerentes)
    {
        _usuarioRepository.FindAsync(Arg.Any<Expression<Func<Usuario, bool>>>(), includeDeleted: false, Arg.Any<CancellationToken>())
            .Returns(gerentes.ToList());
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 2, 0)]
    [InlineData(true, 0, 1)]
    public async Task ObtenerEstadoAsync_EsquemaNoAlDia_NoPuedeOperarYNoConsultaUsuarios(bool conecta, int pendientes, int desconocidas)
    {
        // Arrange
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema(conecta, pendientes, desconocidas));

        // Act
        var estado = await _sut.ObtenerEstadoAsync();

        // Assert: con el esquema desactualizado, la tabla de usuarios podría no existir.
        estado.PuedeOperar.Should().BeFalse();
        await _usuarioRepository.DidNotReceiveWithAnyArgs().FindAsync(default!, default, default);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_EsquemaAlDiaSinGerentes_NoPuedeOperar()
    {
        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema());
        GerentesActivos();

        var estado = await _sut.ObtenerEstadoAsync();

        estado.ExisteGerenteActivo.Should().BeFalse();
        estado.PuedeOperar.Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerEstadoAsync_GerentesConYSinEmail_PuedeOperarEInformaLosQueNoTienen()
    {
        // Arrange: el filtro por rol lo aplica el repositorio. "admin" representa una fila sembrada antes de que el
        // email fuera obligatorio, un estado que el agregado ya no permite crear pero que existe en bases viejas.
        var conEmail = Usuario.Crear("ana", "Ana", "Gómez", "hash", RolUsuarioEnum.Gerente, "ana@libreria.com");
        var sinEmail = Usuario.Crear("admin", "Admin", "General", "hash", RolUsuarioEnum.Cajero, null);

        _esquema.ObtenerEstadoAsync(Arg.Any<CancellationToken>()).Returns(Esquema());
        GerentesActivos(conEmail, sinEmail);

        // Act
        var estado = await _sut.ObtenerEstadoAsync();

        // Assert
        estado.PuedeOperar.Should().BeTrue();
        estado.GerentesSinEmail.Should().Equal("admin");
    }
}
