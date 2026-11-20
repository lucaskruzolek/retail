using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Retail.Application.DTOs.Sesiones;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Services;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Sesiones activas (MT-3) y aviso de mantenimiento (MT-12) sobre SQL Server, con SQL explícito y parametrizado.
/// </summary>
/// <remarks>
/// <para>
/// <c>SESIONES_ACTIVAS</c> y <c>MANTENIMIENTO_PROGRAMADO</c> son estado técnico efímero: no están en el modelo de EF
/// Core ni heredan de <c>BaseEntity</c>, y sus filas se borran físicamente (excepción a la Ley 3, ver
/// <c>docs/SISTEMA_DE_PERSISTENCIA.md</c>). Por eso se accede a ellas igual que <see cref="RegistroAccesosService"/>:
/// con ADO.NET sobre la conexión del <see cref="RetailDbContext"/>.
/// </para>
/// <para>
/// Todos los tiempos salen de <c>SYSUTCDATETIME()</c>: una PC con el reloj corrido no puede quitarle la sesión a otra (H10).
/// </para>
/// </remarks>
public class SesionesActivasSqlServer : ISesionesActivas
{
    // Violación de la clave primaria (usuario ya tiene fila) o del índice único de terminal: un login que corrió en paralelo.
    private const int ErrorClaveDuplicada = 2627;
    private const int ErrorIndiceUnicoDuplicado = 2601;

    private const string SqlVencida =
        "CAST(CASE WHEN s.ultimo_latido < DATEADD(second, -@vencimiento, SYSUTCDATETIME()) THEN 1 ELSE 0 END AS bit)";

    private static readonly int VencimientoSegundos = (int)ParametrosSesion.Vencimiento.TotalSeconds;

    private readonly RetailDbContext _context;

