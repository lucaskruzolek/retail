using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Regla "esta PC es el servidor" (MT-12): la comparación en sí y la consulta real al motor de pruebas.
/// </summary>
public class EquipoServidorSqlServerTests
{
    [Theory]
    [InlineData(".", null, "CAJA1")]
    [InlineData(@".\SQLEXPRESS", null, "CAJA1")]
    [InlineData("(local)", "OTRO-NOMBRE", "CAJA1")]
    [InlineData("localhost", "OTRO-NOMBRE", "CAJA1")]
    [InlineData("tcp:127.0.0.1,1433", null, "CAJA1")]
    [InlineData(@"caja1\SQLEXPRESS", "CAJA1", "CAJA1")]
    [InlineData("10.0.0.5", "caja1", "CAJA1")]
    public void Evaluar_MotorEnEstaPc_EsEstaPcVerdadero(string origen, string? nombreFisico, string estaPc)
    {
        var info = EquipoServidorSqlServer.Evaluar(origen, nombreFisico, estaPc);

        info.EsEstaPc.Should().BeTrue();
    }

    [Theory]
    [InlineData(@"SERVIDOR\SQLEXPRESS", "SERVIDOR", "CAJA1")]
    [InlineData("tcp:servidor,1433", null, "CAJA1")]
    [InlineData("192.168.1.10", "SERVIDOR", "CAJA1")]
    public void Evaluar_MotorEnOtraPc_EsEstaPcFalsoYNombraAlServidor(string origen, string? nombreFisico, string estaPc)
    {
        var info = EquipoServidorSqlServer.Evaluar(origen, nombreFisico, estaPc);

        info.EsEstaPc.Should().BeFalse();
        info.NombreEquipoServidor.Should().Be(nombreFisico ?? "servidor");
    }

    [Fact]
    public void Evaluar_OrigenLocalSinNombreFisico_InformaElNombreRealDeEstaPc()
    {
        var info = EquipoServidorSqlServer.Evaluar(".", null, "CAJA1");

        info.NombreEquipoServidor.Should().Be("CAJA1");
    }

    [Fact]
    public async Task ObtenerAsync_ServidorDePruebas_EsEstaPcPorqueElMotorCorreEnLaMismaMaquina()
    {
        // La base de pruebas no existe: la consulta va contra master, como en una PC nueva.
        var contexto = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion("RetailDb_Test_Inexistente"))
            .Options);
        await using var _ = contexto;

        var info = await new EquipoServidorSqlServer(contexto).ObtenerAsync();

        info.EsEstaPc.Should().BeTrue();
        info.NombreEquipoServidor.Should().NotBeNullOrWhiteSpace();
    }
}
