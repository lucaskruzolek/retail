using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Unicidad de nombres de clasificación a nivel de base (D-4, D-22): índices únicos filtrados sobre columnas con
/// colación Modern_Spanish_CI_AI, más la clave foránea de Categoría hacia Rubro.
/// </summary>
public class ClasificacionUnicidadTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_ClasUnic_{Guid.NewGuid():N}";
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

    private async Task<int> IdRubroAsync()
    {
        var rubro = Rubro.Crear("General");
        _context.Rubros.Add(rubro);
        await _context.SaveChangesAsync();
        return rubro.Id;
    }

    [Theory]
    [InlineData("LAPICES")]
    [InlineData("Lapices")]
    public async Task GuardarCategoria_ConNombreIgualSinTildesNiMayusculas_LanzaDbUpdateException(string segundoNombre)
    {
        var idRubro = await IdRubroAsync();
        _context.Categorias.Add(Categoria.Crear("Lápices", idRubro));
        await _context.SaveChangesAsync();

        _context.Categorias.Add(Categoria.Crear(segundoNombre, idRubro));
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Theory]
    [InlineData("LAPICES")]
    [InlineData("Lapices")]
    public async Task GuardarMarca_ConNombreIgualSinTildesNiMayusculas_LanzaDbUpdateException(string segundoNombre)
    {
        _context.Marcas.Add(Marca.Crear("Lápices"));
        await _context.SaveChangesAsync();

        _context.Marcas.Add(Marca.Crear(segundoNombre));
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Theory]
    [InlineData("LAPICES")]
    [InlineData("Lapices")]
    public async Task GuardarRubro_ConNombreIgualSinTildesNiMayusculas_LanzaDbUpdateException(string segundoNombre)
    {
        _context.Rubros.Add(Rubro.Crear("Lápices"));
        await _context.SaveChangesAsync();

        _context.Rubros.Add(Rubro.Crear(segundoNombre));
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GuardarCategoria_AnioYAnoSeparados_NoColisionan()
    {
        var idRubro = await IdRubroAsync();
        _context.Categorias.Add(Categoria.Crear("Año", idRubro));
        await _context.SaveChangesAsync();

        _context.Categorias.Add(Categoria.Crear("Ano", idRubro));
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GuardarCategoria_ConNombreDeCategoriaDadaDeBaja_EstaPermitido()
    {
        var idRubro = await IdRubroAsync();
        var vieja = Categoria.Crear("Lápices", idRubro);
        _context.Categorias.Add(vieja);
        await _context.SaveChangesAsync();
        vieja.MarkAsDeleted();
        await _context.SaveChangesAsync();

        _context.Categorias.Add(Categoria.Crear("Lápices", idRubro));
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GuardarCategoria_ConRubroInexistente_LanzaDbUpdateException()
    {
        _context.Categorias.Add(Categoria.Crear("Huérfana", 999_999));
        var accion = () => _context.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>();
    }
}
