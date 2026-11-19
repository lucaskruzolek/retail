using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Services;
using Retail.Application.Validators.Usuarios;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Concurrencia;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Write skew de "el último Gerente" (Track 6.7, Paquete 2): el bloqueo de aplicación <see cref="SqlServerBloqueoGerentes"/>
/// serializa entre terminales las bajas y degradaciones de Gerentes. Son deterministas: sin hilos; la "otra terminal"
/// es un segundo objeto de bloqueo que retiene el recurso mientras el servicio intenta tomarlo.
/// </summary>
public class ConcurrenciaGerentesTests : PedidosPruebaBase
{
    private static readonly TimeSpan EsperaCorta = TimeSpan.FromMilliseconds(200);

    public ConcurrenciaGerentesTests()
        : base("ConcGerentes")
    {
    }

    [Fact]
    public async Task AdquirirAsync_BloqueoTomadoPorOtraTerminal_FallaConConflictoTrasLaEspera()
    {
        // Arrange
        await using var terminalA = await new SqlServerBloqueoGerentes(Contexto, EsperaCorta).AdquirirAsync();
        await using var contextoB = NuevoContexto();

        // Act
        var act = () => new SqlServerBloqueoGerentes(contextoB, EsperaCorta).AdquirirAsync();

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>()
            .WithMessage("*Otra terminal*Gerente*");
    }

    [Fact]
    public async Task AdquirirAsync_TrasLiberar_OtraTerminalLoObtiene()
    {
        // Arrange
        var terminalA = await new SqlServerBloqueoGerentes(Contexto, EsperaCorta).AdquirirAsync();
        await using var contextoB = NuevoContexto();
        await terminalA.DisposeAsync();

        // Act
        await using var terminalB = await new SqlServerBloqueoGerentes(contextoB, EsperaCorta).AdquirirAsync();

        // Assert
        terminalB.Should().NotBeNull();
    }

    [Fact]
    public async Task BajaUsuarioAsync_ConBloqueoDeGerentesTomadoPorOtraTerminal_FallaYNoDaDeBaja()
    {
        // Arrange: hay dos Gerentes, así que sin el bloqueo la baja procedería.
        var otroGerente = await SembrarGerenteAsync("segundo.gerente");
        await using var terminalA = await new SqlServerBloqueoGerentes(Contexto, EsperaCorta).AdquirirAsync();
        await using var contextoB = NuevoContexto();
        var servicio = CrearServicio(contextoB);

        // Act
        var act = () => servicio.BajaUsuarioAsync(IdUsuario, otroGerente.Id);

        // Assert
        await act.Should().ThrowAsync<ConflictoDeConcurrenciaException>();
        await using var verificacion = NuevoContexto();
        (await verificacion.Usuarios.AnyAsync(u => u.Id == otroGerente.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task BajaUsuarioAsync_DosGerentesDadosDeBajaEnSecuencia_ElSegundoLanzaUltimoGerente()
    {
        // Arrange
        var otroGerente = await SembrarGerenteAsync("segundo.gerente");
        await using var contextoA = NuevoContexto();
        await using var contextoB = NuevoContexto();

        // Act
        await CrearServicio(contextoA).BajaUsuarioAsync(IdUsuario, otroGerente.Id);
        var act = () => CrearServicio(contextoB).BajaUsuarioAsync(IdUsuario, IdUsuario);

        // Assert
        await act.Should().ThrowAsync<UltimoGerenteException>();
        await using var verificacion = NuevoContexto();
        (await verificacion.Usuarios.CountAsync(u => u.IdRol == (int)RolUsuarioEnum.Gerente)).Should().Be(1);
    }

    private async Task<Usuario> SembrarGerenteAsync(string nombreUsuario)
    {
        var gerente = Usuario.Crear(
            nombreUsuario, "Beatriz", "Lopez", "hash-de-prueba", RolUsuarioEnum.Gerente, $"{nombreUsuario}@example.com");
        Contexto.Usuarios.Add(gerente);
        await Contexto.SaveChangesAsync();
        return gerente;
    }

    private static UsuarioService CrearServicio(RetailDbContext contexto)
    {
        return new UsuarioService(
            new Repository<Usuario>(contexto),
            new UnitOfWork(contexto),
            Substitute.For<IPasswordHasher>(),
            new SqlServerBloqueoGerentes(contexto, EsperaCorta),
            new CrearUsuarioValidator(),
            new ModificarUsuarioValidator(),
            new CambiarPasswordValidator());
    }
}
