using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Autoriza operaciones reservadas al Gerente en los servicios de reportes, terminales y caja. El actor se vuelve a
/// leer de la base en cada operación: una baja o un cambio de rol surten efecto sin esperar a que cierre la sesión, y
/// ocultar un botón en la UI no es un control de acceso.
/// </summary>
internal static class AutorizacionGerente
{
    /// <summary>
    /// Devuelve el actor si existe, está activo y es Gerente; si no, lanza <see cref="OperacionNoAutorizadaException"/>
    /// con <paramref name="mensajeSiNoEsGerente"/>.
    /// </summary>
    public static async Task<Usuario> ExigirAsync(
        IRepository<Usuario> usuarios,
        int idActor,
        string mensajeSiNoEsGerente,
        CancellationToken cancellationToken)
    {
        var actor = idActor > 0
            ? await usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        if (actor is null)
        {
            throw new OperacionNoAutorizadaException("La operación fue solicitada por un usuario inexistente o dado de baja.");
        }

        if (!actor.EsGerente())
        {
            throw new OperacionNoAutorizadaException(mensajeSiNoEsGerente);
        }

        return actor;
    }
}
