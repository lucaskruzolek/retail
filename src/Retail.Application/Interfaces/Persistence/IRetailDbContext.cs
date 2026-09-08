namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Abstracción del contexto de persistencia para desacoplar Entity Framework Core de la capa de aplicación.
/// </summary>
public interface IRetailDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
