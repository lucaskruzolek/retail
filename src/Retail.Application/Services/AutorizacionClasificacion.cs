using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Centraliza la autorización de las escrituras sobre rubros, categorías y marcas (Track 6.5, D-5), para que
/// los tres servicios apliquen exactamente la misma regla y el mismo mensaje. El actor se vuelve a leer de la
/// base en cada operación: así una baja o un cambio de rol surten efecto sin esperar a que cierre la sesión.
/// </summary>
internal static class AutorizacionClasificacion
{
    public static async Task ExigirAsync(IRepository<Usuario> usuarios, int idActor, CancellationToken cancellationToken)
    {
        var actor = idActor > 0
            ? await usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        if (actor is null)
        {
            throw new OperacionNoAutorizadaException("La operación fue solicitada por un usuario inexistente o dado de baja.");
        }

        if (!actor.PuedeAdministrarClasificacion())
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente o un Encargado puede administrar rubros, categorías y marcas.");
        }
    }
}
