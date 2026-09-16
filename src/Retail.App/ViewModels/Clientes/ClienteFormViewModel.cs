using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Clientes;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Clientes;

/// <summary>
/// ViewModel para el diálogo modal de creación y modificación de clientes (RF-20).
/// </summary>
public partial class ClienteFormViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idCliente;

    [ObservableProperty]
    private string _razonSocialONombre = string.Empty;

    [ObservableProperty]
    private TipoDocumentoEnum _tipoDocumento = TipoDocumentoEnum.Dni;

    [ObservableProperty]
    private string _numeroDocumento = string.Empty;

    [ObservableProperty]
    private CondicionIvaEnum _condicionIva = CondicionIvaEnum.ConsumidorFinal;

    [ObservableProperty]
    private string? _domicilioFiscal;

    [ObservableProperty]
    private string? _telefono;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private bool _tieneCuentaCorriente;

    [ObservableProperty]
    private decimal _limiteCredito;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsAlta))]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    private bool _esModoEdicion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public Func<Task>? OnGuardarAsync { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool EsAlta => !EsModoEdicion;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public string TituloVentana => EsModoEdicion ? "Modificar Cliente" : "Nuevo Cliente";

    public IReadOnlyList<TipoDocumentoEnum> TiposDocumentoDisponibles { get; } = Enum.GetValues<TipoDocumentoEnum>();

    public IReadOnlyList<CondicionIvaEnum> CondicionesIvaDisponibles { get; } = Enum.GetValues<CondicionIvaEnum>();

    partial void OnTieneCuentaCorrienteChanged(bool value)
    {
        if (!value)
        {
            LimiteCredito = 0m;
        }
        else if (LimiteCredito <= 0m)
        {
            LimiteCredito = 10000m; // Límite inicial sugerido al habilitar
        }
    }

    public void ConfigurarAlta()
    {
        EsModoEdicion = false;
        IdCliente = 0;
        RazonSocialONombre = string.Empty;
        TipoDocumento = TipoDocumentoEnum.Dni;
        NumeroDocumento = string.Empty;
        CondicionIva = CondicionIvaEnum.ConsumidorFinal;
        DomicilioFiscal = null;
        Telefono = null;
        Email = null;
        TieneCuentaCorriente = false;
        LimiteCredito = 0m;
        MensajeError = null;
    }

    public void ConfigurarEdicion(ClienteDto cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        EsModoEdicion = true;
        IdCliente = cliente.IdCliente;
        RazonSocialONombre = cliente.RazonSocialONombre;
        TipoDocumento = cliente.TipoDocumento;
        NumeroDocumento = cliente.NumeroDocumento;
        CondicionIva = cliente.CondicionIva;
        DomicilioFiscal = cliente.DomicilioFiscal;
        Telefono = cliente.Telefono;
        Email = cliente.Email;
        TieneCuentaCorriente = cliente.TieneCuentaCorriente;
        LimiteCredito = cliente.LimiteCredito;
        MensajeError = null;
    }

    public bool Validar()
    {
        MensajeError = null;

        if (string.IsNullOrWhiteSpace(RazonSocialONombre))
        {
            MensajeError = "La razón social o nombre es obligatorio.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NumeroDocumento))
        {
            MensajeError = "El número de documento o CUIT es obligatorio.";
            return false;
        }

        if (NumeroDocumento.Trim().Length < 7)
        {
            MensajeError = "El número de documento debe contener al menos 7 caracteres.";
            return false;
        }

        if (TieneCuentaCorriente && LimiteCredito < 0m)
        {
            MensajeError = "El límite de crédito no puede ser negativo.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Email) && !Email.Contains('@', StringComparison.Ordinal))
        {
            MensajeError = "El formato del correo electrónico no es válido.";
            return false;
        }

        return true;
    }

    public CrearClienteDto GenerarCrearDto() => new()
    {
        RazonSocialONombre = RazonSocialONombre.Trim(),
        TipoDocumento = TipoDocumento,
        NumeroDocumento = NumeroDocumento.Trim(),
        CondicionIva = CondicionIva,
        DomicilioFiscal = string.IsNullOrWhiteSpace(DomicilioFiscal) ? null : DomicilioFiscal.Trim(),
        Telefono = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono.Trim(),
        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
        TieneCuentaCorriente = TieneCuentaCorriente,
        LimiteCredito = TieneCuentaCorriente ? LimiteCredito : 0m
    };

    public ActualizarClienteDto GenerarActualizarDto() => new()
    {
        IdCliente = IdCliente,
        RazonSocialONombre = RazonSocialONombre.Trim(),
        TipoDocumento = TipoDocumento,
        NumeroDocumento = NumeroDocumento.Trim(),
        CondicionIva = CondicionIva,
        DomicilioFiscal = string.IsNullOrWhiteSpace(DomicilioFiscal) ? null : DomicilioFiscal.Trim(),
        Telefono = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono.Trim(),
        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
        TieneCuentaCorriente = TieneCuentaCorriente,
        LimiteCredito = TieneCuentaCorriente ? LimiteCredito : 0m
    };
}
