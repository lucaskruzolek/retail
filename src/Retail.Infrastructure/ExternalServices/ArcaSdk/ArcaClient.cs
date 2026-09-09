using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Fiscal;
using Retail.Application.Interfaces.Infrastructure;

namespace Retail.Infrastructure.ExternalServices.ArcaSdk;

/// <summary>
/// Cliente HTTP tipado que se comunica con el microservicio fiscal local arcasdk (localhost:8080).
/// Incorpora manejo de excepciones para garantizar la contingencia fiscal (RF-17).
/// </summary>
public partial class ArcaClient : IArcaClient
{
    private readonly HttpClient _httpClient;
    private readonly ArcaOptions _options;
    private readonly ILogger<ArcaClient> _logger;

    public ArcaClient(
        HttpClient httpClient,
        IOptions<ArcaOptions> options,
        ILogger<ArcaClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RespuestaCaeDto> SolicitarCaeAsync(SolicitudCaeDto solicitud, CancellationToken cancellationToken = default)
    {
        LogDespachandoSolicitud(_logger, solicitud.IdVenta);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/cae", solicitud, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var resultado = await response.Content.ReadFromJsonAsync<RespuestaCaeDto>(cancellationToken: cancellationToken);
                if (resultado is not null)
                {
                    return resultado;
                }
            }

            string detalleError = await response.Content.ReadAsStringAsync(cancellationToken);
            LogRespuestaNoExitosa(_logger, response.StatusCode, detalleError);

            return new RespuestaCaeDto
            {
                Exitoso = false,
                ResultadoArca = "R",
                MotivoError = $"Respuesta fallida de arcasdk ({response.StatusCode}): {detalleError}"
            };
        }
        catch (HttpRequestException ex)
        {
            LogErrorHttp(_logger, ex);
            return new RespuestaCaeDto
            {
                Exitoso = false,
                ResultadoArca = "R",
                MotivoError = $"Error de transporte HTTP hacia arcasdk: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            LogTimeout(_logger, ex);
            return new RespuestaCaeDto
            {
                Exitoso = false,
                ResultadoArca = "R",
                MotivoError = "Timeout de conexión: arcasdk no respondió dentro del límite de tiempo configurado"
            };
        }
        catch (Exception ex)
        {
            LogErrorInesperado(_logger, ex);
            return new RespuestaCaeDto
            {
                Exitoso = false,
                ResultadoArca = "R",
                MotivoError = $"Error inesperado al solicitar CAE: {ex.Message}"
            };
        }
    }

    public async Task<bool> VerificarSaludServicioAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogServicioNoDisponible(_logger, ex);
            return false;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "ArcaClient: Despachando solicitud fiscal HTTP hacia arcasdk para Venta #{IdVenta}")]
    private static partial void LogDespachandoSolicitud(ILogger logger, int idVenta);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "ArcaClient: arcasdk respondió con código {StatusCode}. Detalle: {Detalle}")]
    private static partial void LogRespuestaNoExitosa(ILogger logger, HttpStatusCode statusCode, string detalle);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "ArcaClient: Error de transporte HTTP al intentar conectar con arcasdk")]
    private static partial void LogErrorHttp(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "ArcaClient: Timeout al esperar respuesta del microservicio arcasdk")]
    private static partial void LogTimeout(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "ArcaClient: Error no contemplado al solicitar CAE")]
    private static partial void LogErrorInesperado(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "ArcaClient: El microservicio arcasdk no se encuentra disponible")]
    private static partial void LogServicioNoDisponible(ILogger logger, Exception ex);
}
