using Retail.Application.DTOs.Sistema;
using Retail.Domain.Entities;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// La base temporal donde se restauró una copia (D-17). Permite validarla y prepararla antes de que reemplace a la
/// vigente. Si se libera sin haber llamado a <see cref="PromoverAVigenteAsync"/>, se elimina: ante cualquier falla, la
/// base vigente sigue intacta.
/// </summary>
public interface IBaseRestaurada : IAsyncDisposable
{
    /// <summary>
    /// Nombre que recibirá la base vigente al ser reemplazada (<c>RetailDb_Anterior_&lt;fecha&gt;</c>), o <c>null</c> si
    /// no existe una base vigente (por ejemplo, en una PC nueva). Se conoce antes del intercambio para poder registrarlo.
    /// </summary>
    string? NombreBaseAnterior { get; }

    /// <summary>
    /// Compara las migraciones de la copia con las que conoce esta versión de la aplicación (R-7).
    /// </summary>
    Task<EstadoEsquemaDto> ObtenerEstadoEsquemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pone al día una copia de una versión anterior, antes de leer sus datos con el modelo actual.
    /// </summary>
    Task AplicarMigracionesPendientesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Usuario activo (no dado de baja) con ese nombre dentro de la copia, o <c>null</c>.
    /// </summary>
    Task<Usuario?> BuscarUsuarioActivoAsync(string nombreUsuario, CancellationToken cancellationToken = default);

    Task<bool> ExisteGerenteActivoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si la copia tiene el usuario de base de datos con el que corre la copia automática diaria. Ese usuario y sus
    /// permisos viven dentro de la base: una copia tomada antes de registrar la tarea programada no los tiene, y la tarea
    /// fallaría después de restaurarla.
    /// </summary>
    Task<bool> TieneUsuarioDeLaTareaProgramadaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Copia en la base temporal el historial de copias y de restauraciones de la base vigente (R-1). Ante un registro
    /// con la misma <c>Referencia</c>, prevalece el de la vigente, que es la versión más reciente de ese hecho (R-18).
    /// Exige que ambas bases tengan el mismo esquema.
    /// </summary>
    Task ImportarHistorialDeLaBaseVigenteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registros de copias que la base temporal tiene en <c>EnCurso</c> (R-18). Se devuelven con seguimiento de cambios:
    /// las transiciones se aplican con los métodos del agregado y se guardan con <see cref="GuardarCambiosAsync"/>.
    /// </summary>
    Task<IReadOnlyList<RegistroBackup>> ObtenerCopiasEnCursoAsync(CancellationToken cancellationToken = default);

    Task AgregarRestauracionAsync(RegistroRestauracion registro, CancellationToken cancellationToken = default);

    Task GuardarCambiosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Intercambio final: la vigente pasa a llamarse <see cref="NombreBaseAnterior"/> y la temporal ocupa su lugar. Si
    /// el segundo paso falla, revierte el primero. Cierra las conexiones abiertas contra la base vigente: la aplicación
    /// debe reiniciarse. Devuelve las advertencias para el operador: problemas posteriores al intercambio que no lo
    /// deshacen (por ejemplo, una base que no pudo volver a modo multiusuario).
    /// </summary>
    Task<IReadOnlyList<string>> PromoverAVigenteAsync(CancellationToken cancellationToken = default);
}
