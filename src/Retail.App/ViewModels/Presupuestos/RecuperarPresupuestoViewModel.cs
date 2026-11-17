using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Diálogo "Recuperar presupuesto" del Punto de venta (RF-12): pide el número, lo busca y devuelve lo necesario para armar
/// el ticket. Los errores esperables (no existe, ya convertido o anulado) se muestran dentro del diálogo, sin cerrarlo,
/// para corregir el número y volver a intentar.
/// </summary>
public partial class RecuperarPresupuestoViewModel : ObservableObject
{
    private readonly IPresupuestoService _presupuestoService;
    private readonly CargaSerializada _acceso;
    private readonly ILogger _logger;

    /// <param name="acceso">Fila de accesos a datos del POS, para no usar su DbContext en paralelo (H-19).</param>
    public RecuperarPresupuestoViewModel(
        IPresupuestoService presupuestoService,
        CargaSerializada acceso,
        ILogger logger)
    {
        _presupuestoService = presupuestoService ?? throw new ArgumentNullException(nameof(presupuestoService));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Número tal como lo escribe el usuario (con o sin ceros a la izquierda).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecuperarCommand))]
    private string _numero = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecuperarCommand))]
    [NotifyPropertyChangedFor(nameof(NoEstaOcupado))]
    private bool _isBusy;

    public bool NoEstaOcupado => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>Presupuesto recuperado; <c>null</c> si el usuario canceló o todavía no recuperó ninguno.</summary>
    public PresupuestoParaVentaDto? Resultado { get; private set; }

    /// <summary>El diálogo se cierra apenas hay un presupuesto recuperado.</summary>
    public bool Recuperado => Resultado is not null;

    partial void OnNumeroChanged(string value)
    {
        // Corregir el número es empezar de nuevo: el error anterior ya no corresponde.
        MensajeError = null;
    }

    [RelayCommand(CanExecute = nameof(PuedeRecuperar))]
    private async Task RecuperarAsync()
    {
        if (!int.TryParse(Numero.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int idPresupuesto) || idPresupuesto <= 0)
        {
            MensajeError = "Ingrese el número del presupuesto (solo dígitos).";
            return;
        }

        IsBusy = true;
        MensajeError = null;

        try
        {
            var (completado, presupuesto) = await _acceso.EjecutarEnFilaAsync(token => _presupuestoService.RecuperarParaVentaAsync(idPresupuesto, token));
            if (completado)
            {
                Resultado = presupuesto;
                OnPropertyChanged(nameof(Recuperado));
            }
        }
        catch (TransicionPresupuestoInvalidaException ex)
        {
            // Convertido o anulado: ya no se puede cobrar. Es información, no una falla.
            var motivo = ex.EstadoActual == EstadoPresupuestoEnum.Convertido ? "ya se convirtió en una venta" : "está anulado";
            MensajeError = $"El presupuesto N.º {PresentacionPresupuesto.FormatearNumero(idPresupuesto)} {motivo}: no se puede recuperar.";
        }
        catch (DomainException ex)
        {
            // Entre otros, "No se encontró el presupuesto N".
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recuperar el presupuesto {IdPresupuesto} en el Punto de venta", idPresupuesto);
            MensajeError = "No se pudo recuperar el presupuesto. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool PuedeRecuperar()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(Numero);
    }
}
