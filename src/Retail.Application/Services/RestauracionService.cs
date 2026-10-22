using Retail.Application.DTOs.Auth;
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

    /// <summary>
    /// Advertencia cuando la vigente tenía el usuario de la tarea programada y la copia restaurada no.
    /// </summary>
    public const string AvisoPermisosDeLaCopiaAutomatica =
        @"La copia restaurada no tiene los permisos de la copia automática diaria: vuelva a ejecutar scripts\registrar-tarea-backup.ps1 como administrador.";

    private readonly IRepository<Usuario> _usuarios;
    private readonly IRepository<RegistroBackup> _registros;
    private readonly IRepository<RegistroRestauracion> _restauraciones;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly IBackupService _backupService;
    private readonly IRestauradorBaseDatos _restaurador;
    private readonly IBloqueoRestauracion _bloqueo;
    private readonly IArchivosBackup _archivos;
    private readonly IEsquemaBaseDatos _esquema;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRegistroAccesosService _registroAccesos;

    public RestauracionService(
        IRepository<Usuario> usuarios,
        IRepository<RegistroBackup> registros,
        IRepository<RegistroRestauracion> restauraciones,
        IUnitOfWork unitOfWork,
        IAuthService authService,
        IBackupService backupService,
        IRestauradorBaseDatos restaurador,
        IBloqueoRestauracion bloqueo,
        IArchivosBackup archivos,
        IEsquemaBaseDatos esquema,
        IPasswordHasher passwordHasher,
        IRegistroAccesosService registroAccesos)
    {
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _registros = registros ?? throw new ArgumentNullException(nameof(registros));
        _restauraciones = restauraciones ?? throw new ArgumentNullException(nameof(restauraciones));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _restaurador = restaurador ?? throw new ArgumentNullException(nameof(restaurador));
        _bloqueo = bloqueo ?? throw new ArgumentNullException(nameof(bloqueo));
        _archivos = archivos ?? throw new ArgumentNullException(nameof(archivos));
        _esquema = esquema ?? throw new ArgumentNullException(nameof(esquema));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _registroAccesos = registroAccesos ?? throw new ArgumentNullException(nameof(registroAccesos));
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

        var copia = await BuscarCopiaRestaurableAsync(referenciaCopia, cancellationToken);

        return await RestaurarAsync(
            OrigenRestauracionEnum.Aplicacion,
            copia,
            (temporal, ct) => ExigirGerenteEnLaCopiaAsync(temporal, actor, ct),
            operador: null,
            progreso,
            cancellationToken);
    }

    public async Task<ResultadoRestauracionDto> RestaurarConAutorizacionDeGerenteAsync(
        int idActor,
        string passwordActor,
        CredencialAutorizanteDto autorizante,
        Guid referenciaCopia,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passwordActor);
        ArgumentNullException.ThrowIfNull(autorizante);

        var actor = idActor > 0
            ? await _usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        // Control dual (D-30): ejecuta un Administrador. El Gerente usa RestaurarDesdeHistorialAsync (D-8), y a quien no
        // puede ejecutar no se le verifica ninguna contraseña.
        if (actor is null || !actor.EsAdministrador())
        {
            throw new OperacionNoAutorizadaException("Solo un Administrador puede restaurar con la autorización de un Gerente.");
        }

        // D-31 / §3.1: el intento se cuenta y se guarda ANTES de verificar la credencial (conteo pesimista). Lanza
        // CuentaBloqueadaException si la cuenta ya agotó sus intentos, sin llegar a verificar ninguna contraseña.
        try
        {
            actor.RegistrarIntentoDeAutorizacion();
        }
        catch (CuentaBloqueadaException)
        {
            await RegistrarAutorizacionAsync(actor, ResultadoEventoAccesoEnum.Bloqueado, "Cuenta ya bloqueada", cancellationToken);
            throw;
        }

        await _usuarios.UpdateAsync(actor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // D-33: el Administrador también demuestra quién es (step-up). No se usa IAuthService.VerificarCredencialAsync: lanza
        // CuentaBloqueadaException si la cuenta EstaBloqueada y, en el 3.er intento, el contador ya vale 3 antes de
        // verificar, así que bloquearía aunque ambas contraseñas fueran correctas. Si falla, no se evalúa la del Gerente.
        if (string.IsNullOrEmpty(passwordActor) || !_passwordHasher.VerifyPassword(passwordActor, actor.PasswordHash))
        {
            // Después de guardar el contador: el hecho se registra con el desenlace que ya decidió el dominio. Nunca la contraseña.
            await RegistrarAutorizacionAsync(actor, ResultadoDeUnFallo(actor), "Contraseña del Administrador incorrecta", cancellationToken);

            if (actor.EstaBloqueada)
            {
                throw CuentaBloqueadaException.PorAutorizacionesFallidas();
            }

            throw new CredencialesInvalidasException("Su contraseña es incorrecta.");
        }

        Usuario gerenteAutorizante;
        try
        {
            var candidato = await BuscarAutorizanteAsync(autorizante, cancellationToken);
            gerenteAutorizante = ExigirGerenteConCredencial(candidato, autorizante.Password, "Usuario o contraseña del Gerente incorrectos.");
        }
        catch (CredencialesInvalidasException)
        {
            // El contador ya está guardado: se audita el intento (sin contraseñas) y se decide qué respuesta recibe el Administrador.
            await RegistrarAutorizacionAsync(
                actor,
                ResultadoDeUnFallo(actor),
                $"Gerente intentado: {ReglasTexto.NormalizarNombreUsuario(autorizante.NombreUsuario)}",
                cancellationToken);

            if (actor.EstaBloqueada)
            {
                throw CuentaBloqueadaException.PorAutorizacionesFallidas();
            }

            throw;
        }

        // Autorización válida: los intentos son consecutivos, así que el contador vuelve a cero aunque la restauración
        // falle después por otra causa.
        actor.RegistrarAutorizacionExitosa();
        await _usuarios.UpdateAsync(actor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // La fila se escribe ANTES de restaurar: así ya está en la base vigente cuando se importa su auditoría a la restaurada.
        await RegistrarAutorizacionAsync(
            actor, ResultadoEventoAccesoEnum.Aceptado, $"Gerente: {gerenteAutorizante.NombreUsuario}", cancellationToken);

        var copia = await BuscarCopiaRestaurableAsync(referenciaCopia, cancellationToken);

        return await RestaurarAsync(
            OrigenRestauracionEnum.Aplicacion,
            copia,
            (temporal, ct) => ExigirGerenteEnLaCopiaAsync(temporal, gerenteAutorizante, ct),
            operador: actor,
            progreso,
            cancellationToken);
    }

    private static ResultadoEventoAccesoEnum ResultadoDeUnFallo(Usuario actor) =>
        actor.EstaBloqueada ? ResultadoEventoAccesoEnum.Bloqueado : ResultadoEventoAccesoEnum.Rechazado;

    private Task RegistrarAutorizacionAsync(
        Usuario actor,
        ResultadoEventoAccesoEnum resultado,
        string detalle,
        CancellationToken cancellationToken)
    {
        return _registroAccesos.RegistrarEventoAsync(
            TipoEventoAccesoEnum.AutorizacionRestauracion, resultado, actor.NombreUsuario, actor.Id, detalle, cancellationToken);
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

        // B-3: la ruta se normaliza antes de cualquier trabajo. Una ruta relativa quedaría registrada tal cual, y una
        // demasiado larga fallaría recién al guardar el registro, después de un restore completo.
        // La longitud se controla solo sobre la ruta preparada (la que se registra): una ruta de origen larga es justamente
        // lo que PrepararParaRestaurarAsync resuelve al traer la copia a una carpeta corta.
        rutaArchivo = NormalizarRutaDeCopia(rutaArchivo, validarLongitud: false);

        // Con SQL Server Express el motor corre con otra cuenta de Windows y no puede leer, por ejemplo, la carpeta
        // Descargas del Gerente: la copia se trae primero al directorio de respaldos y desde ahí se usa para todo,
        // incluido el registro de auditoría. La longitud se controla sobre el resultado: el nombre puede haber ganado un sufijo.
        try
        {
            rutaArchivo = NormalizarRutaDeCopia(
                await _archivos.PrepararParaRestaurarAsync(rutaArchivo, cancellationToken), validarLongitud: true);
        }
        catch (Exception ex) when (DebeEnvolverse(ex))
        {
            throw new RestauracionFallidaException(
                $"No se pudo preparar la copia en el directorio de respaldos: {DescripcionDeFalla.Para(ex)}", ex);
        }

        ManifiestoBackupDto manifiesto;
        try
        {
            manifiesto = await _archivos.LeerManifiestoAsync(rutaArchivo, cancellationToken);
        }
        catch (Exception ex) when (DebeEnvolverse(ex))
        {
            throw new RestauracionFallidaException($"No se pudo leer el manifiesto de la copia: {DescripcionDeFalla.Para(ex)}", ex);
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
            operador: null,
            progreso,
            cancellationToken);
    }

    public async Task<RegistroRestauracionDto?> ObtenerUltimaRestauracionAsync(int idActor, CancellationToken cancellationToken = default)
    {
        var restauraciones = await ConsultarRestauracionesAsync(idActor, cancellationToken);

        return restauraciones.FirstOrDefault();
    }

    public async Task<IReadOnlyList<RegistroRestauracionDto>> ListarRestauracionesAsync(int idActor, CancellationToken cancellationToken = default)
    {
        return await ConsultarRestauracionesAsync(idActor, cancellationToken);
    }

    /// <summary>
    /// Autoriza al actor (Gerente o Administrador) y devuelve las restauraciones de la más reciente a la más antigua.
    /// </summary>
    private async Task<List<RegistroRestauracionDto>> ConsultarRestauracionesAsync(int idActor, CancellationToken cancellationToken)
    {
        var actor = idActor > 0
            ? await _usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        if (actor is null || (!actor.EsGerente() && !actor.EsAdministrador()))
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente o un Administrador puede consultar las restauraciones.");
        }

        // Las restauraciones son excepcionales (unas pocas en toda la vida del sistema): ordenar en memoria es barato.
        var registros = await _restauraciones.ListAllAsync(includeDeleted: false, cancellationToken);

        return registros
            .OrderByDescending(r => r.Fecha)
            .ThenByDescending(r => r.Id)
            .Select(r => new RegistroRestauracionDto
            {
                Referencia = r.Referencia,
                Fecha = r.Fecha,
                Origen = r.Origen,
                NombreUsuario = r.NombreUsuario,
                NombreOperador = r.NombreOperador,
                ReferenciaCopia = r.ReferenciaCopia,
                RutaArchivo = r.RutaArchivo,
                TuvoCopiaPrevia = r.ReferenciaCopiaPrevia is not null,
                BaseAnterior = r.BaseAnterior
            })
            .ToList();
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
        Usuario? operador,
        IProgress<int>? progreso,
        CancellationToken cancellationToken)
    {
        try
        {
            return await EjecutarRestauracionAsync(origen, copia, autorizarEnLaCopia, operador, progreso, cancellationToken);
        }
        catch (Exception ex) when (DebeEnvolverse(ex))
        {
            throw new RestauracionFallidaException(
                $"No se pudo completar la restauración: {DescripcionDeFalla.Para(ex)}. La base vigente no cambió.", ex);
        }
    }

    private async Task<ResultadoRestauracionDto> EjecutarRestauracionAsync(
        OrigenRestauracionEnum origen,
        CopiaARestaurar copia,
        Func<IBaseRestaurada, CancellationToken, Task<Usuario>> autorizarEnLaCopia,
        Usuario? operador,
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
            await ExigirSinTurnoDeCajaAbiertoAsync(cancellationToken);

            // B-0: con el exclusivo tomado ninguna copia corre, así que las EnCurso de la vigente son huérfanas.
            var cerradas = await CerrarCopiasInterrumpidasEnLaVigenteAsync(cancellationToken);
            if (cerradas > 0)
            {
                advertencias.Add(cerradas == 1
                    ? "Se cerró 1 copia de seguridad interrumpida en la base vigente."
                    : $"Se cerraron {cerradas} copias de seguridad interrumpidas en la base vigente.");
            }
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

        // Los permisos de la copia automática diaria viven dentro de la base: una copia tomada antes de registrar la tarea
        // programada no los tiene y la tarea de esta noche fallaría.
        if (vigente.Existe
            && await _restaurador.VigenteTieneUsuarioDeLaTareaProgramadaAsync(cancellationToken)
            && !await temporal.TieneUsuarioDeLaTareaProgramadaAsync(cancellationToken))
        {
            advertencias.Add(AvisoPermisosDeLaCopiaAutomatica);
        }

        var gerente = await autorizarEnLaCopia(temporal, cancellationToken);

        Guid? referenciaCopiaPrevia = null;
        if (vigente.Existe)
        {
            // El restore puede llevar minutos: los controles se repiten justo antes de la copia previa, que es la
            // última foto de los datos reales (TOCTOU).
            await ExigirSinTurnoDeCajaAbiertoAsync(cancellationToken);
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
            temporal.NombreBaseAnterior,
            idOperador: operador?.Id,
            nombreOperador: operador?.NombreUsuario);

        await temporal.AgregarRestauracionAsync(restauracion, cancellationToken);
        await temporal.GuardarCambiosAsync(cancellationToken);

        // Punto de no retorno. Lo que sigue no se cancela: dejar el intercambio a medias sería peor que terminarlo.
        advertencias.AddRange(await temporal.PromoverAVigenteAsync(CancellationToken.None));

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

        return ExigirGerenteConCredencial(usuario, password);
    }

    /// <summary>
    /// D-30: el autorizante se busca en la base vigente por nombre normalizado, entre los usuarios no dados de baja.
    /// </summary>
    private async Task<Usuario?> BuscarAutorizanteAsync(CredencialAutorizanteDto autorizante, CancellationToken cancellationToken)
    {
        var nombre = ReglasTexto.NormalizarNombreUsuario(autorizante.NombreUsuario);
        var encontrados = await _usuarios.FindAsync(u => u.NombreUsuario == nombre, includeDeleted: false, cancellationToken);
        return encontrados.SingleOrDefault();
    }

    private async Task<CopiaARestaurar> BuscarCopiaRestaurableAsync(Guid referenciaCopia, CancellationToken cancellationToken)
    {
        var encontrados = await _registros.FindAsync(r => r.Referencia == referenciaCopia, includeDeleted: false, cancellationToken);
        var registro = encontrados.SingleOrDefault();
        if (registro is null || !registro.EsRestaurable)
        {
            throw new RestauracionFallidaException(
                "La copia elegida no es restaurable: solo se restauran copias verificadas cuyo archivo se conserva.");
        }

        return new CopiaARestaurar(
            registro.Referencia,
            registro.RutaArchivo,
            registro.TamanoBytes!.Value,
            registro.HashSha256!,
            registro.UltimaMigracion);
    }

    /// <summary>
    /// Regla común de credencial de Gerente (D-18, D-30): un usuario inexistente, que no es Gerente, con cambio de
    /// contraseña pendiente (la eligió otra persona) o con contraseña incorrecta recibe siempre la misma respuesta.
    /// </summary>
    private Usuario ExigirGerenteConCredencial(Usuario? usuario, string password, string? mensaje = null)
    {
        if (usuario is null || !usuario.EsGerente() || usuario.DebeCambiarPassword || string.IsNullOrEmpty(password)
            || !_passwordHasher.VerifyPassword(password, usuario.PasswordHash))
        {
            throw mensaje is null ? new CredencialesInvalidasException() : new CredencialesInvalidasException(mensaje);
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
            advertencias.Add($"No se pudo tomar la copia previa de la base vigente: {DescripcionDeFalla.Para(ex)}");
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
            advertencias.Add($"No se pudieron eliminar las bases anteriores viejas ({DescripcionDeFalla.Para(ex)}); se reintentará en la próxima restauración.");
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

    private async Task ExigirSinTurnoDeCajaAbiertoAsync(CancellationToken cancellationToken)
    {
        // R-11: restaurar con la caja abierta perdería los movimientos del turno y dejaría el arqueo inconsistente.
        // Con SQL mínimo en el puerto: la vigente puede tener un esquema viejo que el modelo de EF no puede leer (D-26).
        if (await _restaurador.HayTurnoDeCajaAbiertoEnBaseVigenteAsync(cancellationToken))
        {
            throw new RestauracionFallidaException("Hay un turno de caja abierto. Ciérrelo antes de restaurar una copia.");
        }
    }

    /// <summary>
    /// B-0: una copia real sostiene el bloqueo compartido mientras corre y la restauración ya tiene el exclusivo, así que
    /// toda copia <c>EnCurso</c> de la base vigente es huérfana (su proceso murió). Se la cierra como fallida, antes de la
    /// copia previa, que de lo contrario chocaría con el índice de copia única en curso (R-12). Devuelve cuántas cerró.
    /// </summary>
    private async Task<int> CerrarCopiasInterrumpidasEnLaVigenteAsync(CancellationToken cancellationToken)
    {
        // Una base de una versión anterior al historial no tiene copias registradas que puedan estar en curso.
        if (!await _esquema.ExisteHistorialDeBackupsAsync(cancellationToken))
        {
            return 0;
        }

        // También las Exitoso: el proceso pudo morir durante la verificación, y esa copia no está verificada ni es restaurable.
        var huerfanas = await _registros.FindAsync(
            r => r.Estado == EstadoBackupEnum.EnCurso || r.Estado == EstadoBackupEnum.Exitoso,
            includeDeleted: false,
            cancellationToken);
        if (huerfanas.Count == 0)
        {
            return 0;
        }

        foreach (var registro in huerfanas)
        {
            registro.RegistrarFallo(RegistroBackup.MensajeCopiaHuerfana);
            await _registros.UpdateAsync(registro, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Con el registro ya cerrado se borran los restos parciales (ver BackupService.CerrarCopiasInterrumpidasAsync).
        foreach (var registro in huerfanas)
        {
            try
            {
                _archivos.EliminarCopia(registro.RutaArchivo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Residuo no restaurable: no justifica abortar la restauración.
            }
        }

        return huerfanas.Count;
    }

    private static string NormalizarRutaDeCopia(string rutaArchivo, bool validarLongitud)
    {
        string ruta;
        try
        {
            ruta = Path.GetFullPath(rutaArchivo.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new RestauracionFallidaException($"La ruta '{rutaArchivo}' no es una ruta de archivo válida.", ex);
        }

        if (validarLongitud && ruta.Length > RegistroBackup.LongitudMaximaRuta)
        {
            throw new RestauracionFallidaException(
                $"La ruta de la copia supera los {RegistroBackup.LongitudMaximaRuta} caracteres: muévala a una carpeta con " +
                "una ruta más corta (por ejemplo, la carpeta de respaldos) y vuelva a intentar.");
        }

        return ruta;
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
