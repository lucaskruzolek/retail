namespace Retail.Domain.Enums;

/// <summary>
/// Origen de una copia de seguridad de la base (RF-22).
/// </summary>
public enum TipoBackupEnum
{
    /// <summary>
    /// Pedido por un Gerente desde la aplicación, con reautenticación.
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Periódico, ejecutado por el sistema: la tarea del Programador de tareas de Windows (D-28).
    /// </summary>
    Automatico = 2,

    /// <summary>
    /// Red de seguridad que se toma justo antes de restaurar otra copia.
    /// </summary>
    PreRestauracion = 3,

    /// <summary>
    /// Red de seguridad que se toma justo antes de aplicar migraciones (<c>Retail.Cli migrar</c>).
    /// </summary>
    PreMigracion = 4
}
