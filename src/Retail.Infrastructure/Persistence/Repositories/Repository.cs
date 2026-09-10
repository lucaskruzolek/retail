using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Common;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación genérica del patrón Repositorio para Raíces de Agregado con Entity Framework Core 8.
/// </summary>
/// <typeparam name="T">Tipo de la entidad raíz del agregado.</typeparam>
public class Repository<T> : IRepository<T> where T : BaseEntity, IAggregateRoot
{
    private readonly RetailDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<T>();
    }

    public virtual Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, includeDeleted: false, cancellationToken);
    }

    public virtual async Task<T?> GetByIdAsync(int id, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public virtual Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return ListAllAsync(includeDeleted: false, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> ListAllAsync(bool includeDeleted, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query.ToListAsync(cancellationToken);
    }

    public virtual Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return FindAsync(predicate, includeDeleted: false, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query.Where(predicate).ToListAsync(cancellationToken);
    }


    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkAsDeleted();
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }
}
