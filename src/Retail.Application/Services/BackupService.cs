using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Orquesta una copia de seguridad completa (RF-22): autoriza, registra el intento, delega la copia y la verificación
/// al motor, deja el manifiesto junto al archivo y aplica la política de retención.
/// </summary>
/// <remarks>
/// El registro se guarda en <c>EnCurso</c> antes de copiar, para que el índice único de la base impida una segunda
/// copia simultánea (R-12) y para que un corte a mitad de camino deje evidencia del intento.
/// </remarks>
public class BackupService : IBackupService
{
    private const string OperadorSistema = "Sistema";

    private readonly IRepository<RegistroBackup> _registros;
    private readonly IRepository<Usuario> _usuarios;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly IEjecutorBackupBaseDatos _ejecutor;
    private readonly IArchivosBackup _archivos;
    private readonly IEsquemaBaseDatos _esquema;
    private readonly IBloqueoRestauracion _bloqueo;
    private readonly PoliticaBackup _politica;

    public BackupService(
        IRepository<RegistroBackup> registros,
        IRepository<Usuario> usuarios,
        IUnitOfWork unitOfWork,
        IAuthService authService,
        IEjecutorBackupBaseDatos ejecutor,
        IArchivosBackup archivos,
        IEsquemaBaseDatos esquema,
        IBloqueoRestauracion bloqueo,
        PoliticaBackup politica)
    {
        _registros = registros ?? throw new ArgumentNullException(nameof(registros));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _ejecutor = ejecutor ?? throw new ArgumentNullException(nameof(ejecutor));
        _archivos = archivos ?? throw new ArgumentNullException(nameof(archivos));
        _esquema = esquema ?? throw new ArgumentNullException(nameof(esquema));
        _bloqueo = bloqueo ?? throw new ArgumentNullException(nameof(bloqueo));
        _politica = politica ?? throw new ArgumentNullException(nameof(politica));
    }

    public async Task<IReadOnlyList<RegistroBackupDto>> ListarAsync(int idActor, CancellationToken cancellationToken = default)
    {
        var actor = await ObtenerActorAsync(idActor, cancellationToken);
        if (!actor.EsGerente() && !actor.EsAdministrador())
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente o un Administrador puede consultar el historial de copias.");
        }

        var registros = await _registros.ListAllAsync(includeDeleted: false, cancellationToken);

        return registros
            .OrderByDescending(r => r.FechaInicio)
            .ThenByDescending(r => r.Id)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ResultadoBackupDto> CrearBackupManualAsync(
        int idActor,
        string password,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await ObtenerActorAsync(idActor, cancellationToken);

        // La copia contiene la base completa (datos personales, Ley 25.326): la piden el Gerente o el Administrador
        // (D-32), cada uno con su propia contraseña. Copiar no restaura ni modifica datos.
        if (!actor.EsGerente() && !actor.EsAdministrador())
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente o un Administrador puede generar una copia de seguridad manual.");
        }

        // Reautenticación (D-8): una sesión abierta en el mostrador no alcanza.
        await _authService.VerificarCredencialAsync(idActor, password, cancellationToken);

        await CerrarCopiasInterrumpidasAsync(cancellationToken);

