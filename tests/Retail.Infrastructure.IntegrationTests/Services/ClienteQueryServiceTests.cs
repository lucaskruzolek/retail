using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Clientes;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
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
            ClientesDePrueba.Crear(razonSocialONombre: "Librería Central", tipoDocumento: TipoDocumentoEnum.Cuit, numeroDocumento: "30101010100", condicionIva: CondicionIvaEnum.ResponsableInscripto, tieneCuentaCorriente: true, limiteCredito: 50000m, saldoCuentaCorriente: 15000m),
            ClientesDePrueba.Crear(razonSocialONombre: "Carlos Gomez", tipoDocumento: TipoDocumentoEnum.Dni, numeroDocumento: "20202020", condicionIva: CondicionIvaEnum.ConsumidorFinal, tieneCuentaCorriente: true, limiteCredito: 20000m, saldoCuentaCorriente: 5000m),
            ClientesDePrueba.Crear(razonSocialONombre: "Escuela Normal", tipoDocumento: TipoDocumentoEnum.Cuit, numeroDocumento: "30303030308", condicionIva: CondicionIvaEnum.Exento, tieneCuentaCorriente: true, limiteCredito: 80000m, saldoCuentaCorriente: 0m)
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
            ClientesDePrueba.Crear(razonSocialONombre: "Ana Martinez", numeroDocumento: "27404040400", condicionIva: CondicionIvaEnum.ConsumidorFinal, tipoDocumento: TipoDocumentoEnum.Cuit),
            ClientesDePrueba.Crear(razonSocialONombre: "Supermercado Norte", numeroDocumento: "30505050505", condicionIva: CondicionIvaEnum.ResponsableInscripto, tipoDocumento: TipoDocumentoEnum.Cuit)
        );
        await _context.SaveChangesAsync();

        // Act - Buscar por documento
        var resDoc = await _sut.BuscarClientesRapidoAsync("40404040");
        // Act - Buscar por nombre
        var resNom = await _sut.BuscarClientesRapidoAsync("Supermercado");

        // Assert
        resDoc.Should().ContainSingle(c => c.RazonSocialONombre == "Ana Martinez");
        resNom.Should().ContainSingle(c => c.NumeroDocumento == "30505050505"); // se guarda sin guiones
    }

    [Fact]
    public async Task BuscarClientesRapidoAsync_SinAcentosNiMayusculas_EncuentraElNombreAcentuadoYRespetaLaEnie()
    {
        // Arrange: razon_social_o_nombre usa Modern_Spanish_CI_AI
        _context.Clientes.AddRange(
            ClientesDePrueba.Crear(razonSocialONombre: "Librería Martínez", numeroDocumento: "27111111117", condicionIva: CondicionIvaEnum.ConsumidorFinal, tipoDocumento: TipoDocumentoEnum.Cuit),
            ClientesDePrueba.Crear(razonSocialONombre: "Papelera Peña", numeroDocumento: "27222222228", condicionIva: CondicionIvaEnum.ConsumidorFinal, tipoDocumento: TipoDocumentoEnum.Cuit));
        await _context.SaveChangesAsync();

        // Act
        var sinAcentos = await _sut.BuscarClientesRapidoAsync("libreria martinez");
        var conEnie = await _sut.BuscarClientesRapidoAsync("peña");
        var sinEnie = await _sut.BuscarClientesRapidoAsync("pena");

        // Assert
        sinAcentos.Should().ContainSingle(c => c.RazonSocialONombre == "Librería Martínez");
        conEnie.Should().ContainSingle(c => c.RazonSocialONombre == "Papelera Peña");
        sinEnie.Should().BeEmpty("en castellano la ñ es una letra distinta de la n");
    }
}
