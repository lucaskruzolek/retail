namespace Retail.Domain.Enums;

/// <summary>
/// Ciclo de vida de una copia de seguridad (RF-22). Solo una copia <see cref="Verificado"/> puede restaurarse.
/// </summary>
public enum EstadoBackupEnum
{
    /// <summary>
    /// El motor está escribiendo el archivo. Puede haber una sola copia en este estado a la vez.
    /// </summary>
    EnCurso = 1,

    /// <summary>
    /// El archivo se escribió completo, pero todavía no se comprobó que sea legible.
    /// </summary>
    Exitoso = 2,

    /// <summary>
    /// El archivo pasó <c>RESTORE VERIFYONLY ... WITH CHECKSUM</c>: es apto para restaurar.
    /// </summary>
    Verificado = 3,

    /// <summary>
    /// La copia o su verificación fallaron. El registro se conserva como evidencia del intento.
    /// </summary>
    Fallido = 4,

    /// <summary>
    /// La política de retención borró el archivo. El registro se conserva (borrado lógico de la información, Ley 3).
    /// </summary>
    ArchivoPurgado = 5
}
