using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Clientes;

/// <summary>
/// ViewModel para el diálogo modal de cobranzas multimedio de cuentas corrientes (RF-20).
/// Gestiona el cálculo reactivo de vuelto para efectivo y el despacho transaccional con la caja.
/// </summary>
public partial class CobranzaModalViewModel : ObservableObject
{
    private readonly IClienteService _clienteService;
    private readonly ICajaService _cajaService;

    [ObservableProperty]
    private ClienteDto _cliente;

    [ObservableProperty]
    private decimal _saldoActual;

    [ObservableProperty]
    private decimal _montoCobro;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsEfectivo))]
    private MedioPagoEnum _medioPago = MedioPagoEnum.Efectivo;

    [ObservableProperty]
    private decimal _efectivoRecibido;

    [ObservableProperty]
    private decimal _vuelto;

    [ObservableProperty]
    private string? _referencia;

    [ObservableProperty]
    private bool _imprimirRecibo = true;

    [ObservableProperty]
    private bool _tieneError;

    [ObservableProperty]
    private string? _mensajeError;

    [ObservableProperty]
    private bool _estaProcesando;

    public CobranzaResultadoDto? Resultado { get; private set; }

    public bool EsEfectivo => MedioPago == MedioPagoEnum.Efectivo;

    public IReadOnlyList<MedioPagoEnum> MediosPagoDisponibles { get; } =
    [
        MedioPagoEnum.Efectivo,
        MedioPagoEnum.TarjetaDebito,
        MedioPagoEnum.TarjetaCredito,
        MedioPagoEnum.TransferenciaQr
    ];

    public event EventHandler<bool>? RequestClose;

    public CobranzaModalViewModel(
        IClienteService clienteService,
        ICajaService cajaService,
        ClienteDto cliente)
    {
        _clienteService = clienteService ?? throw new ArgumentNullException(nameof(clienteService));
        _cajaService = cajaService ?? throw new ArgumentNullException(nameof(cajaService));
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));

        _saldoActual = cliente.SaldoCuentaCorriente;
        _montoCobro = cliente.SaldoCuentaCorriente;
        _efectivoRecibido = cliente.SaldoCuentaCorriente;
        RecalcularVuelto();
    }

    partial void OnMontoCobroChanged(decimal value)
    {
        LimpiarError();
        RecalcularVuelto();
    }

    partial void OnEfectivoRecibidoChanged(decimal value)
    {
        LimpiarError();
        RecalcularVuelto();
    }

    partial void OnMedioPagoChanged(MedioPagoEnum value)
    {
        LimpiarError();
        RecalcularVuelto();
    }

    private void RecalcularVuelto()
    {
        if (EsEfectivo && EfectivoRecibido > MontoCobro)
        {
            Vuelto = EfectivoRecibido - MontoCobro;
        }
        else
        {
            Vuelto = 0m;
        }
    }

    [RelayCommand]
    private void PagarTotalidad()
    {
        MontoCobro = SaldoActual;
        if (EsEfectivo && EfectivoRecibido < SaldoActual)
        {
            EfectivoRecibido = SaldoActual;
        }
        RecalcularVuelto();
    }

    [RelayCommand]
    private async Task CobrarAsync()
    {
        LimpiarError();

        if (MontoCobro <= 0m)
        {
            MostrarError("El monto a cobrar debe ser mayor a $ 0,00.");
            return;
        }

        if (MontoCobro > SaldoActual)
        {
            MostrarError($"El monto ingresado (${MontoCobro:N2}) supera la deuda actual del cliente (${SaldoActual:N2}).");
            return;
        }

        if (EsEfectivo && EfectivoRecibido > 0m && EfectivoRecibido < MontoCobro)
        {
            MostrarError("El efectivo recibido es inferior al monto a cobrar.");
            return;
        }

        EstaProcesando = true;

        try
        {
            var turnoActivo = await _cajaService.ObtenerTurnoActivoAsync();
            if (turnoActivo == null || turnoActivo.Estado != EstadoTurnoEnum.Abierto)
            {
                MostrarError("No es posible registrar la cobranza porque no hay un turno de caja abierto.");
                return;
            }

            var dto = new RegistrarCobranzaDto
            {
                IdCliente = Cliente.IdCliente,
                IdTurno = turnoActivo.IdTurno,
                IdUsuario = turnoActivo.IdUsuario,
                Monto = MontoCobro,
                MedioPago = MedioPago,
                Referencia = Referencia
            };

            Resultado = await _clienteService.RegistrarCobranzaAsync(dto);
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            MostrarError(ex.Message);
        }
        finally
        {
            EstaProcesando = false;
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        RequestClose?.Invoke(this, false);
    }

    private void MostrarError(string mensaje)
    {
        TieneError = true;
        MensajeError = mensaje;
    }

    private void LimpiarError()
    {
        TieneError = false;
        MensajeError = null;
    }
}
