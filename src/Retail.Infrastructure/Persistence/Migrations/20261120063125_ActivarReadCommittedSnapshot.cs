using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retail.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Activa READ_COMMITTED_SNAPSHOT (RCSI) en la base (Track 6.7, Paquete 6, H11 de la auditoría multiterminal).
    /// </summary>
    /// <remarks>
    /// Por qué: con el aislamiento READ COMMITTED por defecto, un lector espera a que el escritor confirme (y viceversa).
    /// Un reporte largo o una grilla abierta en una terminal frenaba los cobros de otra (RNF-01), y buena parte de los
    /// deadlocks entre terminales eran de tipo lector contra escritor. Con RCSI el lector ve la última versión confirmada
    /// de la fila sin esperar, y esos bloqueos desaparecen.
    /// <para>
    /// Nota: el creador de bases de EF Core para SQL Server ya activa RCSI al crear una base nueva (fuera de Azure), así
    /// que las instalaciones que nacieron de MigrateAsync lo tienen. Esta migración lo garantiza en las que no (por
    /// ejemplo, una copia restaurada de una base creada de otra forma) y deja el requisito explícito en el historial. Es
    /// idempotente y, además, solo ejecuta el ALTER si la opción está apagada: en una base que ya la tiene no desconecta
    /// a nadie (WITH ROLLBACK IMMEDIATE cierra las demás conexiones aunque la opción no cambie).
    /// </para>
    /// <para>
    /// Costo: SQL Server guarda versiones de las filas modificadas en tempdb. Para el volumen de una librería es
    /// despreciable.
    /// </para>
    /// <para>
    /// Por qué es seguro con los controles de concurrencia existentes: ninguno depende de que una lectura bloquee a una
    /// escritura. Los tokens de concurrencia (rowversion) se comprueban en el UPDATE, que sigue tomando bloqueos de
    /// escritura; los índices únicos los valida el motor al insertar; sp_getapplock es un bloqueo de aplicación ajeno al
    /// aislamiento; y la transacción SERIALIZABLE con UPDLOCK de SesionesActivasSqlServer pide el nivel y los bloqueos
    /// de forma explícita, que RCSI no modifica (solo cambia el comportamiento de READ COMMITTED).
    /// </para>
    /// <para>
    /// ALTER DATABASE no puede correr dentro de una transacción, por eso se ejecuta con suppressTransaction. WITH
    /// ROLLBACK IMMEDIATE cierra las demás conexiones abiertas a la base: la migración se aplica en modo mantenimiento
    /// (sin terminales operando) o sobre una base recién restaurada. Una copia anterior a esta migración se restaura con
    /// RCSI apagado; la migración queda pendiente y MigrateAsync, que corre tras cada restauración, la vuelve a aplicar.
    /// Una copia tomada después ya trae la opción: las opciones de la base viajan dentro del respaldo.
    /// </para>
    /// </remarks>
    public partial class ActivarReadCommittedSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()) = 0 " +
                "EXEC(N'ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;');",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()) = 1 " +
                "EXEC(N'ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;');",
                suppressTransaction: true);
        }
    }
}
