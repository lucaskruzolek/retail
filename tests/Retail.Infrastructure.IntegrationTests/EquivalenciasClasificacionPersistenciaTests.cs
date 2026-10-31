using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Persistencia de las equivalencias de clasificación de un proveedor y de sus textos de clasificación en el catálogo
/// (Track 6.6, Fase 3b, M-12, M-13, M-31, M-32) contra SQL Server Express.
/// </summary>
public class EquivalenciasClasificacionPersistenciaTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Equiv_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
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
    public async Task GuardarProveedor_ConEquivalencias_LasRecuperaConElAgregado()
    {
        var idCategoria = await CrearCategoriaAsync();
        var marca = Marca.Crear("Rivadavia");
        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();

        var proveedor = CrearProveedor();
        proveedor.AsignarEquivalenciaCategoria("Escolar", "Cuadernos T/D", idCategoria);
        proveedor.AsignarEquivalenciaCategoria(null, "Varios", null);
        proveedor.AsignarEquivalenciaMarca("rivadavia", marca.Id);
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var leido = await _context.Proveedores.Include(p => p.Equivalencias).SingleAsync(p => p.Id == proveedor.Id);

        leido.Equivalencias.Select(e => (e.Tipo, e.TextoRubro, e.Texto, e.IdCategoria, e.IdMarca, e.EsIgnorada))
            .Should().BeEquivalentTo(new (TipoEquivalenciaEnum, string?, string, int?, int?, bool)[]
            {
                (TipoEquivalenciaEnum.Categoria, "ESCOLAR", "CUADERNOS T/D", idCategoria, null, false),
                (TipoEquivalenciaEnum.Categoria, null, "VARIOS", null, null, true),
                (TipoEquivalenciaEnum.Marca, null, "RIVADAVIA", null, marca.Id, false)
            });
    }

    [Fact]
    public async Task ReasignarEquivalencia_ProveedorCargado_ActualizaElDestinoSinCrearOtraFila()
    {
        var idCategoria = await CrearCategoriaAsync();
        var proveedor = CrearProveedor();
        proveedor.AsignarEquivalenciaCategoria("ESCOLAR", "CUADERNOS", null);
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var cargado = await _context.Proveedores.Include(p => p.Equivalencias).SingleAsync(p => p.Id == proveedor.Id);
        cargado.AsignarEquivalenciaCategoria("escolar", "cuadernos", idCategoria);
        await _context.SaveChangesAsync();

        var filas = await _context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM EQUIVALENCIAS_CLASIFICACION")
            .SingleAsync();
        filas.Should().Be(1);
        cargado.Equivalencias.Single().IdCategoria.Should().Be(idCategoria);
    }

    [Fact]
    public async Task InsertarEquivalencia_ClaveRepetidaSinRubro_LaRechazaElIndiceUnico()
    {
        var proveedor = CrearProveedor();
        proveedor.AsignarEquivalenciaCategoria(null, "VARIOS", null);
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        // Fuera del dominio: dos NULL en texto_rubro cuentan como iguales en un índice único de SQL Server.
        var accion = () => _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO EQUIVALENCIAS_CLASIFICACION (id_proveedor, tipo, texto, created_at) VALUES ({proveedor.Id}, 'Categoria', 'VARIOS', SYSUTCDATETIME())");

        await accion.Should().ThrowAsync<SqlException>()
            .Where(e => e.Message.Contains("IX_EQUIVALENCIAS_CLASIFICACION_id_proveedor_tipo_texto_rubro_texto"));
    }

    [Fact]
    public async Task GuardarEquivalencias_MismoValorEnDosProveedores_EstaPermitido()
    {
        var primero = CrearProveedor();
        primero.AsignarEquivalenciaCategoria(null, "VARIOS", null);
        var segundo = Proveedor.Crear("Otra Distribuidora", "20123456786");
        segundo.AsignarEquivalenciaCategoria(null, "VARIOS", null);
        _context.Proveedores.AddRange(primero, segundo);

        var accion = () => _context.SaveChangesAsync();

        await accion.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("INSERT INTO EQUIVALENCIAS_CLASIFICACION (id_proveedor, tipo, texto, id_marca, created_at) VALUES ({0}, 'Categoria', 'X', {1}, SYSUTCDATETIME())")]
    [InlineData("INSERT INTO EQUIVALENCIAS_CLASIFICACION (id_proveedor, tipo, texto, id_categoria, created_at) VALUES ({0}, 'Marca', 'X', {2}, SYSUTCDATETIME())")]
    [InlineData("INSERT INTO EQUIVALENCIAS_CLASIFICACION (id_proveedor, tipo, texto_rubro, texto, created_at) VALUES ({0}, 'Marca', 'R', 'X', SYSUTCDATETIME())")]
    [InlineData("INSERT INTO EQUIVALENCIAS_CLASIFICACION (id_proveedor, tipo, texto, created_at) VALUES ({0}, 'Inventado', 'X', SYSUTCDATETIME())")]
    public async Task InsertarEquivalencia_DestinoQueNoCorrespondeAlTipo_LaRechazaElCheck(string sql)
    {
        var idCategoria = await CrearCategoriaAsync();
        var marca = Marca.Crear("Rivadavia");
        var proveedor = CrearProveedor();
        _context.Marcas.Add(marca);
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        // {0}, {1} y {2} son parámetros de ExecuteSqlRawAsync, no reemplazos de texto.
        var accion = () => _context.Database.ExecuteSqlRawAsync(sql, proveedor.Id, marca.Id, idCategoria);

        await accion.Should().ThrowAsync<SqlException>()
            .Where(e => e.Message.Contains("CK_EQUIVALENCIAS_CLASIFICACION_Destino"));
    }

    [Fact]
    public async Task GuardarEquivalencia_ConCategoriaInexistente_LanzaDbUpdateException()
    {
        var proveedor = CrearProveedor();
        proveedor.AsignarEquivalenciaCategoria(null, "VARIOS", 9999);
        _context.Proveedores.Add(proveedor);

        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ActualizarCatalogo_ConTextosDelProveedor_LosGuardaComoVienen()
    {
        var proveedor = CrearProveedor();
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();
        var catalogo = new CatalogoProveedor
        {
            IdProveedor = proveedor.Id,
            CodigoProveedor = "P-1",
            DescripcionProveedor = "Cuaderno Rivadavia",
            CostoReposicion = 100m
        };
        catalogo.ActualizarPrecio(120m, rubroProveedor: "Escolar", categoriaProveedor: "Cuadernos T/D", marcaProveedor: "Rivadavia");
        _context.CatalogosProveedores.Add(catalogo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var leido = await _context.CatalogosProveedores.SingleAsync(c => c.Id == catalogo.Id);

        leido.RubroProveedor.Should().Be("Escolar");
        leido.CategoriaProveedor.Should().Be("Cuadernos T/D");
        leido.MarcaProveedor.Should().Be("Rivadavia");
    }

    private async Task<int> CrearCategoriaAsync()
    {
        var rubro = Rubro.Crear("Librería");
        _context.Rubros.Add(rubro);
        await _context.SaveChangesAsync();
        var categoria = Categoria.Crear("Cuadernos", rubro.Id);
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        return categoria.Id;
    }

    private static Proveedor CrearProveedor()
    {
        return Proveedor.Crear("Distribuidora Valida", "20111111112");
    }
}
