using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Cuenta y cronometra los comandos que un contexto envía a SQL Server, separando lecturas (<c>SELECT</c>) de
/// escrituras (los lotes de <c>SaveChanges</c>). Sirve para medir cuántos viajes a la base hace una operación. La
/// duración de una lectura es la de ejecutar el comando, sin la de recorrer las filas que devuelve: eso se mide aparte.
/// </summary>
internal sealed class MedidorDeComandosSql : DbCommandInterceptor
{
    public int Lecturas { get; private set; }

    public int Escrituras { get; private set; }

    public TimeSpan DuracionLecturas { get; private set; }

    public TimeSpan DuracionEscrituras { get; private set; }

    /// <summary>Texto de cada lote de escritura, en orden: muestra qué SQL arma EF Core y si los lotes se repiten.</summary>
    public List<string> TextosDeEscritura { get; } = [];

    public void Reiniciar()
    {
        Lecturas = 0;
        Escrituras = 0;
        DuracionLecturas = TimeSpan.Zero;
        DuracionEscrituras = TimeSpan.Zero;
        TextosDeEscritura.Clear();
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Registrar(command, eventData.Duration);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Registrar(command, eventData.Duration);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Registrar(command, eventData.Duration);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void Registrar(DbCommand command, TimeSpan duracion)
    {
        // Los lotes de SaveChanges empiezan con "SET IMPLICIT_TRANSACTIONS OFF; SET NOCOUNT ON;" y siguen con los
        // UPDATE/INSERT; las consultas de EF Core empiezan con SELECT.
        if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            Lecturas++;
            DuracionLecturas += duracion;
        }
        else
        {
            Escrituras++;
            DuracionEscrituras += duracion;
            TextosDeEscritura.Add(command.CommandText);
        }
    }
}
