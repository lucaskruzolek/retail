using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express los prechequeos de la migración AgregarRubrosYUnicidadClasificacion (Track 6.5,
/// D-24): si CATEGORIAS tiene filas o MARCAS tiene nombres activos duplicados, aborta con THROW y un mensaje claro
/// en lugar de fallar a mitad del DDL con un error críptico de FK o de índice.
/// </summary>
public class MigracionRubrosYUnicidadTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261017141345_AgregarIndicesPaginacionCatalogos";
    private const string MigracionNueva = "20261017201923_AgregarRubrosYUnicidadClasificacion";

    private readonly string _dbName = $"RetailDb_Test_MigRubros_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(MigracionAnterior);
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
    public async Task Up_ConCategoriasExistentes_AbortaConError50001QueMencionaD16()
    {
        // Arrange: una categoría cargada con el esquema anterior (sin id_rubro).
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO CATEGORIAS (nombre_categoria, created_at) VALUES ('Escolar', SYSUTCDATETIME());");

        // Act
        var act = () => _context.GetService<IMigrator>().MigrateAsync(MigracionNueva);

        // Assert
        var excepcion = (await act.Should().ThrowAsync<SqlException>()).Which;
        excepcion.Number.Should().Be(50001);
        excepcion.Message.Should().Contain("D-16");
    }

    [Fact]
    public async Task Up_ConMarcasActivasDuplicadasSinDistinguirMayusculas_AbortaConError50002QueListaLosNombres()
    {
        // Arrange: "Bic" y "BIC" son el mismo nombre bajo la colación CI_AI que usará el índice único.
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO MARCAS (nombre_marca, created_at) VALUES
                ('Bic', SYSUTCDATETIME()),
                ('BIC', SYSUTCDATETIME()),
                ('Maped', SYSUTCDATETIME());
            """);

        // Act
        var act = () => _context.GetService<IMigrator>().MigrateAsync(MigracionNueva);

        // Assert: la lista incluye los duplicados y no la marca que no se repite.
        var excepcion = (await act.Should().ThrowAsync<SqlException>()).Which;
        excepcion.Number.Should().Be(50002);
        excepcion.Message.Should().Contain("'Bic'").And.Contain("'BIC'").And.NotContain("Maped");
    }

    [Fact]
    public async Task Up_ConMarcaDuplicadaDadaDeBaja_NoAbortaPorqueElIndiceSoloCuentaLasActivas()
    {
        // Arrange
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO MARCAS (nombre_marca, created_at, deleted_at) VALUES ('Bic', SYSUTCDATETIME(), SYSUTCDATETIME());
            INSERT INTO MARCAS (nombre_marca, created_at) VALUES ('BIC', SYSUTCDATETIME());
            """);

        // Act
        await _context.GetService<IMigrator>().MigrateAsync(MigracionNueva);

        // Assert
        (await _context.Marcas.CountAsync()).Should().Be(1);
    }
}