    public SesionesActivasSqlServer(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ResultadoInicioSesionDto> IniciarAsync(
        int idUsuario,
        int idTerminal,
        CancellationToken cancellationToken = default)
    {
        // SERIALIZABLE + UPDLOCK/HOLDLOCK en la lectura: dos logins del mismo usuario se encolan en lugar de ganar ambos
        // (o de interbloquearse tomando cada uno un bloqueo compartido y queriendo insertar).
        await using var transaccion = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var tx = transaccion.GetDbTransaction();

        try
        {
            // a) El mutex de instancia única garantiza que nadie más vive en esta PC: toda fila de la terminal que sea
            //    de otro usuario quedó huérfana (la PC se colgó o se reinició sin cerrar sesión).
            await EjecutarAsync(
                tx,
                "DELETE FROM dbo.SESIONES_ACTIVAS WHERE id_terminal = @id_terminal AND id_usuario <> @id_usuario;",
                cancellationToken,
                Entero("@id_terminal", idTerminal),
                Entero("@id_usuario", idUsuario));

            var existente = await LeerSesionDelUsuarioAsync(tx, idUsuario, cancellationToken);

            if (existente is null)
            {
                // c) Sin fila: se inserta. Una violación de PK o de índice único es un login en carrera.
                await InsertarAsync(tx, idUsuario, idTerminal, cancellationToken);
                await transaccion.CommitAsync(cancellationToken);

                return Iniciada(tomoVencida: false);
            }

            if (existente.IdTerminal == idTerminal)
            {
                // Mismo usuario en la misma PC (por ejemplo, volvió a entrar tras un cierre brusco): se refresca.
                await EjecutarAsync(
                    tx,
                    "UPDATE dbo.SESIONES_ACTIVAS SET inicio = SYSUTCDATETIME(), ultimo_latido = SYSUTCDATETIME() " +
                    "WHERE id_usuario = @id_usuario AND id_terminal = @id_terminal;",
                    cancellationToken,
                    Entero("@id_usuario", idUsuario),
                    Entero("@id_terminal", idTerminal));
                await transaccion.CommitAsync(cancellationToken);

                return Iniciada(tomoVencida: false);
            }

            if (!existente.Vencida)
            {
                await transaccion.RollbackAsync(cancellationToken);

                return Rechazada(existente);
            }

            // Sesión vencida en otra terminal: se toma con un UPDATE condicional. Si no afecta filas, otra terminal la
            // renovó en el medio y la sesión sigue viva.
            var filas = await EjecutarAsync(
                tx,
                "UPDATE dbo.SESIONES_ACTIVAS SET id_terminal = @id_terminal, inicio = SYSUTCDATETIME(), ultimo_latido = SYSUTCDATETIME() " +
                "WHERE id_usuario = @id_usuario AND ultimo_latido < DATEADD(second, -@vencimiento, SYSUTCDATETIME());",
                cancellationToken,
                Entero("@id_terminal", idTerminal),
                Entero("@id_usuario", idUsuario),
                Entero("@vencimiento", VencimientoSegundos));

            if (filas == 0)
            {
                await transaccion.RollbackAsync(cancellationToken);

                return Rechazada(existente);
            }

            await transaccion.CommitAsync(cancellationToken);

            return Iniciada(tomoVencida: true);
        }
        catch (SqlException ex) when (ex.Number is ErrorClaveDuplicada or ErrorIndiceUnicoDuplicado)
        {
            await transaccion.RollbackAsync(cancellationToken);

            // Otro login ganó la carrera: se informa dónde quedó la sesión (si todavía se la puede leer).
            var ganadora = await ConConexionAsync(
                () => LeerSesionDelUsuarioAsync(null, idUsuario, cancellationToken),
                cancellationToken);

            return ganadora is null
                ? new ResultadoInicioSesionDto { Iniciada = false }
                : Rechazada(ganadora);
        }
    }

    public Task<EstadoLatidoDto> RenovarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default)
    {
        const string sql =
            "UPDATE dbo.SESIONES_ACTIVAS SET ultimo_latido = SYSUTCDATETIME() " +
            "WHERE id_usuario = @id_usuario AND id_terminal = @id_terminal; " +
            "DECLARE @vigente bit = CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END; " +
            "SELECT @vigente, m.programado_para, m.motivo, SYSUTCDATETIME() " +
            "FROM (SELECT 1 AS uno) AS fila LEFT JOIN dbo.MANTENIMIENTO_PROGRAMADO AS m ON m.id = 1;";

        return ConConexionAsync(
            async () =>
            {
                await using var comando = CrearComando(
                    sql,
                    null,
                    Entero("@id_usuario", idUsuario),
                    Entero("@id_terminal", idTerminal));
                await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
                await lector.ReadAsync(cancellationToken);

                return new EstadoLatidoDto
                {
                    SesionVigente = lector.GetBoolean(0),
                    MantenimientoProgramadoParaUtc = LeerFechaNula(lector, 1),
                    MotivoMantenimiento = lector.IsDBNull(2) ? null : lector.GetString(2),
                    AhoraServidorUtc = EnUtc(lector.GetDateTime(3))
                };
            },
            cancellationToken);
    }

