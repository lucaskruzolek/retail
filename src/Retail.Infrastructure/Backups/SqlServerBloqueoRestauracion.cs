using Microsoft.EntityFrameworkCore;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Persistence.Concurrencia;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Bloqueo de lectores y escritor entre restauraciones y copias, implementado con un <c>sp_getapplock</c> de SQL Server.
/// </summary>
/// <remarks>
/// Semántica de lectores y escritor: la restauración toma el bloqueo <c>Exclusive</c> (escritor, uno solo y sin ningún
/// lector) y las copias toman <c>Shared</c> (lectores, pueden convivir entre sí). Como el bloqueo vive en el motor,
/// vale entre procesos distintos: la aplicación, <c>Retail.Cli</c> y la tarea programada.
/// <para>
/// Cada bloqueo usa una conexión <b>propia</b> a <c>master</c>, con el pool desactivado: un bloqueo de sesión se libera
/// al cerrar la conexión, y con pool la conexión física volvería al pool con el bloqueo tomado y contaminaría a otra
/// operación. Se apunta a <c>master</c> porque la base vigente puede estar siendo reemplazada o no existir (PC nueva),
/// y el bloqueo no debe depender de ella. El recurso lleva el nombre de la base, así que bases distintas (por ejemplo,
/// las de las pruebas en paralelo) no se bloquean entre sí.
/// </para>
/// </remarks>
public class SqlServerBloqueoRestauracion : IBloqueoRestauracion
{
    private const string PrefijoRecurso = "Retail.Restauracion.";

    /// <summary>
    /// Espera de las copias por el bloqueo compartido. Es corta a propósito: cubre el instante en que otra copia, al
    /// iniciar, toma el exclusivo para cerrar huérfanos (B-0), pero no hace esperar a una copia detrás de una restauración
    /// (que dura minutos): esa se rechaza.
    /// </summary>
    public static readonly TimeSpan EsperaCompartidoPorDefecto = TimeSpan.FromSeconds(10);

    private readonly RetailDbContext _context;
    private readonly TimeSpan _esperaCompartido;

    public SqlServerBloqueoRestauracion(RetailDbContext context, TimeSpan? esperaCompartido = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _esperaCompartido = esperaCompartido ?? EsperaCompartidoPorDefecto;
    }

    public async Task<IAsyncDisposable> AdquirirExclusivoAsync(CancellationToken cancellationToken = default)
    {
        var bloqueo = await IntentarAdquirirExclusivoAsync(cancellationToken);
        return bloqueo
            ?? throw new RestauracionFallidaException(
                "Hay otra restauración o una copia de seguridad en curso. Espere a que termine e intente de nuevo.");
    }

    public async Task<IAsyncDisposable?> IntentarAdquirirExclusivoAsync(CancellationToken cancellationToken = default)
    {
        return await TomarAsync("Exclusive", TimeSpan.Zero, cancellationToken);
    }

    public async Task<IAsyncDisposable> AdquirirCompartidoAsync(CancellationToken cancellationToken = default)
    {
        var bloqueo = await TomarAsync("Shared", _esperaCompartido, cancellationToken);
        return bloqueo
            ?? throw new BackupFallidoException(
                "Hay una restauración de la base u otra copia iniciándose en este momento. Intente de nuevo en unos segundos.");
    }

    /// <summary>
    /// Intenta tomar el bloqueo esperando como máximo <paramref name="espera"/> (cero: no espera). Devuelve <c>null</c> si
    /// no está disponible.
    /// </summary>
    private async Task<IAsyncDisposable?> TomarAsync(string modo, TimeSpan espera, CancellationToken cancellationToken)
    {
        var cadena = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No hay una cadena de conexión configurada para la base.");

        var recurso = PrefijoRecurso + BloqueoDeAplicacionSqlServer.ObtenerNombreDeBase(cadena);
        return await BloqueoDeAplicacionSqlServer.TomarAsync(cadena, recurso, modo, espera, cancellationToken);
    }
}
