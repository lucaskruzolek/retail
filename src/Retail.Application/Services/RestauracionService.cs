using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Orquesta una restauración con base temporal e intercambio (RF-22, D-17 a D-26): la copia se restaura aparte, se
/// valida y se prepara ahí, y solo al final reemplaza a la base vigente, que queda intacta como base anterior.
/// </summary>
/// <remarks>
/// Todo lo que puede fallar ocurre antes de <see cref="IBaseRestaurada.PromoverAVigenteAsync"/>. Si algo falla, la base
/// temporal se libera sin promover (y por lo tanto se elimina) y la base vigente no se tocó.
/// </remarks>
public class RestauracionService : IRestauracionService
{
    private const string MensajeCopiaInterrumpida =
        "Copia interrumpida: quedó en curso dentro de la copia restaurada.";

    private readonly IRepository<Usuario> _usuarios;
    private readonly IRepository<RegistroBackup> _registros;
    private readonly IRepository<TurnoCaja> _turnos;
    private readonly IAuthService _authService;
    private readonly IBackupService _backupService;
    private readonly IRestauradorBaseDatos _restaurador;
    private readonly IBloqueoRestauracion _bloqueo;
    private readonly IArchivosBackup _archivos;
    private readonly IEsquemaBaseDatos _esquema;
    private readonly IPasswordHasher _passwordHasher;

    public RestauracionService(
        IRepository<Usuario> usuarios,
        IRepository<RegistroBackup> registros,
        IRepository<TurnoCaja> turnos,
        IAuthService authService,
        IBackupService backupService,
        IRestauradorBaseDatos restaurador,
        IBloqueoRestauracion bloqueo,
        IArchivosBackup archivos,
        IEsquemaBaseDatos esquema,
        IPasswordHasher passwordHasher)
    {
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _registros = registros ?? throw new ArgumentNullException(nameof(registros));
        _turnos = turnos ?? throw new ArgumentNullException(nameof(turnos));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _restaurador = restaurador ?? throw new ArgumentNullException(nameof(restaurador));
        _bloqueo = bloqueo ?? throw new ArgumentNullException(nameof(bloqueo));
        _archivos = archivos ?? throw new ArgumentNullException(nameof(archivos));
        _esquema = esquema ?? throw new ArgumentNullException(nameof(esquema));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<ResultadoRestauracionDto> RestaurarDesdeHistorialAsync(
        int idActor,
        string password,
        Guid referenciaCopia,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        var actor = idActor > 0
            ? await _usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        // Restaurar reemplaza la base completa: es exclusivo del Gerente (D-3), con reautenticación (D-8).
        if (actor is null || !actor.EsGerente())
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente puede restaurar una copia de seguridad.");
        }

        await _authService.VerificarCredencialAsync(idActor, password, cancellationToken);

        var encontrados = await _registros.FindAsync(r => r.Referencia == referenciaCopia, includeDeleted: false, cancellationToken);
        var registro = encontrados.SingleOrDefault();
        if (registro is null || !registro.EsRestaurable)
        {
            throw new RestauracionFallidaException(
                "La copia elegida no es restaurable: solo se restauran copias verificadas cuyo archivo se conserva.");
        }

        var copia = new CopiaARestaurar(
            registro.Referencia,
            registro.RutaArchivo,
            registro.TamanoBytes!.Value,
            registro.HashSha256!,
            registro.UltimaMigracion);

        return await RestaurarAsync(
            OrigenRestauracionEnum.Aplicacion,
            copia,
            (temporal, ct) => ExigirGerenteEnLaCopiaAsync(temporal, actor, ct),
            progreso,
            cancellationToken);
    }

    public async Task<ResultadoRestauracionDto> RestaurarDesdeArchivoAsync(
        string rutaArchivo,
        string nombreUsuario,
        string password,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaArchivo);
        ArgumentNullException.ThrowIfNull(nombreUsuario);
        ArgumentNullException.ThrowIfNull(password);

        ManifiestoBackupDto manifiesto;
        try
        {
            manifiesto = await _archivos.LeerManifiestoAsync(rutaArchivo, cancellationToken);
        }
        catch (Exception ex) when (DebeEnvolverse(ex))
        {
            throw new RestauracionFallidaException($"No se pudo leer el manifiesto de la copia: {ex.Message}", ex);
        }

        var copia = new CopiaARestaurar(
            manifiesto.Referencia,
            rutaArchivo,
            manifiesto.TamanoBytes,
            manifiesto.HashSha256,
            manifiesto.UltimaMigracion);

