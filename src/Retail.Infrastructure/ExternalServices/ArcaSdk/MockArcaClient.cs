using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Retail.Application.DTOs.Fiscal;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Domain.Enums;

namespace Retail.Infrastructure.ExternalServices.ArcaSdk;

/// <summary>
/// Implementación simulada (Mock) de IArcaClient para desarrollo local y ejecución de tests sin requerir
/// certificados fiscales reales de AFIP ni la instancia en ejecución del microservicio arcasdk.
/// Cumple con la Ley 6 de AGENTS.md.
/// </summary>
public partial class MockArcaClient : IArcaClient
{
    private readonly ArcaOptions _options;
    private readonly ILogger<MockArcaClient> _logger;

    /// <summary>
    /// Bandera configurable para simular la caída del servicio y probar la contingencia fiscal (RF-17).
    /// </summary>
    public bool SimularCaidaServicio { get; set; }

    /// <summary>
    /// Bandera configurable para simular excepciones de red/socket a nivel de transporte HTTP.
    /// </summary>
    public bool SimularExcepcionRed { get; set; }

    /// <summary>
    /// Retardo simulado en milisegundos para emular la latencia de red hacia ARCA.
    /// </summary>
    public int LatenciaSimuladaMs { get; set; } = 20;

    public MockArcaClient(
        IOptions<ArcaOptions> options,
        ILogger<MockArcaClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RespuestaCaeDto> SolicitarCaeAsync(SolicitudCaeDto solicitud, CancellationToken cancellationToken = default)
    {
        LogProcesandoSolicitud(_logger, solicitud.IdVenta, solicitud.TipoComprobante, solicitud.Total);

        if (SimularExcepcionRed)
        {
            LogSimulandoExcepcionRed(_logger);
            throw new HttpRequestException("No se pudo conectar con el microservicio fiscal arcasdk (Simulación de caída de red)");
        }

        if (SimularCaidaServicio)
        {
            LogSimulandoCaidaServicio(_logger);
            return new RespuestaCaeDto
            {
                Exitoso = false,
                ResultadoArca = "R",
                MotivoError = "Microservicio fiscal arcasdk no responde en localhost:8080 (Simulación de contingencia RF-17)"
            };
        }

        // Simulación de rechazo fiscal por CUIT de prueba o inconsistencia en importes
        if (solicitud.CuitCliente == "99999999999" || solicitud.Total <= 0)
        {
            LogSolicitudRechazada(_logger, solicitud.CuitCliente, solicitud.Total);

            return new RespuestaCaeDto
            {
                Exitoso = false,
                ResultadoArca = "R",
                MotivoError = "Rechazo simulado AFIP/ARCA: CUIT inválido o inconsistencia en el monto del comprobante"
            };
        }

        if (LatenciaSimuladaMs > 0)
        {
            await Task.Delay(LatenciaSimuladaMs, cancellationToken);
        }

        int puntoVenta = solicitud.PuntoVenta > 0 ? solicitud.PuntoVenta : _options.PuntoVentaDefecto;
        int nroComprobante = solicitud.IdVenta > 0 ? solicitud.IdVenta : 1;

        // Generación de CAE simulado estándar de 14 dígitos: '74' + PV (4 dígitos) + Nro (8 dígitos)
        string caeSimulado = $"74{puntoVenta:D4}{nroComprobante:D8}";
        DateOnly vtoCae = DateOnly.FromDateTime(solicitud.FechaComprobante.AddDays(10));

        LogCaeAsignado(_logger, caeSimulado, vtoCae);

        return new RespuestaCaeDto
        {
            Exitoso = true,
            Cae = caeSimulado,
            FechaVtoCae = vtoCae,
            NumeroComprobante = nroComprobante,
            ResultadoArca = "A",
            MotivoError = null
        };
    }

    public Task<bool> VerificarSaludServicioAsync(CancellationToken cancellationToken = default)
    {
        bool estaSaludable = !SimularCaidaServicio && !SimularExcepcionRed;
        LogVerificacionSalud(_logger, estaSaludable);
        return Task.FromResult(estaSaludable);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "MockArcaClient: Procesando solicitud CAE para Venta #{IdVenta}, Tipo: {TipoComprobante}, Total: {Total}")]
    private static partial void LogProcesandoSolicitud(ILogger logger, int idVenta, TipoComprobanteFiscalEnum tipoComprobante, decimal total);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "MockArcaClient: Simulando excepción de transporte HTTP hacia arcasdk")]
    private static partial void LogSimulandoExcepcionRed(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "MockArcaClient: Simulando respuesta de contingencia por servicio no disponible")]
    private static partial void LogSimulandoCaidaServicio(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "MockArcaClient: Solicitud rechazada por CUIT inválido o total inconsistente (CUIT: {Cuit}, Total: {Total})")]
    private static partial void LogSolicitudRechazada(ILogger logger, string? cuit, decimal total);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "MockArcaClient: CAE asignado con éxito: {Cae}, Vto: {Vto}")]
    private static partial void LogCaeAsignado(ILogger logger, string cae, DateOnly vto);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "MockArcaClient: Verificación de salud simulada -> {Saludable}")]
    private static partial void LogVerificacionSalud(ILogger logger, bool saludable);
}
