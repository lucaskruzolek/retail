using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Colección de <see cref="PermisosTareaProgramadaIntegracionTests"/>: corre sola, sin ninguna otra clase de pruebas en paralelo.
/// </summary>
/// <remarks>
/// Estas pruebas suplantan cuentas con permisos mínimos y ejecutan BACKUP y RESTORE VERIFYONLY sobre una base propia. Las
/// pruebas de restauración corren en paralelo contra la misma instancia Express (RESTORE, DROP, SINGLE_USER, ClearAllPools).
/// Dos de estas pruebas fallaron una vez de cada cinco corridas completas y nunca aisladas; no se pudo capturar el error, así que
/// esto es un endurecimiento por descarte: aislarla elimina la competencia por el servidor sin tocar lo que las pruebas verifican.
/// </remarks>
[CollectionDefinition(Nombre, DisableParallelization = true)]
public sealed class PermisosTareaProgramadaColeccion : ICollectionFixture<PermisosTareaProgramadaFixture>
{
    public const string Nombre = "Permisos de la tarea programada (sin paralelismo)";
}
