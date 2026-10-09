using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Ventas;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// ViewModel para el modal de checkout y cobro multimedio en el mostrador (RF-09, RF-20).
/// Gestiona pagos divididos, cálculo reactivo de vuelto, billetes rápidos y validación de cuentas corrientes.
/// </summary>
public partial class CobroModalViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    [ObservableProperty]
    private decimal _totalVenta;

    [ObservableProperty]
    private ClienteDto? _cliente;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsEfectivo))]
    [NotifyPropertyChangedFor(nameof(EsCuentaCorriente))]
    [NotifyPropertyChangedFor(nameof(ExcedeLimiteCredito))]
    private MedioPagoEnum _medioPagoSeleccionado = MedioPagoEnum.Efectivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Vuelto))]
    [NotifyPropertyChangedFor(nameof(VueltoFormateado))]
    [NotifyPropertyChangedFor(nameof(ExcedeLimiteCredito))]
    private decimal _montoImputar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Vuelto))]
    [NotifyPropertyChangedFor(nameof(VueltoFormateado))]
    private decimal _efectivoRecibido;

    [ObservableProperty]
    private bool _imprimirTicket = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    // El banner de error enlazaba el string con BooleanToVisibilityConverter, que solo entiende bool y lo ocultaba
    // siempre: mismo patrón que ArticuloFormViewModel.
    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public ObservableCollection<PagoVentaDto> PagosImputados { get; } = new();

    public bool OperacionConfirmada { get; private set; }

    public bool EsEfectivo => MedioPagoSeleccionado == MedioPagoEnum.Efectivo;

    public bool EsCuentaCorriente => MedioPagoSeleccionado == MedioPagoEnum.CuentaCorriente;

    public bool CuentaCorrienteHabilitada => Cliente is { TieneCuentaCorriente: true };

    public decimal CreditoDisponible => Cliente == null
        ? 0m
        : Math.Max(0m, Cliente.LimiteCredito - Cliente.SaldoCuentaCorriente);

    public bool ExcedeLimiteCredito => EsCuentaCorriente && MontoImputar > CreditoDisponible;

    public decimal TotalImputado => PagosImputados.Sum(p => p.Monto);

    public decimal SaldoPendiente => Math.Max(0m, TotalVenta - TotalImputado);

    public bool TieneSaldoPendiente => SaldoPendiente > 0m;

    public bool PuedeConfirmar => SaldoPendiente == 0m && PagosImputados.Count > 0;

    public decimal Vuelto => EsEfectivo ? Math.Max(0m, EfectivoRecibido - MontoImputar) : 0m;

    public string TotalVentaFormateado => TotalVenta.ToString("C2", CulturaArgentina);

    public string TotalImputadoFormateado => TotalImputado.ToString("C2", CulturaArgentina);

    public string SaldoPendienteFormateado => SaldoPendiente.ToString("C2", CulturaArgentina);

    public string VueltoFormateado => Vuelto.ToString("C2", CulturaArgentina);

    public string MontoImputarFormateado => MontoImputar.ToString("C2", CulturaArgentina);

    public string CreditoDisponibleFormateado => CreditoDisponible.ToString("C2", CulturaArgentina);

    public IReadOnlyList<MedioPagoEnum> MediosPagoDisponibles { get; } =
    [
        MedioPagoEnum.Efectivo,
        MedioPagoEnum.TarjetaDebito,
        MedioPagoEnum.TarjetaCredito,
        MedioPagoEnum.TransferenciaQr,
        MedioPagoEnum.CuentaCorriente
    ];

    public CobroModalViewModel(decimal totalVenta, ClienteDto? cliente)
    {
        _totalVenta = totalVenta;
        _cliente = cliente;
        _montoImputar = totalVenta;
        _efectivoRecibido = totalVenta;
    }

    // D-16: el monto exacto precargado es solo una sugerencia. El primer billete lo reemplaza (el cliente entrega
    // $5.000 para un total de $3.500) y los siguientes suman (dos billetes de $2.000). Si el cajero tipea el monto,
    // el conteo ya empezó y los billetes suman a lo tipeado.
    private bool _conteoDeBilletesIniciado;
    private bool _asignandoSugerencia;

    partial void OnEfectivoRecibidoChanged(decimal value)
    {
        if (!_asignandoSugerencia)
        {
            _conteoDeBilletesIniciado = true;
        }
    }

    [RelayCommand]
    public void AgregarMontoEfectivo(decimal monto)
    {
        EfectivoRecibido = _conteoDeBilletesIniciado ? EfectivoRecibido + monto : monto;
    }

    [RelayCommand]
    public void SumarMil() => AgregarMontoEfectivo(1000m);

    [RelayCommand]
    public void SumarDosMil() => AgregarMontoEfectivo(2000m);

    [RelayCommand]
    public void SumarCincoMil() => AgregarMontoEfectivo(5000m);

    [RelayCommand]
    public void SumarDiezMil() => AgregarMontoEfectivo(10000m);

    [RelayCommand]
    public void SumarVeinteMil() => AgregarMontoEfectivo(20000m);

    /// <summary>
    /// Vuelve al monto exacto y reinicia el conteo: el próximo billete lo reemplaza.
    /// </summary>
    [RelayCommand]
    public void EstablecerPagoExacto()
    {
        SugerirEfectivoExacto(MontoImputar);
    }

    private void SugerirEfectivoExacto(decimal monto)
    {
        _asignandoSugerencia = true;
        try
        {
            EfectivoRecibido = monto;
        }
        finally
        {
            _asignandoSugerencia = false;
        }

        _conteoDeBilletesIniciado = false;
    }

    [RelayCommand]
    public void ImputarPago()
    {
        MensajeError = null;

        if (MontoImputar <= 0m)
        {
            MensajeError = "El monto a imputar debe ser mayor a cero.";
            return;
        }

        if (MontoImputar > SaldoPendiente)
        {
            MensajeError = $"El monto a imputar ({MontoImputar:C2}) no puede superar el saldo pendiente ({SaldoPendiente:C2}).";
            return;
        }

        if (EsCuentaCorriente)
        {
            if (!CuentaCorrienteHabilitada)
            {
                MensajeError = "El cliente no posee cuenta corriente habilitada para este medio de pago.";
                return;
            }

            if (ExcedeLimiteCredito)
            {
                MensajeError = $"El monto supera el crédito disponible del cliente ({CreditoDisponible:C2}).";
                return;
            }
        }

        decimal? efectivoRecibidoDto = EsEfectivo ? EfectivoRecibido : null;
        decimal? vueltoDto = EsEfectivo ? Vuelto : null;

        var pago = new PagoVentaDto
        {
            MedioPago = MedioPagoSeleccionado,
            Monto = MontoImputar,
            MontoRecibido = efectivoRecibidoDto,
            Vuelto = vueltoDto
        };

        PagosImputados.Add(pago);

        ActualizarEstadosDeSaldo();
    }

    [RelayCommand]
    public void RemoverPago(PagoVentaDto pago)
    {
        if (PagosImputados.Remove(pago))
        {
            ActualizarEstadosDeSaldo();
        }
    }

    [RelayCommand]
    public void ConfirmarCobro()
    {
        if (!PuedeConfirmar)
        {
            MensajeError = "Aún resta saldo pendiente por cubrir antes de confirmar el cobro.";
            return;
        }

        OperacionConfirmada = true;
    }

    private void ActualizarEstadosDeSaldo()
    {
        OnPropertyChanged(nameof(TotalImputado));
        OnPropertyChanged(nameof(TotalImputadoFormateado));
        OnPropertyChanged(nameof(SaldoPendiente));
        OnPropertyChanged(nameof(SaldoPendienteFormateado));
        OnPropertyChanged(nameof(TieneSaldoPendiente));
        OnPropertyChanged(nameof(PuedeConfirmar));

        MontoImputar = SaldoPendiente;
        SugerirEfectivoExacto(SaldoPendiente);
        OnPropertyChanged(nameof(MontoImputarFormateado));
    }
}
