using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Clientes;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

public class ClienteQueryServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_CliQ_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private ClienteQueryService _sut = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        _sut = new ClienteQueryService(_context);
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
    public async Task ObtenerClientesPaginadosAsync_ConPaginacionYMetricasDeuda_CalculaEnServidor()
    {
        // Arrange
        var clientes = new List<Cliente>
        {
            new()
            {
                RazonSocialONombre = "Librería Central",
                TipoDocumento = TipoDocumentoEnum.Cuit,
                NumeroDocumento = "30-10101010-1",
                CondicionIva = CondicionIvaEnum.ResponsableInscripto,
                TieneCuentaCorriente = true,
                LimiteCredito = 50000m,
                SaldoCuentaCorriente = 15000m
            },
            new()
            {
                RazonSocialONombre = "Carlos Gomez",
                TipoDocumento = TipoDocumentoEnum.Dni,
                NumeroDocumento = "20-20202020-2",
                CondicionIva = CondicionIvaEnum.ConsumidorFinal,
                TieneCuentaCorriente = true,
                LimiteCredito = 20000m,
                SaldoCuentaCorriente = 5000m
            },
            new()
            {
                RazonSocialONombre = "Escuela Normal",
                TipoDocumento = TipoDocumentoEnum.Cuit,
                NumeroDocumento = "30-30303030-3",
                CondicionIva = CondicionIvaEnum.Exento,
                TieneCuentaCorriente = true,
                LimiteCredito = 80000m,
                SaldoCuentaCorriente = 0m
            }
        };
        _context.Clientes.AddRange(clientes);
        await _context.SaveChangesAsync();

        var consulta = new ConsultaClientesDto
        {
            Pagina = 1,
            TamanoPagina = 2
        };

        // Act
        var resultado = await _sut.ObtenerClientesPaginadosAsync(consulta);

        // Assert
        resultado.Items.Should().HaveCount(2);
        resultado.TotalRegistros.Should().Be(3);
        resultado.TotalClientes.Should().Be(3);
        resultado.TotalClientesConDeuda.Should().Be(2);
        resultado.TotalDeudaCartera.Should().Be(20000m);
        resultado.TotalPaginas.Should().Be(2);
    }

    [Fact]
    public async Task BuscarClientesRapidoAsync_FiltraPorDocumentoONombreConPushDown()
    {
        // Arrange
        _context.Clientes.AddRange(
            new Cliente { RazonSocialONombre = "Ana Martinez", NumeroDocumento = "27-40404040-7", CondicionIva = CondicionIvaEnum.ConsumidorFinal },
            new Cliente { RazonSocialONombre = "Supermercado Norte", NumeroDocumento = "30-50505050-5", CondicionIva = CondicionIvaEnum.ResponsableInscripto }
        );
        await _context.SaveChangesAsync();

        // Act - Buscar por documento
        var resDoc = await _sut.BuscarClientesRapidoAsync("40404040");
        // Act - Buscar por nombre
        var resNom = await _sut.BuscarClientesRapidoAsync("Supermercado");

        // Assert
        resDoc.Should().ContainSingle(c => c.RazonSocialONombre == "Ana Martinez");
        resNom.Should().ContainSingle(c => c.NumeroDocumento == "30-50505050-5");
    }
}
