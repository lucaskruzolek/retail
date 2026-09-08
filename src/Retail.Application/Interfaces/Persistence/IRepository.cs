using System.Linq.Expressions;
using Retail.Domain.Common;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Contrato genérico de persistencia para Raíces de Agregado (DDD).
/// Restringido exclusivamente a entidades que implementan IAggregateRoot.
/// </summary>
/// <typeparam name="T">Entidad raíz del agregado.</typeparam>
public interface IRepository<T> where T : BaseEntity, IAggregateRoot
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}
