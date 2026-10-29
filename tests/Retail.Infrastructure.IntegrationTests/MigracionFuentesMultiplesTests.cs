using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra SQL Server Express el relleno de la migración AgregarFuentesMultiples (Track 4.3, L3a, A-16): cada
/// vínculo vigente de ARTICULOS.id_catalogo_proveedor queda registrado también como fuente en
/// CATALOGOS_PROVEEDORES.id_articulo; los vínculos de artículos dados de baja no se migran y la FK nueva rechaza un
/// artículo inexistente.
/// </summary>
public class MigracionFuentesMultiplesTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnterior = "20261028211913_AgregarPedidos";
    private const string MigracionNueva = "20261029080028_AgregarFuentesMultiples";

    private readonly string _dbName = $"RetailDb_Test_MigFuentes_{Guid.NewGuid():N}";
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
    public async Task Up_ConVinculosExistentes_RegistraCadaPrincipalVigenteComoFuente()
    {
        // Arrange: vínculos 1:1 cargados con el esquema anterior.
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO PROVEEDORES (razon_social, cuit, created_at) VALUES ('Distribuidora Sur', '30712345671', SYSUTCDATETIME());
            DECLARE @proveedor INT = SCOPE_IDENTITY();

            INSERT INTO CATALOGOS_PROVEEDORES (id_proveedor, codigo_proveedor, descripcion_proveedor, precio_costo,
                                               fecha_actualizacion, created_at) VALUES
                (@proveedor, 'VIG', 'Ítem de un artículo vigente', 100, SYSUTCDATETIME(), SYSUTCDATETIME()),
                (@proveedor, 'BAJ', 'Ítem de un artículo dado de baja', 100, SYSUTCDATETIME(), SYSUTCDATETIME()),
                (@proveedor, 'LIB', 'Ítem sin vincular', 100, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO ARTICULOS (descripcion, costo_reposicion, porcentaje_ganancia, precio_venta, stock_actual,
                                   stock_minimo, es_servicio, id_catalogo_proveedor, created_at, deleted_at) VALUES
                ('Vigente', 100, 50, 150, 1, 0, 0,
                    (SELECT id_catalogo FROM CATALOGOS_PROVEEDORES WHERE codigo_proveedor = 'VIG'), SYSUTCDATETIME(), NULL),
                ('De baja', 100, 50, 150, 1, 0, 0,
                    (SELECT id_catalogo FROM CATALOGOS_PROVEEDORES WHERE codigo_proveedor = 'BAJ'), SYSUTCDATETIME(), SYSUTCDATETIME()),
                ('Manual', 100, 50, 150, 1, 0, 0, NULL, SYSUTCDATETIME(), NULL);
            """);

        // Act
        await _context.GetService<IMigrator>().MigrateAsync(MigracionNueva);

        // Assert
        var filas = await _context.Database
            .SqlQuery<FilaFuente>(
                $"""
                SELECT c.codigo_proveedor AS Codigo, a.descripcion AS Articulo
                FROM CATALOGOS_PROVEEDORES c
                LEFT JOIN ARTICULOS a ON a.id_articulo = c.id_articulo
                """)
            .ToListAsync();

        filas.Select(f => (f.Codigo, f.Articulo)).Should().BeEquivalentTo(new (string, string?)[]
        {
            ("VIG", "Vigente"),
            ("BAJ", null),
            ("LIB", null)
        });
    }

    [Fact]
    public async Task Up_FuenteConArticuloInexistente_LaFkLaRechaza()
    {
        await _context.GetService<IMigrator>().MigrateAsync(MigracionNueva);

        var act = () => _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO PROVEEDORES (razon_social, cuit, created_at) VALUES ('Distribuidora Sur', '30712345671', SYSUTCDATETIME());
            INSERT INTO CATALOGOS_PROVEEDORES (id_proveedor, codigo_proveedor, descripcion_proveedor, precio_costo,
                                               fecha_actualizacion, created_at, id_articulo)
                VALUES (SCOPE_IDENTITY(), 'X', 'Ítem', 100, SYSUTCDATETIME(), SYSUTCDATETIME(), 999999);
            """);

        (await act.Should().ThrowAsync<SqlException>())
            .WithMessage("*FK_CATALOGOS_PROVEEDORES_ARTICULOS_id_articulo*");
    }

    // SqlQuery de EF Core 8 materializa los tipos no mapeados por sus propiedades.
    private sealed class FilaFuente
    {
        public string Codigo { get; set; } = string.Empty;

        public string? Articulo { get; set; }
    }
}