        return await RestaurarAsync(
            OrigenRestauracionEnum.Consola,
            copia,
            (temporal, ct) => VerificarCredencialEnLaCopiaAsync(temporal, nombreUsuario, password, ct),
            progreso,
            cancellationToken);
    }

    /// <summary>
    /// D-25: la UI y la consola reciben siempre un error legible. Se propagan sin cambios la cancelación, las reglas de
    /// negocio y autorización (<see cref="DomainException"/>, que incluye las credenciales inválidas) y los rechazos que
    /// ya son <see cref="RestauracionFallidaException"/>.
    /// </summary>
    private async Task<ResultadoRestauracionDto> RestaurarAsync(
        OrigenRestauracionEnum origen,
        CopiaARestaurar copia,
        Func<IBaseRestaurada, CancellationToken, Task<Usuario>> autorizarEnLaCopia,
        IProgress<int>? progreso,
        CancellationToken cancellationToken)
    {
        try
        {
            return await EjecutarRestauracionAsync(origen, copia, autorizarEnLaCopia, progreso, cancellationToken);
        }
        catch (Exception ex) when (DebeEnvolverse(ex))
        {
            throw new RestauracionFallidaException($"No se pudo completar la restauración: {ex.Message}", ex);
        }
    }

    private async Task<ResultadoRestauracionDto> EjecutarRestauracionAsync(
        OrigenRestauracionEnum origen,
        CopiaARestaurar copia,
        Func<IBaseRestaurada, CancellationToken, Task<Usuario>> autorizarEnLaCopia,
        IProgress<int>? progreso,
        CancellationToken cancellationToken)
    {
        await using var exclusividad = await _bloqueo.AdquirirExclusivoAsync(cancellationToken);

        await ExigirArchivoSinCambiosAsync(copia, cancellationToken);

        // R-3: el aviso de conciliación fiscal acompaña a toda restauración exitosa.
        var advertencias = new List<string> { ResultadoRestauracionDto.AvisoConciliacionArca };

        // D-26: "existe" y "responde" deciden la copia previa y los controles; "está al día", solo el historial.
        var vigente = await EvaluarBaseVigenteAsync(cancellationToken);
        if (vigente.Existe)
        {
            await ExigirSinOperacionesEnCursoAsync(cancellationToken);
        }
        else
        {
            advertencias.Add(
                "No existe una base vigente (por ejemplo, un equipo nuevo): no se tomó una copia previa ni hay historial " +
                "de respaldos que conservar.");
        }

        if (vigente.Existe && !vigente.EstaAlDia)
        {
            advertencias.Add(
                "La base vigente no tiene el esquema de esta versión: se tomó la copia previa, pero su historial de " +
                "respaldos no se pasó a la base restaurada (queda en la base anterior).");
        }

        await using var temporal = await _restaurador.RestaurarEnBaseTemporalAsync(copia.RutaArchivo, progreso, cancellationToken);

        // R-7: una copia de una versión posterior tiene tablas o columnas que esta aplicación no conoce.
        var estadoCopia = await temporal.ObtenerEstadoEsquemaAsync(cancellationToken);
        if (estadoCopia.MigracionesDesconocidas.Count > 0)
        {
            throw new RestauracionFallidaException(
                "La copia fue tomada con una versión más nueva de la aplicación. Actualice la aplicación antes de " +
                $"restaurarla (migraciones desconocidas: {string.Join(", ", estadoCopia.MigracionesDesconocidas)}).");
        }

        // Una copia vieja se pone al día antes de leer sus datos: el modelo actual no podría leer un esquema anterior.
        await temporal.AplicarMigracionesPendientesAsync(cancellationToken);

        var gerente = await autorizarEnLaCopia(temporal, cancellationToken);

        Guid? referenciaCopiaPrevia = null;
        if (vigente.Existe)
        {
            // El restore puede llevar minutos: los controles se repiten justo antes de la copia previa, que es la
            // última foto de los datos reales (TOCTOU).
            await ExigirSinOperacionesEnCursoAsync(cancellationToken);
            referenciaCopiaPrevia = await TomarCopiaPreviaAsync(origen, gerente, advertencias, cancellationToken);
        }

        if (vigente.EstaAlDia)
        {
            // R-1: el historial de la vigente (incluida la copia previa recién tomada) pasa a la base restaurada.
            await temporal.ImportarHistorialDeLaBaseVigenteAsync(cancellationToken);
        }

        await CerrarCopiasEnCursoAsync(temporal, copia, cancellationToken);

        var restauracion = RegistroRestauracion.Registrar(
            origen,
            gerente.Id,
            gerente.NombreUsuario,
            copia.Referencia,
            copia.RutaArchivo,
            copia.UltimaMigracion,
            referenciaCopiaPrevia,
            temporal.NombreBaseAnterior);

        await temporal.AgregarRestauracionAsync(restauracion, cancellationToken);
        await temporal.GuardarCambiosAsync(cancellationToken);

        // Punto de no retorno. Lo que sigue no se cancela: dejar el intercambio a medias sería peor que terminarlo.
        await temporal.PromoverAVigenteAsync(CancellationToken.None);

        await EliminarBasesAnterioresSinPropagarAsync(temporal.NombreBaseAnterior, advertencias);

        return new ResultadoRestauracionDto
        {
            ReferenciaRestauracion = restauracion.Referencia,
            ReferenciaCopia = copia.Referencia,
            ReferenciaCopiaPrevia = referenciaCopiaPrevia,
            BaseAnterior = temporal.NombreBaseAnterior,
            MigracionesAplicadas = estadoCopia.MigracionesPendientes,
            Advertencias = advertencias
        };
    }

    /// <summary>
    /// D-26: una base que no existe (PC nueva) permite continuar sin red previa; una que existe pero no responde puede
    /// tener los únicos datos reales, así que se aborta en lugar de reemplazarla a ciegas.
    /// </summary>
    private async Task<(bool Existe, bool EstaAlDia)> EvaluarBaseVigenteAsync(CancellationToken cancellationToken)
    {
        if (!await _restaurador.ExisteBaseVigenteAsync(cancellationToken))
        {
            return (false, false);
        }

        var estado = await _esquema.ObtenerEstadoAsync(cancellationToken);
        if (!estado.PuedeConectar)
        {
            throw new RestauracionFallidaException(
                "La base vigente existe pero no responde. No se restaura sobre ella sin poder tomar una copia previa: " +
                "revise el servidor de base de datos.");
        }

        return (true, estado.EstaAlDia);
    }

    /// <summary>
    /// Desde la aplicación el actor ya se autenticó contra la vigente; la copia igual debe tener un Gerente activo:
    /// si no, la aplicación arrancaría bloqueada después de restaurarla.
    /// </summary>
    private static async Task<Usuario> ExigirGerenteEnLaCopiaAsync(IBaseRestaurada temporal, Usuario actor, CancellationToken cancellationToken)
    {
        if (!await temporal.ExisteGerenteActivoAsync(cancellationToken))
        {
            throw new RestauracionFallidaException(
                "La copia no tiene ningún Gerente activo: después de restaurarla nadie podría administrar el sistema.");
        }

        return actor;
    }

    /// <summary>
    /// D-18: quien restaura desde la consola demuestra que era Gerente en esa copia. Un usuario inexistente, dado de
    /// baja, que no es Gerente o cuya contraseña la eligió otro (por ejemplo, la sembrada en bases viejas) recibe la
    /// misma respuesta que una contraseña incorrecta.
    /// </summary>
    private async Task<Usuario> VerificarCredencialEnLaCopiaAsync(
        IBaseRestaurada temporal,
        string nombreUsuario,
        string password,
        CancellationToken cancellationToken)
    {
        var usuario = await temporal.BuscarUsuarioActivoAsync(ReglasTexto.NormalizarNombreUsuario(nombreUsuario), cancellationToken);

        if (usuario is null || !usuario.EsGerente() || usuario.DebeCambiarPassword || string.IsNullOrEmpty(password)
            || !_passwordHasher.VerifyPassword(password, usuario.PasswordHash))
        {
            throw new CredencialesInvalidasException();
        }

        return usuario;
    }

    private async Task<Guid?> TomarCopiaPreviaAsync(
        OrigenRestauracionEnum origen,
        Usuario gerente,
        List<string> advertencias,
        CancellationToken cancellationToken)
    {
        try
        {
            var copiaPrevia = await _backupService.CrearBackupPreRestauracionAsync(
                gerente.Id, gerente.NombreUsuario, progreso: null, cancellationToken);
            return copiaPrevia.Referencia;
        }
        catch (BackupFallidoException ex) when (origen == OrigenRestauracionEnum.Consola)
        {
            // En una recuperación ante desastres la vigente puede estar dañada justamente por eso no se puede copiar.
            // La base anterior igual se conserva intacta (D-17). Desde la aplicación, en cambio, la vigente está sana y
            // una copia que falla indica un problema real (por ejemplo, el disco): se aborta.
            advertencias.Add($"No se pudo tomar la copia previa de la base vigente: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// R-18: el archivo <c>.bak</c> contiene su propio registro todavía <c>EnCurso</c>, y cualquier otra copia que
    /// estuviera en curso en ese momento quedó interrumpida. Si no se cierran, el índice de copia única bloquearía
    /// todas las copias siguientes.
    /// </summary>
    private static async Task CerrarCopiasEnCursoAsync(IBaseRestaurada temporal, CopiaARestaurar copia, CancellationToken cancellationToken)
    {
        var enCurso = await temporal.ObtenerCopiasEnCursoAsync(cancellationToken);

        foreach (var registro in enCurso)
        {
            if (registro.Referencia == copia.Referencia)
            {
                // Es la copia que se acaba de restaurar: se completó y se verificó, porque su huella coincide.
                registro.RegistrarExito(copia.TamanoBytes, copia.HashSha256);
                registro.MarcarVerificado();
            }
            else
            {
                registro.RegistrarFallo(MensajeCopiaInterrumpida);
            }
        }
    }

    /// <summary>
    /// Después del punto de no retorno la restauración ya está hecha: una falla de limpieza no puede informarse como
    /// una restauración fallida.
    /// </summary>
    private async Task EliminarBasesAnterioresSinPropagarAsync(string? baseAConservar, List<string> advertencias)
    {
        try
        {
            var noEliminadas = await _restaurador.EliminarBasesAnterioresAsync(baseAConservar, CancellationToken.None);
            advertencias.AddRange(noEliminadas.Select(b =>
                $"No se pudo eliminar la base anterior '{b}'; se reintentará en la próxima restauración."));
        }
        catch (Exception ex)
        {
            advertencias.Add($"No se pudieron eliminar las bases anteriores viejas ({ex.Message}); se reintentará en la próxima restauración.");
        }
    }

    private async Task ExigirArchivoSinCambiosAsync(CopiaARestaurar copia, CancellationToken cancellationToken)
    {
        ArchivoBackupDto huella;
        try
        {
            huella = await _archivos.CalcularHuellaAsync(copia.RutaArchivo, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RestauracionFallidaException($"No se puede leer el archivo de la copia '{copia.RutaArchivo}': {ex.Message}", ex);
        }

        if (huella.TamanoBytes != copia.TamanoBytes
            || !string.Equals(huella.HashSha256, copia.HashSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new RestauracionFallidaException(
                "El archivo de la copia cambió desde que se verificó (tamaño o SHA-256 distintos): está dañado o fue reemplazado.");
        }
    }

    private async Task ExigirSinOperacionesEnCursoAsync(CancellationToken cancellationToken)
    {
        // R-11: restaurar con la caja abierta perdería los movimientos del turno y dejaría el arqueo inconsistente.
        var abiertos = await _turnos.FindAsync(t => t.Estado == EstadoTurnoEnum.Abierto, includeDeleted: false, cancellationToken);
        if (abiertos.Count > 0)
        {
            throw new RestauracionFallidaException("Hay un turno de caja abierto. Ciérrelo antes de restaurar una copia.");
        }

        // Una base de una versión anterior al historial no tiene copias registradas que puedan estar en curso.
        if (!await _esquema.ExisteHistorialDeBackupsAsync(cancellationToken))
        {
            return;
        }

        var enCurso = await _registros.FindAsync(r => r.Estado == EstadoBackupEnum.EnCurso, includeDeleted: false, cancellationToken);
        if (enCurso.Count > 0)
        {
            throw new RestauracionFallidaException("Hay una copia de seguridad en curso. Espere a que termine antes de restaurar.");
        }
    }

    private static bool DebeEnvolverse(Exception ex) =>
        ex is not (OperationCanceledException or DomainException or RestauracionFallidaException);

    /// <summary>
    /// Lo que se sabe de la copia antes de restaurarla, venga del historial o del manifiesto.
    /// </summary>
    private sealed record CopiaARestaurar(
        Guid Referencia,
        string RutaArchivo,
        long TamanoBytes,
        string HashSha256,
        string UltimaMigracion);
}
