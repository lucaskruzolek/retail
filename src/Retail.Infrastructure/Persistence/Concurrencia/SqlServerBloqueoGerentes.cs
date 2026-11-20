using Microsoft.EntityFrameworkCore;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Concurrencia;

/// <summary>
/// Serializa entre terminales las operaciones que pueden dejar al sistema sin Gerente, con un <c>sp_getapplock</c>
/// <c>Exclusive</c> de SQL Server (ver <see cref="BloqueoDeAplicacionSqlServer"/>). El recurso lleva el nombre de la
/// base, así que bases distintas (por ejemplo, las de las pruebas en paralelo) no se bloquean entre sí.
/// </summary>
public class SqlServerBloqueoGerentes : IBloqueoGerentes
{
    private const string PrefijoRecurso = "Retail.Gerentes.";

    /// <summary>
    /// Espera por el bloqueo. Las operaciones protegidas duran milisegundos, así que diez segundos cubren de sobra a otra
    /// terminal que esté terminando; pasado ese tiempo, se informa el conflicto en lugar de colgar la pantalla.
    /// </summary>
    public static readonly TimeSpan EsperaPorDefecto = TimeSpan.FromSeconds(10);

    private readonly RetailDbContext _context;
    private readonly TimeSpan _espera;

    public SqlServerBloqueoGerentes(RetailDbContext context, TimeSpan? espera = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _espera = espera ?? EsperaPorDefecto;
    }

    public async Task<IAsyncDisposable> AdquirirAsync(CancellationToken cancellationToken = default)
    {
        var cadena = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No hay una cadena de conexión configurada para la base.");

        var recurso = PrefijoRecurso + BloqueoDeAplicacionSqlServer.ObtenerNombreDeBase(cadena);
        var bloqueo = await BloqueoDeAplicacionSqlServer.TomarAsync(cadena, recurso, "Exclusive", _espera, cancellationToken);

        return bloqueo
            ?? throw new ConflictoDeConcurrenciaException(
                "Otra terminal está modificando una cuenta de Gerente en este momento. Intente de nuevo en unos segundos.");
    }
}
