using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express el relleno de la migración AgregarDiccionariosYOrigenClasificacion (Track 6.6,
/// M-30): los artículos que ya tenían categoría o marca reciben el origen 'Manual' antes de crear los CHECK, incluidos
/// los dados de baja; los que no las tenían quedan sin clasificar.
/// </summary>
public class MigracionDiccionariosYOrigenTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261022010404_AgregarTipoEventoYDetalleAuditoriaAccesos";
    private const string MigracionNueva = "20261022160838_AgregarDiccionariosYOrigenClasificacion";

    private readonly string _dbName = $"RetailDb_Test_MigOrigen_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
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
    public async Task Up_ConArticulosClasificados_CompletaElOrigenManualYCreaLosCheck()
    {
        // Arrange: artículos cargados con el esquema anterior (sin columnas de origen).
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO RUBROS (nombre_rubro, created_at) VALUES ('General', SYSUTCDATETIME());
            INSERT INTO CATEGORIAS (nombre_categoria, id_rubro, created_at) VALUES ('Mochilas', SCOPE_IDENTITY(), SYSUTCDATETIME());
            DECLARE @categoria INT = SCOPE_IDENTITY();
            INSERT INTO MARCAS (nombre_marca, created_at) VALUES ('Mooving', SYSUTCDATETIME());
            DECLARE @marca INT = SCOPE_IDENTITY();

            INSERT INTO ARTICULOS (descripcion, id_categoria, id_marca, costo_reposicion, porcentaje_ganancia, precio_venta,
                                   stock_actual, stock_minimo, es_servicio, created_at, deleted_at) VALUES
                ('Completo', @categoria, @marca, 100, 50, 150, 1, 0, 0, SYSUTCDATETIME(), NULL),
                ('Solo categoría', @categoria, NULL, 100, 50, 150, 1, 0, 0, SYSUTCDATETIME(), NULL),
                ('Sin clasificar', NULL, NULL, 100, 50, 150, 1, 0, 0, SYSUTCDATETIME(), NULL),
                ('De baja', @categoria, @marca, 100, 50, 150, 1, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
            """);

        // Act
        await _context.GetService<IMigrator>().MigrateAsync(MigracionNueva);

        // Assert
        var filas = await _context.Database
            .SqlQuery<FilaOrigen>(
                $"SELECT descripcion AS Descripcion, origen_categoria AS OrigenCategoria, origen_marca AS OrigenMarca FROM ARTICULOS")
            .ToListAsync();

        filas.Select(f => (f.Descripcion, f.OrigenCategoria, f.OrigenMarca)).Should().BeEquivalentTo(new (string, string?, string?)[]
        {
            ("Completo", "Manual", "Manual"),
            ("Solo categoría", "Manual", null),
            ("Sin clasificar", null, null),
            ("De baja", "Manual", "Manual")
        });
    }

    // SqlQuery de EF Core 8 materializa los tipos no mapeados por sus propiedades.
    private sealed class FilaOrigen
    {
        public string Descripcion { get; set; } = string.Empty;

        public string? OrigenCategoria { get; set; }

        public string? OrigenMarca { get; set; }
    }
}
