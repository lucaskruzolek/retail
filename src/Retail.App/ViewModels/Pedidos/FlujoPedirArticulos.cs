using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// La conversación con el usuario para pedir (A-17), compartida por las dos pantallas de origen: Artículos y el
/// importador. Pide la vista previa, abre el modal de pedido, guarda lo confirmado y traduce el resultado o el error a
/// un mensaje. No decide qué se puede pedir: manda todos los ítems y la vista previa los clasifica (H2). Las reglas
/// del lote viven en <see cref="IPedidoService"/>; esta clase solo coordina la presentación.
/// </summary>
public sealed class FlujoPedirArticulos
{
    private readonly IPedidoService _pedidoService;
    private readonly IPedidoDialogService _dialogService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<FlujoPedirArticulos> _logger;

    public FlujoPedirArticulos(
        IPedidoService pedidoService,
        IPedidoDialogService dialogService,
        ICurrentUserSession session,
        ILogger<FlujoPedirArticulos> logger)
    {
        _pedidoService = pedidoService ?? throw new ArgumentNullException(nameof(pedidoService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Pedidos los operan el Gerente y el Encargado (A-14, G1). Hoy es solo una restricción de la interfaz: la
    /// verificación en el servicio es deuda registrada para el Track 6.1.
    /// </summary>
    public bool PuedePedir => _session.EsGerente || _session.EsEncargado;

    /// <summary>
    /// Recorre vista previa → modal → guardado. Los accesos a datos esperan su turno en la
    /// <see cref="CargaSerializada"/> de la pantalla de origen, que comparte el DbContext (Ley 4, H-19).
    /// </summary>
    /// <param name="idsCatalogoProveedor">Los ítems a pedir; si está vacío, solo se informan los avisos previos.</param>
    /// <param name="avisosPrevios">Lo que la pantalla ya excluyó, con su motivo.</param>
    /// <param name="ofrecerIncorporar">Si el modal ofrece incorporar primero los ítems sin incorporar.</param>
    /// <param name="carga">La fila de accesos a datos de la pantalla de origen.</param>
    /// <param name="idProveedorPreferido">
    /// El distribuidor por el que está filtrada la pantalla de origen: cada renglón arranca en su fuente (Track 4.3, K6).
    /// </param>
    public async Task<ResultadoPedirArticulos> PedirAsync(
        IReadOnlyCollection<int> idsCatalogoProveedor,
        IReadOnlyList<string> avisosPrevios,
        bool ofrecerIncorporar,
        CargaSerializada carga,
        int? idProveedorPreferido = null)
    {
        ArgumentNullException.ThrowIfNull(idsCatalogoProveedor);
        ArgumentNullException.ThrowIfNull(avisosPrevios);
        ArgumentNullException.ThrowIfNull(carga);

        if (_session.IdUsuario is not int idUsuario)
        {
            return ResultadoPedirArticulos.ConError("No hay un usuario en sesión. Vuelva a iniciar sesión para pedir.");
        }

        if (idsCatalogoProveedor.Count == 0)
        {
            _dialogService.MostrarAlerta(
                "No hay nada para pedir",
                $"Ninguno de los artículos seleccionados se puede pedir:{Environment.NewLine}{string.Join(Environment.NewLine, avisosPrevios)}");
            return ResultadoPedirArticulos.Cancelado;
        }

        VistaPreviaPedidoDto? vistaPrevia = null;
        Exception? error = null;
        await carga.EjecutarOperacionAsync(
            token => Task.Run(() => _pedidoService.ObtenerVistaPreviaAsync(idsCatalogoProveedor, idProveedorPreferido, token), token),
            resultado => vistaPrevia = resultado,
            ex => error = ex);

        if (vistaPrevia is null)
        {
            return Fallo(error);
        }

        var respuesta = _dialogService.MostrarPedirArticulos(vistaPrevia, avisosPrevios, ofrecerIncorporar);
        switch (respuesta.Accion)
        {
            case AccionPedirArticulosEnum.IncorporarPrimero:
                var idsSinIncorporar = vistaPrevia.Omitidos
                    .Where(o => o.Motivo == MotivoOmisionPedidoEnum.SinIncorporar)
                    .Select(o => o.IdCatalogoProveedor)
                    .ToList();
                return ResultadoPedirArticulos.IncorporarPrimero(idsSinIncorporar);

            case AccionPedirArticulosEnum.Confirmar:
                return await GuardarAsync(idUsuario, respuesta.Renglones, carga);

            default:
                return ResultadoPedirArticulos.Cancelado;
        }
    }

    private async Task<ResultadoPedirArticulos> GuardarAsync(
        int idUsuario,
        IReadOnlyList<RenglonAAgregarDto> renglones,
        CargaSerializada carga)
    {
        var dto = new AgregarVariosAlPedidoDto { IdUsuario = idUsuario, Renglones = renglones };

        ResultadoAgregarAlPedidoDto? resultado = null;
        Exception? error = null;
        await carga.EjecutarOperacionAsync(
            token => Task.Run(() => _pedidoService.AgregarVariosAlPedidoAsync(dto, token), token),
            r => resultado = r,
            ex => error = ex);

        if (resultado is null)
        {
            return Fallo(error);
        }

        // H2: si entre la vista previa y el guardado todos dejaron de poderse pedir, no se guardó nada.
        return resultado.Pedidos.Count == 0
            ? ResultadoPedirArticulos.ConError("No se agregó nada: los ítems dejaron de poderse pedir mientras revisaba el pedido.")
            : ResultadoPedirArticulos.Guardado(DescribirResultado(resultado));
    }

    /// <summary>
    /// Traduce el error a un mensaje en línea. Sin error y sin resultado, la operación se canceló porque se cerró la
    /// pantalla: no hay nada que informar.
    /// </summary>
    private ResultadoPedirArticulos Fallo(Exception? error)
    {
        switch (error)
        {
            case null:
                return ResultadoPedirArticulos.Cancelado;

            case ValidationException validacion:
                return ResultadoPedirArticulos.ConError(string.Join(" ", validacion.Errors.Select(e => e.ErrorMessage)));

            case DomainException dominio:
                return ResultadoPedirArticulos.ConError(dominio.Message);

            default:
                _logger.LogError(error, "Error al pedir artículos");
                return ResultadoPedirArticulos.ConError("No se pudo agregar al pedido. Intente nuevamente.");
        }
    }

    private static string DescribirResultado(ResultadoAgregarAlPedidoDto resultado)
    {
        var pedidos = string.Join(", ", resultado.Pedidos.Select(p => $"#{p.IdPedido} ({p.RazonSocialProveedor})"));
        var mensaje = resultado.Pedidos.Count == 1
            ? $"Se agregó al borrador {pedidos}."
            : $"Se agregó a los borradores {pedidos}.";

        return resultado.Omitidos.Count == 0
            ? mensaje
            : $"{mensaje} {resultado.Omitidos.Count} ítem(s) se omitieron porque ya no se pueden pedir.";
    }
}
