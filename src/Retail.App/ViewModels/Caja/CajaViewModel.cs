using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Caja;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Caja;

public partial class CajaViewModel : ObservableObject, IDisposable
{
    // Serializa la lectura del turno sobre el DbContext de esta pantalla y la cancela al salir de ella (H-19).
    private readonly CargaSerializada _cargaTurno = new();

    private readonly ICajaService _cajaService;
    private readonly ICajaDialogService _dialogService;

    [ObservableProperty]
    private TurnoCajaDto? _turnoActivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTurnoAbierto))]
    private bool _tieneTurnoActivo;

    public bool IsTurnoAbierto => TurnoActivo != null && TurnoActivo.Estado == Domain.Enums.EstadoTurnoEnum.Abierto;

    public CajaViewModel(ICajaService cajaService, ICajaDialogService dialogService)
    {
        _cajaService = cajaService;
        _dialogService = dialogService;

        // Cargar estado inicial del turno al arrancar la vista
        _ = CargarTurnoActivoAsync();
    }

    [RelayCommand]
    public Task CargarTurnoActivoAsync()
    {
        return _cargaTurno.EjecutarAsync(
            token => Task.Run(() => _cajaService.ObtenerTurnoActivoAsync(token), token),
            turno =>
            {
                TurnoActivo = turno;
                TieneTurnoActivo = TurnoActivo != null;
            },
            ex => _dialogService.MostrarError("Error", $"Error al cargar el turno activo: {ex.Message}"));
    }

    [RelayCommand]
    public async Task AbrirTurnoAsync()
    {
        if (_dialogService.MostrarDialogoApertura(out decimal saldoInicial) == true)
        {
            try
            {
                // ID de usuario temporal de sesión
                int usuarioIdActual = 1;

                var dto = new AperturaTurnoDto
                {
                    IdUsuario = usuarioIdActual,
                    SaldoInicial = saldoInicial
                };

                TurnoActivo = await _cajaService.AbrirTurnoAsync(dto);
                TieneTurnoActivo = true;

                _dialogService.MostrarInformacion("Caja", "Turno abierto exitosamente.");
            }
            catch (Exception ex)
            {
                _dialogService.MostrarAdvertencia("Error de Apertura", $"No se pudo abrir el turno: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task RegistrarMovimientoAsync()
    {
        if (TurnoActivo == null) return;

        var dto = _dialogService.MostrarDialogoMovimiento(TurnoActivo.IdTurno);
        if (dto != null)
        {
            try
            {
                TurnoActivo = await _cajaService.RegistrarMovimientoAsync(dto);
                _dialogService.MostrarInformacion("Caja", "Movimiento registrado correctamente.");
            }
            catch (Exception ex)
            {
                _dialogService.MostrarError("Error de Movimiento", $"No se pudo registrar el movimiento: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public async Task CerrarTurnoAsync()
    {
        if (TurnoActivo == null) return;

        if (_dialogService.MostrarDialogoArqueo(TurnoActivo.IdTurno, out decimal saldoDeclarado, out decimal montoRetenido) == true)
        {
            try
            {
                var dto = new ArqueoCiegoDto
                {
                    IdTurno = TurnoActivo.IdTurno,
                    SaldoDeclaradoEfectivo = saldoDeclarado,
                    MontoRetenidoEnCaja = montoRetenido
                };

                var resultado = await _cajaService.CerrarTurnoConArqueoCiegoAsync(dto);

                _dialogService.MostrarInformacion("Arqueo de Caja",
                    $"Turno cerrado con éxito.\nSaldo Teórico: ${resultado.SaldoTeoricoEfectivo:N2}\nDeclarado: ${resultado.SaldoDeclaradoEfectivo:N2}\nDiferencia: ${resultado.DiferenciaEfectivo:N2}");

                TurnoActivo = null;
                TieneTurnoActivo = false;
            }
            catch (Exception ex)
            {
                _dialogService.MostrarError("Error", $"Error al cerrar el turno: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Lo invoca el scope de la pantalla al salir de ella: cancela la carga en curso para que no use el
    /// DbContext ya descartado ni muestre errores en otra pantalla (H-19).
    /// </summary>
    public void Dispose()
    {
        _cargaTurno.Dispose();
        GC.SuppressFinalize(this);
    }
}
