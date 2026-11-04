using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Cronometra cada <c>SaveChanges</c> de un contexto de punta a punta: la detección de cambios del Change Tracker,
/// el armado de los lotes y su envío a SQL Server.
/// </summary>
internal sealed class MedidorDeGuardado : SaveChangesInterceptor
{
    private readonly Stopwatch _cronometro = new();

    public int Guardados { get; private set; }

    public TimeSpan Duracion { get; private set; }

    public void Reiniciar()
    {
        Guardados = 0;
        Duracion = TimeSpan.Zero;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _cronometro.Restart();
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        _cronometro.Stop();
        Guardados++;
        Duracion += _cronometro.Elapsed;
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
