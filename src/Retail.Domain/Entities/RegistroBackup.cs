using System.Text.RegularExpressions;
using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que registra una copia de seguridad de la base y custodia su ciclo de vida (RF-22):
/// <c>EnCurso → Exitoso → Verificado</c>, con <c>Fallido</c> y <c>ArchivoPurgado</c> como estados finales.
/// </summary>
/// <remarks>
/// El operador se guarda como una foto (<see cref="IdUsuario"/> + <see cref="NombreUsuario"/>) y no como clave foránea:
/// al restaurar una copia, el historial de respaldos se vuelve a insertar en la base restaurada, donde un usuario
/// creado después de esa copia no existe (R-1). Además, un registro de auditoría debe conservar quién fue aunque la
/// cuenta cambie después.
/// </remarks>
public partial class RegistroBackup : BaseEntity, IAggregateRoot
{
    public const int LongitudMaximaRuta = 400;
    public const int LongitudMaximaMigracion = 150;
    public const int LongitudMaximaVersion = 50;
    public const int LongitudMaximaMensajeError = 1000;
    public const int LongitudHashSha256 = 64;

    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Iniciar.
    private RegistroBackup()
    {
    }

    /// <summary>
    /// Identidad estable entre bases: sobrevive a una restauración, cuando el <see cref="BaseEntity.Id"/> puede
    /// cambiar. Permite reinsertar el historial sin duplicarlo (R-1).
    /// </summary>
    public Guid Referencia { get; private set; }

    public TipoBackupEnum Tipo { get; private set; }

    public EstadoBackupEnum Estado { get; private set; }

    /// <summary>
    /// Operador que pidió la copia; <c>null</c> cuando la ejecutó el sistema.
    /// </summary>
    public int? IdUsuario { get; private set; }

    public string? NombreUsuario { get; private set; }

    public DateTime FechaInicio { get; private set; }

    public DateTime? FechaFin { get; private set; }

    /// <summary>
    /// Ruta completa del archivo <c>.bak</c>.
    /// </summary>
    public string RutaArchivo { get; private set; } = string.Empty;

    public long? TamanoBytes { get; private set; }

    /// <summary>
    /// SHA-256 del archivo en hexadecimal (minúsculas): permite detectar un archivo corrupto o reemplazado antes
    /// de restaurarlo.
    /// </summary>
    public string? HashSha256 { get; private set; }

    /// <summary>
    /// Última migración aplicada cuando se tomó la copia: impide restaurar un esquema que esta versión no conoce.
    /// </summary>
    public string UltimaMigracion { get; private set; } = string.Empty;

    public string VersionApp { get; private set; } = string.Empty;

    public string? MensajeError { get; private set; }

    /// <summary>
    /// Solo una copia cuyo archivo fue verificado (y no purgado) puede restaurarse.
    /// </summary>
    public bool EsRestaurable => Estado == EstadoBackupEnum.Verificado;

    public bool EsDelSistema => IdUsuario is null;

    /// <summary>
    /// Registra el comienzo de una copia. Las copias que pide una persona (manual y previa a restaurar) exigen el
    /// operador; las que dispara el sistema (automática y previa a migrar) no lo admiten: un registro no puede
    /// mentir sobre su origen.
    /// </summary>
    public static RegistroBackup Iniciar(
        TipoBackupEnum tipo,
        int? idUsuario,
        string? nombreUsuario,
        string rutaArchivo,
        string ultimaMigracion,
        string versionApp)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new DomainException($"El tipo de copia de seguridad '{tipo}' no es válido.");
        }

        var pideOperador = tipo is TipoBackupEnum.Manual or TipoBackupEnum.PreRestauracion;
        var tieneOperador = idUsuario is not null || !string.IsNullOrWhiteSpace(nombreUsuario);

        if (pideOperador && (idUsuario is null or <= 0 || string.IsNullOrWhiteSpace(nombreUsuario)))
        {
            throw new DomainException($"Una copia de tipo {tipo} debe registrar el operador que la pidió.");
        }

        if (!pideOperador && tieneOperador)
        {
            throw new DomainException($"Una copia de tipo {tipo} la ejecuta el sistema: no admite operador.");
        }

        return new RegistroBackup
        {
            Referencia = Guid.NewGuid(),
            Tipo = tipo,
            Estado = EstadoBackupEnum.EnCurso,
            IdUsuario = idUsuario,
            NombreUsuario = nombreUsuario?.Trim(),
            FechaInicio = DateTime.UtcNow,
            RutaArchivo = ExigirTexto(rutaArchivo, "la ruta del archivo", LongitudMaximaRuta),
            UltimaMigracion = ExigirTexto(ultimaMigracion, "la última migración", LongitudMaximaMigracion),
            VersionApp = ExigirTexto(versionApp, "la versión de la aplicación", LongitudMaximaVersion)
        };
    }

    /// <summary>
    /// El motor terminó de escribir el archivo.
    /// </summary>
    public void RegistrarExito(long tamanoBytes, string hashSha256)
    {
        ExigirEstado("registrar el éxito de", EstadoBackupEnum.EnCurso);

        if (tamanoBytes <= 0)
        {
            throw new DomainException("El tamaño de una copia de seguridad debe ser mayor a cero.");
        }

        var hash = hashSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!HashSha256Valido().IsMatch(hash))
        {
            throw new DomainException("El hash SHA-256 debe tener 64 caracteres hexadecimales.");
        }

        TamanoBytes = tamanoBytes;
        HashSha256 = hash;
        Estado = EstadoBackupEnum.Exitoso;
        FechaFin = DateTime.UtcNow;
    }

    /// <summary>
    /// El archivo pasó la verificación del motor y es apto para restaurar.
    /// </summary>
    public void MarcarVerificado()
    {
        ExigirEstado("verificar", EstadoBackupEnum.Exitoso);
        Estado = EstadoBackupEnum.Verificado;
    }

    /// <summary>
    /// La copia o su verificación fallaron. Se conserva el motivo para el diagnóstico.
    /// </summary>
    public void RegistrarFallo(string mensajeError)
    {
        ExigirEstado("registrar el fallo de", EstadoBackupEnum.EnCurso, EstadoBackupEnum.Exitoso);

        var mensaje = string.IsNullOrWhiteSpace(mensajeError) ? "Error sin detalle." : mensajeError.Trim();
        MensajeError = mensaje.Length > LongitudMaximaMensajeError ? mensaje[..LongitudMaximaMensajeError] : mensaje;
        Estado = EstadoBackupEnum.Fallido;
        FechaFin ??= DateTime.UtcNow;
    }

    /// <summary>
    /// La política de retención borró el archivo. El registro queda como historia: ya no es restaurable.
    /// </summary>
    public void MarcarArchivoPurgado()
    {
        ExigirEstado("purgar", EstadoBackupEnum.Exitoso, EstadoBackupEnum.Verificado);
        Estado = EstadoBackupEnum.ArchivoPurgado;
    }

    private void ExigirEstado(string operacion, params EstadoBackupEnum[] permitidos)
    {
        if (!permitidos.Contains(Estado))
        {
            throw new TransicionBackupInvalidaException(Estado, operacion);
        }
    }

    private static string ExigirTexto(string valor, string campo, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException($"Debe indicarse {campo} de la copia de seguridad.");
        }

        var texto = valor.Trim();
        if (texto.Length > longitudMaxima)
        {
            throw new DomainException($"{char.ToUpperInvariant(campo[0])}{campo[1..]} supera los {longitudMaxima} caracteres.");
        }

        return texto;
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex HashSha256Valido();
}