        await using var bloqueo = await _bloqueo.AdquirirCompartidoAsync(cancellationToken);
        return await CopiarConHistorialAsync(TipoBackupEnum.Manual, actor.Id, actor.NombreUsuario, progreso, cancellationToken);
    }

    public async Task<ResultadoBackupDto> CrearBackupDelSistemaAsync(
        TipoBackupEnum tipo,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        if (tipo is not (TipoBackupEnum.Automatico or TipoBackupEnum.PreMigracion))
        {
            throw new DomainException($"Una copia de tipo {tipo} no la dispara el sistema.");
        }

        await CerrarCopiasInterrumpidasAsync(cancellationToken);

        // Ninguna copia corre mientras una restauración reemplaza la base (C-2).
        await using var bloqueo = await _bloqueo.AdquirirCompartidoAsync(cancellationToken);

        // Una base de una versión anterior todavía no tiene la tabla del historial, pero igual debe poder
        // respaldarse antes de migrarla: nunca se migra sin red de seguridad.
        if (!await _esquema.ExisteHistorialDeBackupsAsync(cancellationToken))
        {
            return await CopiarSinHistorialAsync(tipo, OperadorSistema, progreso, cancellationToken);
        }

        return await CopiarConHistorialAsync(tipo, null, null, progreso, cancellationToken);
    }

    public async Task<ResultadoBackupDto> CrearBackupPreRestauracionAsync(
        int idUsuario,
        string nombreUsuario,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        // No toma el bloqueo compartido: corre bajo el bloqueo exclusivo de la restauración que la pide.

        // Desde la consola, la base vigente puede ser de una versión anterior al historial: igual se respalda.
        if (!await _esquema.ExisteHistorialDeBackupsAsync(cancellationToken))
        {
            return await CopiarSinHistorialAsync(TipoBackupEnum.PreRestauracion, nombreUsuario, progreso, cancellationToken);
        }

        return await CopiarConHistorialAsync(TipoBackupEnum.PreRestauracion, idUsuario, nombreUsuario, progreso, cancellationToken);
    }

    /// <summary>
    /// B-0: autorrepara los registros huérfanos. Una copia real sostiene el bloqueo compartido mientras corre (incluida su
    /// verificación), y es el motor quien lo suelta si su proceso muere. Por lo tanto, si se obtiene el exclusivo, ninguna
    /// copia ni restauración está corriendo y todo registro <c>EnCurso</c> o <c>Exitoso</c> (el proceso murió verificando)
    /// quedó huérfano: es un hecho garantizado por el motor, no una heurística de tiempo. Si no se obtiene, hay una copia o
    /// una restauración en marcha y su registro es legítimo. De cada huérfano se borra además el <c>.bak</c> y el
    /// manifiesto parciales.
    /// </summary>
    /// <remarks>
    /// El exclusivo se libera antes de tomar el compartido. El hueco entre ambos es inofensivo: una copia que arranque en
    /// ese instante es legítima y la rechaza el control de "copia en curso" de <c>CopiarConHistorialAsync</c>; una
    /// restauración que arranque en ese instante hace fallar la toma del compartido. No se usa en la copia previa a
    /// restaurar, que corre bajo el exclusivo de su restauración y esa ya cerró los huérfanos.
    /// </remarks>
    private async Task CerrarCopiasInterrumpidasAsync(CancellationToken cancellationToken)
    {
        await using var exclusivo = await _bloqueo.IntentarAdquirirExclusivoAsync(cancellationToken);
        if (exclusivo is null)
        {
            return;
        }

        // Una base de una versión anterior al historial no tiene registros que puedan estar en curso.
        if (!await _esquema.ExisteHistorialDeBackupsAsync(cancellationToken))
        {
            return;
        }

        var huerfanos = await _registros.FindAsync(
            r => r.Estado == EstadoBackupEnum.EnCurso || r.Estado == EstadoBackupEnum.Exitoso,
            includeDeleted: false,
            cancellationToken);
        if (huerfanos.Count == 0)
        {
            return;
        }

        foreach (var registro in huerfanos)
        {
            registro.RegistrarFallo(RegistroBackup.MensajeCopiaHuerfana);
            await _registros.UpdateAsync(registro, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Recién con el registro ya cerrado se borran los restos: el archivo parcial no es restaurable y, como el registro
        // pasó a Fallido, la retención (que solo mira Exitoso y Verificado) ya no lo vería nunca. Un archivo que no se pueda
        // borrar ahora (el motor puede conservarlo abierto unos instantes tras la caída del proceso) queda como residuo en
        // el directorio: no es restaurable y no ocupa un lugar de la retención; se limpia a mano.
        foreach (var registro in huerfanos)
        {
            EliminarSinPropagar(registro.RutaArchivo);
        }
    }

    private async Task<ResultadoBackupDto> CopiarConHistorialAsync(
        TipoBackupEnum tipo,
        int? idUsuario,
        string? nombreUsuario,
        IProgress<int>? progreso,
        CancellationToken cancellationToken)
    {
        var enCurso = await _registros.FindAsync(r => r.Estado == EstadoBackupEnum.EnCurso, includeDeleted: false, cancellationToken);
        if (enCurso.Count > 0)
        {
            throw new DomainException(
                $"Ya hay una copia de seguridad en curso (iniciada a las {enCurso[0].FechaInicio.ToLocalTime():HH:mm}). Espere a que termine.");
        }

        var ultimaMigracion = await ObtenerUltimaMigracionAsync(cancellationToken);
        var ruta = _archivos.PrepararRutaNueva(tipo);
        var registro = RegistroBackup.Iniciar(tipo, idUsuario, nombreUsuario, ruta, ultimaMigracion, _politica.VersionApp);

        try
        {
            await _registros.AddAsync(registro, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (RestriccionDeUnicidadException ex)
        {
            // Dos procesos pasaron a la vez el control de "copia en curso": el índice único de la base dejó pasar solo a uno.
            // El único índice único que puede chocar acá es el de EnCurso (la Referencia es un GUID nuevo).
            throw new DomainException("Ya hay una copia de seguridad en curso. Espere a que termine.", ex);
        }

        try
        {
            var archivo = await _ejecutor.CrearCopiaAsync(ruta, progreso, cancellationToken);
            registro.RegistrarExito(archivo.TamanoBytes, archivo.HashSha256);
            await GuardarAsync(registro, cancellationToken);

            await _ejecutor.VerificarCopiaAsync(ruta, cancellationToken);
            await _archivos.EscribirManifiestoAsync(ruta, CrearManifiesto(registro, archivo), cancellationToken);

            // Recién con el archivo verificado y su manifiesto escrito la copia es apta para restaurar.
            registro.MarcarVerificado();
            await GuardarAsync(registro, cancellationToken);
        }
        catch (Exception ex)
        {
            await RegistrarFalloAsync(registro, ex);

            if (ex is OperationCanceledException)
            {
                throw;
            }

            throw new BackupFallidoException($"No se pudo completar la copia de seguridad: {DescripcionDeFalla.Para(ex)}", ex);
        }

        var (purgadas, errores) = await AplicarRetencionAsync(cancellationToken);

        return new ResultadoBackupDto
        {
            Referencia = registro.Referencia,
            RutaArchivo = ruta,
            TamanoBytes = registro.TamanoBytes!.Value,
            HashSha256 = registro.HashSha256!,
            RegistradaEnHistorial = true,
            CopiasPurgadas = purgadas,
            ErroresDeRetencion = errores
        };
    }

    private async Task<ResultadoBackupDto> CopiarSinHistorialAsync(
        TipoBackupEnum tipo,
        string operador,
        IProgress<int>? progreso,
        CancellationToken cancellationToken)
    {
        var ultimaMigracion = await ObtenerUltimaMigracionAsync(cancellationToken);
        var ruta = _archivos.PrepararRutaNueva(tipo);
        var referencia = Guid.NewGuid();
        var fechaInicio = DateTime.UtcNow;

        try
        {
            var archivo = await _ejecutor.CrearCopiaAsync(ruta, progreso, cancellationToken);
            await _ejecutor.VerificarCopiaAsync(ruta, cancellationToken);

            var manifiesto = new ManifiestoBackupDto
            {
                Referencia = referencia,
                Tipo = tipo.ToString(),
                FechaInicioUtc = fechaInicio,
                Operador = operador,
                ArchivoBak = Path.GetFileName(ruta),
                TamanoBytes = archivo.TamanoBytes,
                HashSha256 = archivo.HashSha256,
                UltimaMigracion = ultimaMigracion,
                VersionApp = _politica.VersionApp,
                RegistradaEnHistorial = false
            };
            await _archivos.EscribirManifiestoAsync(ruta, manifiesto, cancellationToken);

            return new ResultadoBackupDto
            {
                Referencia = referencia,
                RutaArchivo = ruta,
                TamanoBytes = archivo.TamanoBytes,
                HashSha256 = archivo.HashSha256,
                RegistradaEnHistorial = false
            };
        }
        catch (OperationCanceledException)
        {
            // Igual que en CopiarConHistorialAsync: una copia cancelada no deja su .bak parcial en el disco.
            EliminarSinPropagar(ruta);
            throw;
        }
        catch (Exception ex)
        {
            EliminarSinPropagar(ruta);
            throw new BackupFallidoException($"No se pudo completar la copia de seguridad: {DescripcionDeFalla.Para(ex)}", ex);
        }
    }

    /// <summary>
    /// Conserva las <see cref="PoliticaBackup.CantidadMaximaCopias"/> copias más recientes con archivo. Las previas a
    /// restaurar tienen su propia regla (D-24): no ocupan esos lugares y se conservan
    /// <see cref="PoliticaBackup.DiasRetencionPreRestauracion"/> días. Las demás se borran del disco, pero su registro
    /// queda como historia (<c>ArchivoPurgado</c>, Ley 3).
    /// </summary>
    private async Task<(IReadOnlyList<string> Purgadas, IReadOnlyList<string> Errores)> AplicarRetencionAsync(
        CancellationToken cancellationToken)
    {
        var conArchivo = await _registros.FindAsync(
            r => r.Estado == EstadoBackupEnum.Exitoso || r.Estado == EstadoBackupEnum.Verificado,
            includeDeleted: false,
            cancellationToken);

        var excedentesPorCantidad = conArchivo
            .Where(r => r.Tipo != TipoBackupEnum.PreRestauracion)
            .OrderByDescending(r => r.FechaInicio)
            .ThenByDescending(r => r.Id)
            .Skip(_politica.CantidadMaximaCopias);

        // Un restore equivocado puede detectarse tarde: la copia de los datos reales no compite con las copias diarias.
        var limitePreRestauracion = DateTime.UtcNow.AddDays(-_politica.DiasRetencionPreRestauracion);
        var excedentesPorAntiguedad = conArchivo
            .Where(r => r.Tipo == TipoBackupEnum.PreRestauracion && r.FechaInicio < limitePreRestauracion);

        var excedentes = excedentesPorCantidad.Concat(excedentesPorAntiguedad).ToList();

        var purgadas = new List<string>();
        var errores = new List<string>();

        foreach (var excedente in excedentes)
        {
            try
            {
                _archivos.EliminarCopia(excedente.RutaArchivo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Un archivo que no se pudo borrar no invalida la copia nueva: se informa y se reintenta la próxima vez.
                errores.Add($"{excedente.RutaArchivo}: {ex.Message}");
                continue;
            }

            excedente.MarcarArchivoPurgado();
            await _registros.UpdateAsync(excedente, cancellationToken);
            purgadas.Add(excedente.RutaArchivo);
        }

        if (purgadas.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (purgadas, errores);
    }

    /// <summary>
    /// Deja el intento como <c>Fallido</c> y borra el archivo parcial. Usa un token propio: aunque la operación se
    /// haya cancelado, el historial debe reflejar lo que pasó.
    /// </summary>
    private async Task RegistrarFalloAsync(RegistroBackup registro, Exception causa)
    {
        EliminarSinPropagar(registro.RutaArchivo);

        try
        {
            registro.RegistrarFallo(causa is OperationCanceledException ? "Copia cancelada." : causa.Message);
            await GuardarAsync(registro, CancellationToken.None);
        }
        catch (Exception)
        {
            // Si tampoco se puede guardar el fallo (por ejemplo, el servidor dejó de responder), prevalece la
            // excepción original, que es la que explica el problema. El registro queda EnCurso y la autorreparación
            // de huérfanos (B-0) lo cierra en la próxima copia o restauración.
        }
    }

    private void EliminarSinPropagar(string ruta)
    {
        try
        {
            _archivos.EliminarCopia(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El archivo parcial queda en disco: no es restaurable porque su registro no está Verificado.
        }
    }

    private async Task GuardarAsync(RegistroBackup registro, CancellationToken cancellationToken)
    {
        await _registros.UpdateAsync(registro, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> ObtenerUltimaMigracionAsync(CancellationToken cancellationToken)
    {
        return await _esquema.ObtenerUltimaMigracionAplicadaAsync(cancellationToken)
            ?? throw new DomainException("La base no tiene migraciones aplicadas: no hay nada que respaldar.");
    }

    private async Task<Usuario> ObtenerActorAsync(int idActor, CancellationToken cancellationToken)
    {
        var actor = idActor > 0
            ? await _usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        return actor ?? throw new OperacionNoAutorizadaException(
            "La operación fue solicitada por un usuario inexistente o dado de baja.");
    }

    private ManifiestoBackupDto CrearManifiesto(RegistroBackup registro, ArchivoBackupDto archivo) => new()
    {
        Referencia = registro.Referencia,
        Tipo = registro.Tipo.ToString(),
        FechaInicioUtc = registro.FechaInicio,
        Operador = registro.NombreUsuario ?? OperadorSistema,
        ArchivoBak = Path.GetFileName(registro.RutaArchivo),
        TamanoBytes = archivo.TamanoBytes,
        HashSha256 = archivo.HashSha256,
        UltimaMigracion = registro.UltimaMigracion,
        VersionApp = _politica.VersionApp,
        RegistradaEnHistorial = true
    };

    private static RegistroBackupDto MapToDto(RegistroBackup registro) => new()
    {
        IdRegistroBackup = registro.Id,
        Referencia = registro.Referencia,
        Tipo = registro.Tipo,
        Estado = registro.Estado,
        Operador = registro.NombreUsuario ?? OperadorSistema,
        FechaInicio = registro.FechaInicio,
        FechaFin = registro.FechaFin,
        RutaArchivo = registro.RutaArchivo,
        TamanoBytes = registro.TamanoBytes,
        MensajeError = registro.MensajeError,
        EsRestaurable = registro.EsRestaurable
    };
}
