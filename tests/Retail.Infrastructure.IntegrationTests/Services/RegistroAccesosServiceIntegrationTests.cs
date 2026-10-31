using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Auth;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Verifica contra SQL Server Express la auditoría de accesos: el INSERT de eventos y que el DEFAULT de
/// <c>tipo_evento</c> deje funcionando a sp_RegistrarIntentoLogin sin cambios.
/// </summary>
public class RegistroAccesosServiceIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Accesos_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;
    private RetailDbContext _context = null!;
    private RegistroAccesosService _sut = null!;

    public async Task InitializeAsync()
    {
        _connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        _context = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(_connectionString).Options);
        await _context.Database.CrearDesdePlantillaAsync();
        _sut = new RegistroAccesosService(_context);
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

    private async Task<List<(string Nombre, int? IdUsuario, byte Resultado, string? Equipo, byte Tipo, string? Detalle)>> LeerFilasAsync()
    {
        var filas = new List<(string, int?, byte, string?, byte, string?)>();
        await using var conexion = new SqlConnection(_connectionString);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT nombre_usuario, id_usuario, resultado, nombre_equipo, tipo_evento, detalle FROM dbo.AUDITORIA_ACCESOS ORDER BY id_auditoria";
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            filas.Add((
                lector.GetString(0),
                lector.IsDBNull(1) ? null : lector.GetInt32(1),
                lector.GetByte(2),
                lector.IsDBNull(3) ? null : lector.GetString(3),
                lector.GetByte(4),
                lector.IsDBNull(5) ? null : lector.GetString(5)));
        }

        return filas;
    }

    [Fact]
    public async Task RegistrarEventoAsync_InsertaFilaConTipoYDetalle()
    {
        // Act
        await _sut.RegistrarEventoAsync(
            TipoEventoAccesoEnum.AutorizacionRestauracion,
            ResultadoEventoAccesoEnum.Rechazado,
            "soporte",
            null,
            "Gerente intentado: jefe");

        // Assert
        var fila = (await LeerFilasAsync()).Should().ContainSingle().Subject;
        fila.Nombre.Should().Be("soporte");
        fila.IdUsuario.Should().BeNull();
        fila.Resultado.Should().Be((byte)ResultadoEventoAccesoEnum.Rechazado);
        fila.Equipo.Should().Be(Environment.MachineName);
        fila.Tipo.Should().Be((byte)TipoEventoAccesoEnum.AutorizacionRestauracion);
        fila.Detalle.Should().Be("Gerente intentado: jefe");
    }

    [Fact]
    public async Task RegistrarEventoAsync_DetalleLargo_LoRecortaA100Caracteres()
    {
        // Act
        await _sut.RegistrarEventoAsync(
            TipoEventoAccesoEnum.AutorizacionRestauracion, ResultadoEventoAccesoEnum.Rechazado, "soporte", null, new string('x', 150));

        // Assert
        (await LeerFilasAsync()).Single().Detalle.Should().HaveLength(100);
    }

    [Fact]
    public async Task RegistrarIntentoAsync_DelSp_SigueRegistrandoTipoInicioSesion()
    {
        // Act: el SP no conoce las columnas nuevas; el DEFAULT 0 las completa.
        var resultado = await _sut.RegistrarIntentoAsync("cajero", null, credencialesValidas: false);

        // Assert
        resultado.Resultado.Should().Be(1);
        var fila = (await LeerFilasAsync()).Should().ContainSingle().Subject;
        fila.Tipo.Should().Be((byte)TipoEventoAccesoEnum.InicioSesion);
        fila.Detalle.Should().BeNull();
    }
}