    public Task CerrarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default)
    {
        // Solo la fila exacta: si otra terminal ya tomó la sesión vencida, no se le cierra la suya.
        return ConConexionAsync(
            () => EjecutarAsync(
                null,
                "DELETE FROM dbo.SESIONES_ACTIVAS WHERE id_usuario = @id_usuario AND id_terminal = @id_terminal;",
                cancellationToken,
                Entero("@id_usuario", idUsuario),
                Entero("@id_terminal", idTerminal)),
            cancellationToken);
    }

    public Task<IReadOnlyList<SesionActivaDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var sql =
            "SELECT s.id_usuario, u.nombre, u.apellido, s.id_terminal, t.nombre, s.inicio, s.ultimo_latido, " + SqlVencida + " " +
            "FROM dbo.SESIONES_ACTIVAS AS s " +
            "JOIN dbo.USUARIOS AS u ON u.id_usuario = s.id_usuario " +
            "JOIN dbo.TERMINALES AS t ON t.id_terminal = s.id_terminal " +
            "ORDER BY s.inicio;";

        return ConConexionAsync<IReadOnlyList<SesionActivaDto>>(
            async () =>
            {
                var sesiones = new List<SesionActivaDto>();
                await using var comando = CrearComando(sql, null, Entero("@vencimiento", VencimientoSegundos));
                await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

                while (await lector.ReadAsync(cancellationToken))
                {
                    sesiones.Add(new SesionActivaDto
                    {
                        IdUsuario = lector.GetInt32(0),
                        NombreUsuario = $"{lector.GetString(1)} {lector.GetString(2)}",
                        IdTerminal = lector.GetInt32(3),
                        NombreTerminal = lector.GetString(4),
                        InicioUtc = EnUtc(lector.GetDateTime(5)),
                        UltimoLatidoUtc = EnUtc(lector.GetDateTime(6)),
                        Vencida = lector.GetBoolean(7)
                    });
                }

                return sesiones;
            },
            cancellationToken);
    }

    public Task<bool> ForzarCierreAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        return ConConexionAsync(
            async () => await EjecutarAsync(
                null,
                "DELETE FROM dbo.SESIONES_ACTIVAS WHERE id_usuario = @id_usuario;",
                cancellationToken,
                Entero("@id_usuario", idUsuario)) > 0,
            cancellationToken);
    }

    public Task ProgramarMantenimientoAsync(
        int minutosDesdeAhora,
        string? motivo,
        int idUsuario,
        CancellationToken cancellationToken = default)
    {
        return ConConexionAsync(
            () => EjecutarAsync(
                null,
                "UPDATE dbo.MANTENIMIENTO_PROGRAMADO SET programado_para = DATEADD(minute, @minutos, SYSUTCDATETIME()), " +
                "motivo = @motivo, id_usuario_programo = @id_usuario WHERE id = 1;",
                cancellationToken,
                Entero("@minutos", minutosDesdeAhora),
                new SqlParameter("@motivo", SqlDbType.NVarChar, 200) { Value = (object?)motivo ?? DBNull.Value },
                Entero("@id_usuario", idUsuario)),
            cancellationToken);
    }

    public Task CancelarMantenimientoAsync(CancellationToken cancellationToken = default)
    {
        return ConConexionAsync(
            () => EjecutarAsync(
                null,
                "UPDATE dbo.MANTENIMIENTO_PROGRAMADO SET programado_para = NULL, motivo = NULL, id_usuario_programo = NULL WHERE id = 1;",
                cancellationToken),
            cancellationToken);
    }

    public Task<EstadoMantenimientoDto> ObtenerMantenimientoAsync(CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT m.programado_para, m.motivo, SYSUTCDATETIME() " +
            "FROM (SELECT 1 AS uno) AS fila LEFT JOIN dbo.MANTENIMIENTO_PROGRAMADO AS m ON m.id = 1;";

        return ConConexionAsync(
            async () =>
            {
                await using var comando = CrearComando(sql, null);
                await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
                await lector.ReadAsync(cancellationToken);

                return new EstadoMantenimientoDto
                {
                    ProgramadoParaUtc = LeerFechaNula(lector, 0),
                    Motivo = lector.IsDBNull(1) ? null : lector.GetString(1),
                    AhoraServidorUtc = EnUtc(lector.GetDateTime(2))
                };
            },
            cancellationToken);
    }

    public Task<int> ContarSesionesVigentesExceptoAsync(int idTerminal, CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT COUNT(*) FROM dbo.SESIONES_ACTIVAS AS s " +
            "WHERE s.id_terminal <> @id_terminal AND s.ultimo_latido >= DATEADD(second, -@vencimiento, SYSUTCDATETIME());";

        return ConConexionAsync(
            async () =>
            {
                await using var comando = CrearComando(
                    sql,
                    null,
                    Entero("@id_terminal", idTerminal),
                    Entero("@vencimiento", VencimientoSegundos));

                return (int)(await comando.ExecuteScalarAsync(cancellationToken))!;
            },
            cancellationToken);
    }

    private static ResultadoInicioSesionDto Iniciada(bool tomoVencida)
    {
        return new ResultadoInicioSesionDto { Iniciada = true, TomoSesionVencida = tomoVencida };
    }

    private static ResultadoInicioSesionDto Rechazada(SesionExistente otra)
    {
        return new ResultadoInicioSesionDto
        {
            Iniciada = false,
            TerminalOcupada = otra.NombreTerminal,
            InicioOtraSesionUtc = otra.InicioUtc
        };
    }

    private async Task<SesionExistente?> LeerSesionDelUsuarioAsync(
        DbTransaction? tx,
        int idUsuario,
        CancellationToken cancellationToken)
    {
        var sql =
            "SELECT s.id_terminal, t.nombre, s.inicio, " + SqlVencida + " " +
            "FROM dbo.SESIONES_ACTIVAS AS s WITH (UPDLOCK, HOLDLOCK) " +
            "JOIN dbo.TERMINALES AS t ON t.id_terminal = s.id_terminal " +
            "WHERE s.id_usuario = @id_usuario;";

        await using var comando = CrearComando(
            sql,
            tx,
            Entero("@id_usuario", idUsuario),
            Entero("@vencimiento", VencimientoSegundos));
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await lector.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SesionExistente(
            lector.GetInt32(0),
            lector.GetString(1),
            EnUtc(lector.GetDateTime(2)),
            lector.GetBoolean(3));
    }

    private Task<int> InsertarAsync(DbTransaction tx, int idUsuario, int idTerminal, CancellationToken cancellationToken)
    {
        return EjecutarAsync(
            tx,
            "INSERT INTO dbo.SESIONES_ACTIVAS (id_usuario, id_terminal, inicio, ultimo_latido) " +
            "VALUES (@id_usuario, @id_terminal, SYSUTCDATETIME(), SYSUTCDATETIME());",
            cancellationToken,
            Entero("@id_usuario", idUsuario),
            Entero("@id_terminal", idTerminal));
    }

    private async Task<int> EjecutarAsync(
        DbTransaction? tx,
        string sql,
        CancellationToken cancellationToken,
        params SqlParameter[] parametros)
    {
        await using var comando = CrearComando(sql, tx, parametros);

        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CrearComando(string sql, DbTransaction? tx, params SqlParameter[] parametros)
    {
        var comando = _context.Database.GetDbConnection().CreateCommand();
        comando.CommandText = sql;
        comando.Transaction = tx;
        comando.Parameters.AddRange(parametros);

        return comando;
    }

    /// <summary>
    /// Abre la conexión del contexto mientras dura la operación (EF la cuenta por referencias, así que no molesta a
    /// otros usos) y la devuelve al pool al terminar.
    /// </summary>
    private async Task<T> ConConexionAsync<T>(Func<Task<T>> operacion, CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            return await operacion();
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private async Task ConConexionAsync(Func<Task> operacion, CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await operacion();
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private static SqlParameter Entero(string nombre, int valor)
    {
        return new SqlParameter(nombre, SqlDbType.Int) { Value = valor };
    }

    private static DateTime EnUtc(DateTime valor)
    {
        // SQL Server devuelve datetime2 sin zona (Unspecified); estas columnas son siempre UTC.
        return DateTime.SpecifyKind(valor, DateTimeKind.Utc);
    }

    private static DateTime? LeerFechaNula(DbDataReader lector, int indice)
    {
        return lector.IsDBNull(indice) ? null : EnUtc(lector.GetDateTime(indice));
    }

    private sealed record SesionExistente(int IdTerminal, string NombreTerminal, DateTime InicioUtc, bool Vencida);
}
