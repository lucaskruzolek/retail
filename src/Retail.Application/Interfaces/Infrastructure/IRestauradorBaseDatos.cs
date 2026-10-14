namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Puerto hacia el motor para restaurar una copia sin tocar la base vigente hasta el final (D-17): la copia se restaura
/// en una base temporal, se valida ahí y recién entonces se intercambia. Application no conoce <c>RESTORE</c>,
/// <c>WITH MOVE</c> ni los nombres físicos de los archivos. La exclusividad entre restauraciones y copias la da
/// <see cref="IBloqueoRestauracion"/>.
/// </summary>
public interface IRestauradorBaseDatos
{
    /// <summary>
    /// Indica si el servidor tiene registrada la base vigente, aunque no responda (por ejemplo, porque está dañada).
    /// Distingue "no existe" (PC nueva: se restaura sin copia previa) de "existe pero no responde" (se aborta, D-26).
    /// </summary>
    Task<bool> ExisteBaseVigenteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si la base vigente tiene un turno de caja abierto (R-11). Usa SQL mínimo y no el modelo de EF: la vigente
    /// puede tener un esquema más viejo que esta versión (D-26), y materializar la entidad completa fallaría por las
    /// columnas agregadas después. Devuelve <c>false</c> si la base o la tabla no existen.
    /// </summary>
    Task<bool> HayTurnoDeCajaAbiertoEnBaseVigenteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restaura el archivo en una base temporal nueva. Verifica antes que haya espacio en disco (D-23) y descarta una
    /// base temporal que haya quedado de un corte anterior. Liberar la base devuelta sin promoverla la elimina.
    /// </summary>
    /// <param name="progreso">Porcentaje completado (0-100), si el llamador quiere mostrarlo.</param>
    Task<IBaseRestaurada> RestaurarEnBaseTemporalAsync(
        string rutaArchivo,
        IProgress<int>? progreso,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina las bases anteriores que dejaron restauraciones previas, salvo <paramref name="baseAConservar"/> (D-20).
    /// No lanza excepciones: devuelve las bases que no pudo eliminar, para informarlas.
    /// </summary>
    Task<IReadOnlyList<string>> EliminarBasesAnterioresAsync(string? baseAConservar, CancellationToken cancellationToken = default);
}
