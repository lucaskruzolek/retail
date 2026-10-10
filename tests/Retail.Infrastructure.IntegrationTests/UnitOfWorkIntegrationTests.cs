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
/// Verifica contra LocalDB que un guardado fallido deja el contexto limpio: como el contexto vive mientras dura la
/// pantalla (H-19), los cambios rechazados no deben guardarse con la próxima operación exitosa.
/// </summary>
public class UnitOfWorkIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Uow_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();
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

    private static Cliente CrearCliente(string nombre, string documento, decimal limiteCredito)
    {
        return ClientesDePrueba.Crear(razonSocialONombre: nombre, tipoDocumento: TipoDocumentoEnum.Dni, numeroDocumento: documento, condicionIva: CondicionIvaEnum.ConsumidorFinal, tieneCuentaCorriente: limiteCredito != 0m, limiteCredito: limiteCredito);
    }
}
