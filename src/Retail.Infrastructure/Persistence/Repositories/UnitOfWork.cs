using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Persistence;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Repositories;

/// <summary>
/// Coordinador de persistencia atómica y transacciones ACID en Entity Framework Core.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly RetailDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public UnitOfWork(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            DescartarCambios();
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario modificó los mismos datos mientras se procesaba la operación. Vuelva a intentarlo.",
                ex);
        }
        catch
        {
            // La base revirtió la transacción, pero el contexto vive mientras dura la pantalla (H-19): sin limpiarlo,
            // los cambios rechazados se guardarían con la próxima operación exitosa.
            DescartarCambios();
            throw;
        }
    }

    public void DescartarCambios()
    {
        _context.ChangeTracker.Clear();
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No hay una transacción activa para confirmar.");
        }

        try
        {
            // El SaveChangesAsync propio, no el del contexto: traduce los conflictos y descarta los cambios si falla.
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentTransaction?.Dispose();
            }

            _disposed = true;
        }
    }
}
