using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;
using Retail.Infrastructure.Security;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

public class DbInitializerTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Init_{Guid.NewGuid():N}";
    private readonly PasswordHasher _passwordHasher = new();
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();
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
    public async Task InitializeAsync_BaseVacia_DebePoblarRolesAdminCategoriasYVeinteArticulos()
    {
        // Act
        await DbInitializer.InitializeAsync(_context, _passwordHasher);

        // Assert - 1. Roles
        var roles = await _context.Roles.AsNoTracking().ToListAsync();
        roles.Should().HaveCount(3);
        roles.Select(r => r.NombreRol).Should().Contain(new[]
        {
            nameof(RolUsuarioEnum.Cajero),
            nameof(RolUsuarioEnum.Encargado),
            nameof(RolUsuarioEnum.Gerente)
        });

        // Assert - 2. Usuario Administrador
        var admin = await _context.Usuarios
            .Include(u => u.Rol)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NombreUsuario == "admin");

        admin.Should().NotBeNull();
        admin!.NombreCompleto.Should().Be("Administrador General");
        admin.Rol.Should().NotBeNull();
        admin.Rol!.NombreRol.Should().Be(nameof(RolUsuarioEnum.Gerente));
        _passwordHasher.VerifyPassword("Admin123!", admin.PasswordHash).Should().BeTrue();

        // Assert - 3. Categorías y Marcas
        var categorias = await _context.Categorias.AsNoTracking().ToListAsync();
        categorias.Should().HaveCount(5);

        var marcas = await _context.Marcas.AsNoTracking().ToListAsync();
        marcas.Should().HaveCount(6);

        // Assert - 4. Artículos
        var articulos = await _context.Articulos.AsNoTracking().ToListAsync();
        articulos.Should().HaveCount(20);

        // Assert - 5. Verificar productos artesanales con código nulo (RF-04)
        var articulosSinCodigo = articulos.Where(a => a.CodigoBarras == null).ToList();
        articulosSinCodigo.Should().HaveCount(2);
    }

    [Fact]
    public async Task InitializeAsync_EjecucionRepetida_DebeSerIdempotenteSinDuplicarRegistros()
    {
        // Arrange
        await DbInitializer.InitializeAsync(_context, _passwordHasher);

        // Act
        await DbInitializer.InitializeAsync(_context, _passwordHasher);

        // Assert
        var totalRoles = await _context.Roles.CountAsync();
        var totalUsuarios = await _context.Usuarios.CountAsync();
        var totalArticulos = await _context.Articulos.CountAsync();

        totalRoles.Should().Be(3);
        totalUsuarios.Should().Be(1);
        totalArticulos.Should().Be(20);
    }
}
