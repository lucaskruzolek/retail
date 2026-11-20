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
    public static Task<Usuario> ExigirAsync(
        IRepository<Usuario> usuarios,
        int idActor,
        string mensajeSiNoEsGerente,
        CancellationToken cancellationToken)
    {
        return ExigirRolAsync(usuarios, idActor, u => u.EsGerente(), mensajeSiNoEsGerente, cancellationToken);
    }

    /// <summary>
    /// Igual que <see cref="ExigirAsync"/>, pero también admite al Administrador: para las tareas de mantenimiento de la
    /// base que el Administrador ejecuta con autorización de un Gerente (control dual, Track 6.3.1), como preparar el
    /// mantenimiento antes de una restauración.
    /// </summary>
    public static Task<Usuario> ExigirGerenteOAdministradorAsync(
        IRepository<Usuario> usuarios,
        int idActor,
        string mensajeSiNoCorresponde,
        CancellationToken cancellationToken)
    {
        return ExigirRolAsync(
            usuarios,
            idActor,
            u => u.EsGerente() || u.EsAdministrador(),
            mensajeSiNoCorresponde,
            cancellationToken);
    }

    private static async Task<Usuario> ExigirRolAsync(
        IRepository<Usuario> usuarios,
        int idActor,
        Func<Usuario, bool> tieneElRol,
        string mensajeSiNoTieneElRol,
        CancellationToken cancellationToken)
    {
        var actor = idActor > 0
            ? await usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        if (actor is null)
        {
            throw new OperacionNoAutorizadaException("La operación fue solicitada por un usuario inexistente o dado de baja.");
        }

        if (!tieneElRol(actor))
        {
            throw new OperacionNoAutorizadaException(mensajeSiNoTieneElRol);
        }

        return actor;
    }
}
