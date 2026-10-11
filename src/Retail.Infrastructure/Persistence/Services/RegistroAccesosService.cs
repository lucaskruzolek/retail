using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Auth;
using Retail.Application.Interfaces.Persistence;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Ejecuta el procedimiento almacenado sp_RegistrarIntentoLogin y gestiona los parámetros de salida.
/// </summary>
public class RegistroAccesosService : IRegistroAccesosService
{
    private readonly RetailDbContext _context;
    private readonly PoliticaBloqueoOptions _options;

    public RegistroAccesosService(
        RetailDbContext context,
        IOptions<PoliticaBloqueoOptions>? options = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options?.Value ?? new PoliticaBloqueoOptions();
    }

    public async Task<ResultadoRegistroIntentoDto> RegistrarIntentoAsync(
        string nombreUsuario,
        int? idUsuario,
        bool credencialesValidas,
        CancellationToken cancellationToken = default)
    {
        var paramNombreUsuario = new SqlParameter("@nombre_usuario", SqlDbType.NVarChar, 50)
        {
            Value = (object?)nombreUsuario ?? DBNull.Value
        };

        var paramIdUsuario = new SqlParameter("@id_usuario", SqlDbType.Int)
        {
            Value = (object?)idUsuario ?? DBNull.Value
        };

        var paramCredencialesValidas = new SqlParameter("@credenciales_validas", SqlDbType.Bit)
        {
            Value = credencialesValidas
        };

        var paramMaxIntentos = new SqlParameter("@max_intentos", SqlDbType.Int)
        {
            Value = _options.MaxIntentosFallidos
        };

        var paramMinutosBloqueo = new SqlParameter("@minutos_bloqueo", SqlDbType.Int)
        {
            Value = _options.MinutosBloqueo
        };

        var paramEquipo = new SqlParameter("@equipo", SqlDbType.NVarChar, 100)
        {
            Value = (object?)Environment.MachineName ?? DBNull.Value
        };

        var paramResultado = new SqlParameter("@resultado", SqlDbType.TinyInt)
        {
            Direction = ParameterDirection.Output
        };

        var paramIntentosRestantes = new SqlParameter("@intentos_restantes", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };

        var paramBloqueadoHasta = new SqlParameter("@bloqueado_hasta", SqlDbType.DateTime2)
        {
            Direction = ParameterDirection.Output
        };

        var sqlParameters = new object[]
        {
            paramNombreUsuario,
            paramIdUsuario,
            paramCredencialesValidas,
            paramMaxIntentos,
            paramMinutosBloqueo,
            paramEquipo,
            paramResultado,
            paramIntentosRestantes,
            paramBloqueadoHasta
        };

        var sql = "EXEC dbo.sp_RegistrarIntentoLogin @nombre_usuario, @id_usuario, @credenciales_validas, @max_intentos, @minutos_bloqueo, @equipo, @resultado OUTPUT, @intentos_restantes OUTPUT, @bloqueado_hasta OUTPUT";

        await _context.Database.ExecuteSqlRawAsync(sql, sqlParameters, cancellationToken);

        var resultadoVal = paramResultado.Value != DBNull.Value ? (byte)paramResultado.Value : (byte)1;
        var intentosRestantesVal = paramIntentosRestantes.Value != DBNull.Value ? (int)paramIntentosRestantes.Value : 0;
        DateTime? bloqueadoHastaVal = (paramBloqueadoHasta.Value != DBNull.Value && paramBloqueadoHasta.Value is DateTime dt)
            ? dt
            : null;

        return new ResultadoRegistroIntentoDto
        {
            Resultado = resultadoVal,
            IntentosRestantes = intentosRestantesVal,
            BloqueadoHasta = bloqueadoHastaVal
        };
    }
}
