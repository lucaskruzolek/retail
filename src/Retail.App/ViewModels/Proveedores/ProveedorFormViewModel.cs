using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para el formulario modal de alta y modificación de proveedores mayoristas (RF-05).
/// </summary>
public partial class ProveedorFormViewModel : ObservableObject
{
    private readonly IProveedorService _proveedorService;

    [ObservableProperty]
    private int _idProveedor;

    [ObservableProperty]
    private string _razonSocial = string.Empty;

    [ObservableProperty]
    private string _cuit = string.Empty;

    [ObservableProperty]
    private string? _telefono;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    private bool _esModoEdicion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    [ObservableProperty]
    private bool _isBusy;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public string TituloVentana => EsModoEdicion ? "Modificar Proveedor Mayorista" : "Alta de Proveedor Mayorista";

    public bool DialogResult { get; private set; }

    public ProveedorFormViewModel(IProveedorService proveedorService)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
    }

    public void ConfigurarAlta()
    {
        EsModoEdicion = false;
        IdProveedor = 0;
        RazonSocial = string.Empty;
        Cuit = string.Empty;
        Telefono = null;
        Email = null;
        MensajeError = null;
    }

    public void ConfigurarEdicion(ProveedorDto proveedor)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        EsModoEdicion = true;
        IdProveedor = proveedor.IdProveedor;
        RazonSocial = proveedor.RazonSocial;
        Cuit = proveedor.Cuit;
        Telefono = proveedor.Telefono;
        Email = proveedor.Email;
        MensajeError = null;
    }

    [RelayCommand]
    private async Task GuardarAsync(Window window)
    {
        IsBusy = true;
        MensajeError = null;

        try
        {
            if (EsModoEdicion)
            {
                var dto = new ProveedorDto
                {
                    IdProveedor = IdProveedor,
                    RazonSocial = RazonSocial,
                    Cuit = Cuit,
                    Telefono = Telefono,
                    Email = Email
                };

                await _proveedorService.ActualizarProveedorAsync(dto);
            }
            else
            {
                var dto = new CrearProveedorDto
                {
                    RazonSocial = RazonSocial,
                    Cuit = Cuit,
                    Telefono = Telefono,
                    Email = Email
                };

                await _proveedorService.CrearProveedorAsync(dto);
            }

            DialogResult = true;
            if (window != null)
            {
                window.DialogResult = true;
                window.Close();
            }
        }
        catch (ValidationException vex)
        {
            MensajeError = string.Join("\n", vex.Errors.Select(e => e.ErrorMessage));
        }
        catch (DomainException dex)
        {
            MensajeError = dex.Message;
        }
        catch (Exception ex)
        {
            MensajeError = $"Error inesperado al guardar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancelar(Window? window)
    {
        DialogResult = false;
        if (window != null)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
