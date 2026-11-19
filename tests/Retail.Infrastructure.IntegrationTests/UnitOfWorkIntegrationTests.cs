using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express que un guardado fallido deja el contexto limpio: como el contexto vive mientras dura la
/// pantalla (H-19), los cambios rechazados no deben guardarse con la próxima operación exitosa.
/// </summary>
public class UnitOfWorkIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Uow_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.CrearDesdePlantillaAsync();
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
    public async Task SaveChangesAsync_GuardadoRechazadoPorLaBase_DescartaLosCambiosPendientes()
    {
        // Arrange: un límite negativo viola CK_CLIENTES_LimiteCredito
        var uow = new UnitOfWork(_context);
        await _context.Clientes.AddAsync(CrearCliente("Cliente Rechazado", "20111111", limiteCredito: -1m));

        // Act
        var guardar = () => uow.SaveChangesAsync();

        // Assert: el error llega al llamador y el contexto ya no sigue al cliente rechazado
        await guardar.Should().ThrowAsync<DbUpdateException>();
        _context.ChangeTracker.Entries().Should().BeEmpty();

        // La siguiente operación exitosa guarda solo lo suyo
        await _context.Clientes.AddAsync(CrearCliente("Cliente Válido", "20222222", limiteCredito: 0m));
        await uow.SaveChangesAsync();

        var nombres = await _context.Clientes.AsNoTracking().Select(c => c.RazonSocialONombre).ToListAsync();
        nombres.Should().ContainSingle().Which.Should().Be("Cliente Válido");
    }

    [Fact]
    public async Task CommitTransactionAsync_GuardadoRechazadoPorLaBase_DescartaLosCambiosYCierraLaTransaccion()
    {
        // Arrange
        var uow = new UnitOfWork(_context);
        await uow.BeginTransactionAsync();
        await _context.Clientes.AddAsync(CrearCliente("Cliente Rechazado", "20111111", limiteCredito: -1m));

        // Act
        var confirmar = () => uow.CommitTransactionAsync();

        // Assert
        await confirmar.Should().ThrowAsync<DbUpdateException>();
        _context.ChangeTracker.Entries().Should().BeEmpty();

        // La transacción fallida se cerró: se puede abrir y confirmar otra
        await uow.BeginTransactionAsync();
        await _context.Clientes.AddAsync(CrearCliente("Cliente Válido", "20222222", limiteCredito: 0m));
        await uow.CommitTransactionAsync();

        var nombres = await _context.Clientes.AsNoTracking().Select(c => c.RazonSocialONombre).ToListAsync();
        nombres.Should().ContainSingle().Which.Should().Be("Cliente Válido");
    }

    [Fact]
    public async Task SaveChangesAsync_ViolacionDeIndiceUnico_LaExcepcionNombraElIndiceReal()
    {
        // Arrange: los servicios que protegen más de un índice en un mismo guardado (CompraService, InventarioService)
        // eligen el mensaje con CorrespondeAIndice, que depende de que el mensaje real de SQL Server nombre el índice.
        // Los tests unitarios simulan ese texto; este lo verifica contra el motor (Track 6.7, P2).
        var uow = new UnitOfWork(_context);
        await _context.Clientes.AddAsync(CrearCliente("Primer Cliente", "20333333", limiteCredito: 0m));
        await uow.SaveChangesAsync();
        await _context.Clientes.AddAsync(CrearCliente("Cliente Duplicado", "20333333", limiteCredito: 0m));

        // Act
        var guardar = () => uow.SaveChangesAsync();

        // Assert
        var excepcion = (await guardar.Should().ThrowAsync<Retail.Application.Exceptions.RestriccionDeUnicidadException>()).Which;
        excepcion.CorrespondeAIndice("IX_CLIENTES_numero_documento").Should().BeTrue();
        excepcion.CorrespondeAIndice("UX_COMPRAS_PedidoOrigen").Should().BeFalse();
    }

    private static Cliente CrearCliente(string nombre, string documento, decimal limiteCredito)
    {
        return ClientesDePrueba.Crear(razonSocialONombre: nombre, tipoDocumento: TipoDocumentoEnum.Dni, numeroDocumento: documento, condicionIva: CondicionIvaEnum.ConsumidorFinal, tieneCuentaCorriente: limiteCredito != 0m, limiteCredito: limiteCredito);
    }
}
