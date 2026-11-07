using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Ventas;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// ViewModel del modal de cobro del mostrador (RF-09, RF-20). El flujo está pensado para el teclado y para el caso más
/// común, un único medio de pago: el modal abre con el total cargado y el medio elegido con las flechas; Enter pasa al
/// monto y un segundo Enter cobra. Si el monto no alcanza, el pago queda registrado y se prepara el siguiente con otro
/// medio y lo que falta cobrar. Solo el efectivo admite un monto mayor a lo que falta: la diferencia es el vuelto.
/// </summary>
public partial class CobroModalViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    [ObservableProperty]
    private decimal _totalVenta;

    [ObservableProperty]
    private ClienteDto? _cliente;

    [ObservableProperty]
    private MedioPagoEnum _medioPagoSeleccionado = MedioPagoEnum.Efectivo;

    /// <summary>
    /// Lo que el cajero cargó para el pago en curso. En efectivo es lo que entregó el cliente (puede superar lo que
    /// falta); en los demás medios es el importe a cobrar con ese medio.
    /// </summary>
    [ObservableProperty]
    private decimal _monto;

    [ObservableProperty]
    private bool _imprimirTicket = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    // El aviso de error enlazaba el string con BooleanToVisibilityConverter, que solo entiende bool y lo ocultaba
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

    public decimal TotalImputado => PagosImputados.Sum(p => p.Monto);

    public decimal SaldoPendiente => Math.Max(0m, TotalVenta - TotalImputado);

    public bool TieneSaldoPendiente => SaldoPendiente > 0m;

    public bool TienePagos => PagosImputados.Count > 0;

    /// <summary>Lo que el pago en curso cancela de la venta: en efectivo, el excedente sobre lo que falta es vuelto.</summary>
    public decimal MontoAImputar => EsEfectivo ? Math.Min(Monto, SaldoPendiente) : Monto;

    public decimal Vuelto => EsEfectivo ? Math.Max(0m, Monto - SaldoPendiente) : 0m;

    public bool TieneVuelto => Vuelto > 0m;

    /// <summary>Si el pago en curso cubre lo que falta, confirmarlo cobra la venta; si no, agrega un pago parcial.</summary>
    public bool CubreLoQueFalta => TieneSaldoPendiente && Monto >= SaldoPendiente;

    /// <summary>El botón principal dice lo que hará Enter: así el doble significado de la tecla queda a la vista.</summary>
    public string TextoAccionPrincipal => CubreLoQueFalta ? "Cobrar" : "Agregar pago";

    public string EtiquetaMonto => EsEfectivo ? "Recibido" : "Monto";

    public string TotalVentaFormateado => TotalVenta.ToString("C2", CulturaArgentina);

    public string TotalImputadoFormateado => TotalImputado.ToString("C2", CulturaArgentina);

    public string SaldoPendienteFormateado => SaldoPendiente.ToString("C2", CulturaArgentina);

    public string VueltoFormateado => Vuelto.ToString("C2", CulturaArgentina);

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
        _monto = totalVenta;
    }

    // D-16: el monto precargado (lo que falta) es solo una sugerencia. El primer billete lo reemplaza (el cliente entrega
    // $5.000 para un total de $3.500) y los siguientes suman (dos billetes de $2.000). Si el cajero tipea el monto, el
    // conteo ya empezó y los billetes suman a lo tipeado.
    private bool _conteoDeBilletesIniciado;
    private bool _asignandoSugerencia;

    partial void OnMontoChanged(decimal value)
    {
        if (!_asignandoSugerencia)
        {
            _conteoDeBilletesIniciado = true;
        }

        NotificarPagoEnCurso();
    }

    partial void OnMedioPagoSeleccionadoChanged(MedioPagoEnum value)
    {
        OnPropertyChanged(nameof(EsEfectivo));
        OnPropertyChanged(nameof(EsCuentaCorriente));
        OnPropertyChanged(nameof(EtiquetaMonto));
        NotificarPagoEnCurso();
    }

    /// <summary>Un medio se puede elegir salvo la cuenta corriente de un cliente que no la tiene habilitada.</summary>
    public bool EstaHabilitado(MedioPagoEnum medio)
    {
        return medio != MedioPagoEnum.CuentaCorriente || CuentaCorrienteHabilitada;
    }

    /// <summary>Flecha derecha: el siguiente medio habilitado. En el último no da la vuelta. El monto se conserva.</summary>
    [RelayCommand]
    public void SeleccionarMedioSiguiente()
    {
        MoverSeleccionDeMedio(+1);
    }

    /// <summary>Flecha izquierda: el medio habilitado anterior. En el primero no da la vuelta. El monto se conserva.</summary>
    [RelayCommand]
    public void SeleccionarMedioAnterior()
    {
        MoverSeleccionDeMedio(-1);
    }

    private void MoverSeleccionDeMedio(int paso)
    {
        int indice = IndiceDe(MedioPagoSeleccionado);

        for (int i = indice + paso; i >= 0 && i < MediosPagoDisponibles.Count; i += paso)
        {
            if (EstaHabilitado(MediosPagoDisponibles[i]))
            {
                MedioPagoSeleccionado = MediosPagoDisponibles[i];
                return;
            }
        }
    }

    private int IndiceDe(MedioPagoEnum medio)
    {
        for (int i = 0; i < MediosPagoDisponibles.Count; i++)
        {
            if (MediosPagoDisponibles[i] == medio)
            {
                return i;
            }
        }

        return 0;
    }

    [RelayCommand]
    public void AgregarMontoEfectivo(decimal monto)
    {
        Monto = _conteoDeBilletesIniciado ? Monto + monto : monto;
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
    /// Vuelve a lo que falta cobrar y reinicia el conteo: el próximo billete lo reemplaza.
    /// </summary>
    [RelayCommand]
    public void EstablecerPagoExacto()
    {
        SugerirMonto(SaldoPendiente);
    }

    private void SugerirMonto(decimal monto)
    {
        _asignandoSugerencia = true;
        try
        {
            Monto = monto;
        }
        finally
        {
            _asignandoSugerencia = false;
        }

        _conteoDeBilletesIniciado = false;
    }

    /// <summary>
    /// Enter en el monto, F12 o el botón principal. Registra el pago en curso; si con él no falta nada, la operación
    /// queda confirmada (la vista cierra el modal). Si todavía falta, prepara el pago siguiente: otro medio y lo que falta.
    /// </summary>
    [RelayCommand]
    public void ConfirmarPagoActual()
    {
        MensajeError = null;

        if (!TieneSaldoPendiente)
        {
            OperacionConfirmada = TienePagos;
            return;
        }

        if (ValidarPagoEnCurso() is { } error)
        {
            MensajeError = error;
            return;
        }

        PagosImputados.Add(new PagoVentaDto
        {
            MedioPago = MedioPagoSeleccionado,
            Monto = MontoAImputar,
            MontoRecibido = EsEfectivo ? Monto : null,
            Vuelto = EsEfectivo ? Vuelto : null
        });

        if (!TieneSaldoPendiente)
        {
            OperacionConfirmada = true;
            NotificarPagos();
            return;
        }

        MedioPagoSeleccionado = SiguienteMedioSinUsar();
        SugerirMonto(SaldoPendiente);
        NotificarPagos();
    }

    private string? ValidarPagoEnCurso()
    {
        if (Monto <= 0m)
        {
            return "Ingrese un monto mayor a cero.";
        }

        if (!EsEfectivo && Monto > SaldoPendiente)
        {
            return $"El monto no puede superar lo que falta cobrar ({SaldoPendienteFormateado}): solo el efectivo da vuelto.";
        }

        if (EsCuentaCorriente)
        {
            if (!CuentaCorrienteHabilitada)
            {
                return "El cliente no tiene cuenta corriente habilitada.";
            }

            if (MontoAImputar > CreditoDisponible)
            {
                return $"El monto supera el crédito disponible del cliente ({CreditoDisponibleFormateado}).";
            }
        }

        return null;
    }

    // Tras un pago parcial lo más probable es que el resto se pague con otro medio: se propone el primero que no se usó
    // todavía. Si se usaron todos, se mantiene el actual (dos tarjetas distintas son un caso real).
    private MedioPagoEnum SiguienteMedioSinUsar()
    {
        foreach (MedioPagoEnum medio in MediosPagoDisponibles)
        {
            if (EstaHabilitado(medio) && PagosImputados.All(p => p.MedioPago != medio))
            {
                return medio;
            }
        }

        return MedioPagoSeleccionado;
    }

    [RelayCommand]
    public void RemoverPago(PagoVentaDto pago)
    {
        if (PagosImputados.Remove(pago))
        {
            MensajeError = null;
            SugerirMonto(SaldoPendiente);
            NotificarPagos();
        }
    }

    private void NotificarPagos()
    {
        OnPropertyChanged(nameof(TotalImputado));
        OnPropertyChanged(nameof(TotalImputadoFormateado));
        OnPropertyChanged(nameof(SaldoPendiente));
        OnPropertyChanged(nameof(SaldoPendienteFormateado));
        OnPropertyChanged(nameof(TieneSaldoPendiente));
        OnPropertyChanged(nameof(TienePagos));
        NotificarPagoEnCurso();
    }

    private void NotificarPagoEnCurso()
    {
        OnPropertyChanged(nameof(MontoAImputar));
        OnPropertyChanged(nameof(Vuelto));
        OnPropertyChanged(nameof(VueltoFormateado));
        OnPropertyChanged(nameof(TieneVuelto));
        OnPropertyChanged(nameof(CubreLoQueFalta));
        OnPropertyChanged(nameof(TextoAccionPrincipal));
    }
}
