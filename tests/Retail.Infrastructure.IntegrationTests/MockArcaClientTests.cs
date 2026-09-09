using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Retail.Application.DTOs.Fiscal;
using Retail.Domain.Enums;
using Retail.Infrastructure.ExternalServices.ArcaSdk;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

public class MockArcaClientTests
{
    private readonly MockArcaClient _sut;

    public MockArcaClientTests()
    {
        var options = Options.Create(new ArcaOptions
        {
            BaseUrl = "http://localhost:8080",
            TimeoutSeconds = 10,
            UseMockArca = true,
            PuntoVentaDefecto = 1
        });

        var logger = Substitute.For<ILogger<MockArcaClient>>();
        _sut = new MockArcaClient(options, logger)
        {
            LatenciaSimuladaMs = 0 // Cero latencia para tests ultrarrápidos
        };
    }

    [Fact]
    public async Task SolicitarCaeAsync_SolicitudValida_RetornaCaeYResultadoAprobado()
    {
        // Arrange
        var solicitud = new SolicitudCaeDto
        {
            IdVenta = 42,
            TipoComprobante = TipoComprobanteFiscalEnum.FacturaB,
            PuntoVenta = 1,
            CuitCliente = null,
            SubtotalNeto = 1000.00m,
            IvaTotal = 210.00m,
            Total = 1210.00m,
            FechaComprobante = new DateTime(2026, 9, 8, 14, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var resultado = await _sut.SolicitarCaeAsync(solicitud);

        // Assert
        resultado.Should().NotBeNull();
        resultado.Exitoso.Should().BeTrue();
        resultado.ResultadoArca.Should().Be("A");
        resultado.Cae.Should().NotBeNull();
        resultado.Cae!.Length.Should().Be(14);
        resultado.Cae.Should().MatchRegex(@"^\d{14}$");
        resultado.FechaVtoCae.Should().Be(DateOnly.FromDateTime(solicitud.FechaComprobante.AddDays(10)));
        resultado.NumeroComprobante.Should().Be(42);
        resultado.MotivoError.Should().BeNull();
    }

    [Fact]
    public async Task SolicitarCaeAsync_CuitInvalidoOErrorConfigurado_RetornaRechazoConMotivo()
    {
        // Arrange
        var solicitud = new SolicitudCaeDto
        {
            IdVenta = 99,
            TipoComprobante = TipoComprobanteFiscalEnum.FacturaA,
            PuntoVenta = 1,
            CuitCliente = "99999999999", // CUIT reservado para emular rechazo fiscal
            SubtotalNeto = 5000.00m,
            IvaTotal = 1050.00m,
            Total = 6050.00m,
            FechaComprobante = DateTime.UtcNow
        };

        // Act
        var resultado = await _sut.SolicitarCaeAsync(solicitud);

        // Assert
        resultado.Should().NotBeNull();
        resultado.Exitoso.Should().BeFalse();
        resultado.ResultadoArca.Should().Be("R");
        resultado.Cae.Should().BeNull();
        resultado.MotivoError.Should().NotBeNullOrWhiteSpace().And.Contain("CUIT");
    }

    [Fact]
    public async Task SolicitarCaeAsync_SimularCaidaServicio_RetornaFalloContingencia()
    {
        // Arrange
        _sut.SimularCaidaServicio = true;

        var solicitud = new SolicitudCaeDto
        {
            IdVenta = 50,
            TipoComprobante = TipoComprobanteFiscalEnum.FacturaB,
            PuntoVenta = 1,
            CuitCliente = null,
            SubtotalNeto = 1000.00m,
            IvaTotal = 210.00m,
            Total = 1210.00m,
            FechaComprobante = DateTime.UtcNow
        };

        // Act
        var resultado = await _sut.SolicitarCaeAsync(solicitud);

        // Assert
        resultado.Should().NotBeNull();
        resultado.Exitoso.Should().BeFalse();
        resultado.ResultadoArca.Should().Be("R");
        resultado.MotivoError.Should().NotBeNullOrWhiteSpace().And.Contain("RF-17");
    }

    [Fact]
    public async Task SolicitarCaeAsync_SimularExcepcionRed_LanzaHttpRequestException()
    {
        // Arrange
        _sut.SimularExcepcionRed = true;

        var solicitud = new SolicitudCaeDto
        {
            IdVenta = 51,
            TipoComprobante = TipoComprobanteFiscalEnum.FacturaB,
            PuntoVenta = 1,
            CuitCliente = null,
            SubtotalNeto = 1000.00m,
            IvaTotal = 210.00m,
            Total = 1210.00m,
            FechaComprobante = DateTime.UtcNow
        };

        // Act
        var act = async () => await _sut.SolicitarCaeAsync(solicitud);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task VerificarSaludServicioAsync_EstadoNormal_RetornaTrue()
    {
        // Act
        var saludable = await _sut.VerificarSaludServicioAsync();

        // Assert
        saludable.Should().BeTrue();
    }

    [Fact]
    public async Task VerificarSaludServicioAsync_ServicioCaido_RetornaFalse()
    {
        // Arrange
        _sut.SimularCaidaServicio = true;

        // Act
        var saludable = await _sut.VerificarSaludServicioAsync();

        // Assert
        saludable.Should().BeFalse();
    }
}
