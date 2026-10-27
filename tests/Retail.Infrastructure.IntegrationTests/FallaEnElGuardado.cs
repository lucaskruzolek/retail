using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Simula una falla de la base en el guardado número <c>numeroDeGuardado</c> de un contexto, para probar contra
/// SQL Server real que una operación de varios guardados en una transacción no deja nada a medias.
/// </summary>
internal sealed class FallaEnElGuardado(int numeroDeGuardado) : SaveChangesInterceptor
{
    private int _guardados;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _guardados++;
        if (_guardados == numeroDeGuardado)
        {
            throw new InvalidOperationException("Falla simulada de la base.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
