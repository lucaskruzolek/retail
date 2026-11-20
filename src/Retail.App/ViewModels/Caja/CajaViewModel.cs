using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Caja;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Caja;

/// <summary>
/// Pantalla de Caja: turno de esta terminal, movimientos de efectivo y arqueo. Un turno es de quien lo abrió: solo su
/// dueño lo opera (vende, cobra, mueve efectivo), y un Gerente puede cerrar el de otro cajero, de esta terminal o de
/// cualquier otra, con un arqueo (MT-14). Si la terminal tiene un turno ajeno sin cerrar, la pantalla lo dice y ofrece
/// cerrarlo solo a quien puede.
/// </summary>
public partial class CajaViewModel : ObservableObject, IDisposable
{
    // Serializa la lectura del turno sobre el DbContext de esta pantalla y la cancela al salir de ella (H-19).
    private readonly CargaSerializada _cargaTurno = new();

    private readonly ICajaService _cajaService;
    private readonly ICajaDialogService _dialogService;
    private readonly ICurrentUserSession _session;

    [ObservableProperty]
    private TurnoCajaDto? _turnoActivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTurnoAbierto))]
    private bool _tieneTurnoActivo;

    /// <summary>La Gerencia ya consultó los turnos de las otras terminales (para no mostrar el estado vacío antes de saberlo).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoHayTurnosEnOtrasTerminales))]
    private bool _turnosEnOtrasTerminalesCargados;

    /// <summary>Turnos abiertos en las demás terminales, solo para un Gerente (MT-14).</summary>
    public ObservableCollection<TurnoAbiertoDto> TurnosEnOtrasTerminales { get; } = [];

    /// <summary>Hay un turno abierto en esta terminal, sea de quien sea.</summary>
    public bool IsTurnoAbierto => TurnoActivo != null && TurnoActivo.Estado == Domain.Enums.EstadoTurnoEnum.Abierto;

    /// <summary>El turno abierto de esta terminal es del usuario de la sesión.</summary>
    public bool EsTurnoPropio => IsTurnoAbierto && TurnoActivo!.IdUsuario == _session.IdUsuario;

    /// <summary>El turno abierto de esta terminal es de otro cajero (un turno "pausado").</summary>
    public bool EsTurnoAjeno => IsTurnoAbierto && !EsTurnoPropio;

    /// <summary>Solo el dueño del turno mueve efectivo (y vende o cobra desde las otras pantallas).</summary>
    public bool PuedeOperar => EsTurnoPropio;

    /// <summary>Cierra el dueño, o un Gerente un turno ajeno.</summary>
    public bool PuedeCerrar => EsTurnoPropio || (EsTurnoAjeno && _session.EsGerente);

    /// <summary>"Abrir turno" solo se ofrece si la terminal no tiene ninguno abierto: hay uno por terminal.</summary>
    public bool PuedeAbrirTurno => !IsTurnoAbierto;

    public bool EsGerente => _session.EsGerente;

    public string TituloTurnoAjeno => $"Turno de {TurnoActivo?.NombreUsuario ?? "otro cajero"} sin cerrar";

    public bool NoHayTurnosEnOtrasTerminales => TurnosEnOtrasTerminalesCargados && TurnosEnOtrasTerminales.Count == 0;

    public CajaViewModel(ICajaService cajaService, ICajaDialogService dialogService, ICurrentUserSession session)
    {
        _cajaService = cajaService;
        _dialogService = dialogService;
        _session = session ?? throw new ArgumentNullException(nameof(session));

        // Cargar estado inicial del turno al arrancar la vista
        _ = CargarTurnoActivoAsync();
    }

    partial void OnTurnoActivoChanged(TurnoCajaDto? value)
    {
        OnPropertyChanged(nameof(IsTurnoAbierto));
        OnPropertyChanged(nameof(EsTurnoPropio));
        OnPropertyChanged(nameof(EsTurnoAjeno));
        OnPropertyChanged(nameof(PuedeOperar));
        OnPropertyChanged(nameof(PuedeCerrar));
        OnPropertyChanged(nameof(PuedeAbrirTurno));
        OnPropertyChanged(nameof(TituloTurnoAjeno));
    }

    /// <summary>
    /// Lee el turno de esta terminal y, si quien mira es un Gerente, los turnos abiertos en las otras. Las dos lecturas
    /// van una tras otra dentro de la misma operación serializada: comparten el DbContext de la pantalla (H-19).
    /// </summary>
    [RelayCommand]
    public Task CargarTurnoActivoAsync()
    {
        var esGerente = _session.EsGerente;
        var idActor = _session.IdUsuario;

        return _cargaTurno.EjecutarAsync(
            token => Task.Run(async () =>
            {
                var turno = await _cajaService.ObtenerTurnoActivoAsync(token);

                IReadOnlyList<TurnoAbiertoDto> abiertos = [];
                if (esGerente && idActor is int id)
                {
                    abiertos = await _cajaService.ListarTurnosAbiertosAsync(id, token);
                }

                return (Turno: turno, Abiertos: abiertos);
            }, token),
            resultado =>
            {
                TurnoActivo = resultado.Turno;
                TieneTurnoActivo = TurnoActivo != null;

                // El turno de esta terminal ya está en la parte de arriba: la lista solo trae las demás terminales.
                TurnosEnOtrasTerminales.Clear();
                foreach (var abierto in resultado.Abiertos.Where(a => a.IdTurno != resultado.Turno?.IdTurno))
                {
                    TurnosEnOtrasTerminales.Add(abierto);
                }

                TurnosEnOtrasTerminalesCargados = esGerente;
                OnPropertyChanged(nameof(NoHayTurnosEnOtrasTerminales));
            },
            ex => _dialogService.MostrarError("Error", $"Error al cargar el turno activo: {ex.Message}"));
    }

    [RelayCommand]
    public async Task AbrirTurnoAsync()
    {
        if (!PuedeAbrirTurno)
        {
            return;
        }

        if (_session.IdUsuario is not int idUsuario)
        {
            _dialogService.MostrarAdvertencia("Sesión incompleta", "No se pudo identificar el usuario de la sesión. Vuelva a iniciar sesión.");
            return;
        }

        if (_dialogService.MostrarDialogoApertura(out decimal saldoInicial) == true)
        {
            try
            {
                var dto = new AperturaTurnoDto
                {
                    IdUsuario = idUsuario,
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
        if (TurnoActivo == null || !PuedeOperar || _session.IdUsuario is not int idUsuario)
        {
            return;
        }

        var dto = _dialogService.MostrarDialogoMovimiento(TurnoActivo.IdTurno);
        if (dto != null)
        {
            try
            {
                TurnoActivo = await _cajaService.RegistrarMovimientoAsync(dto with { IdUsuario = idUsuario });
                _dialogService.MostrarInformacion("Caja", "Movimiento registrado correctamente.");
            }
            catch (Exception ex)
            {
                await InformarFallaAsync("Error de Movimiento", "No se pudo registrar el movimiento", ex);
            }
        }
    }

    /// <summary>Arqueo y cierre del turno de esta terminal: lo hace su dueño o, si es ajeno, un Gerente.</summary>
    [RelayCommand]
    public async Task CerrarTurnoAsync()
    {
        if (TurnoActivo is not { } turno || !PuedeCerrar)
        {
            return;
        }

        var cerrado = await CerrarConArqueoAsync(turno.IdTurno, turno.SaldoTeoricoEfectivo, turno.TotalVentasElectronicas, turno.CantidadVentas);
        if (cerrado)
        {
            TurnoActivo = null;
            TieneTurnoActivo = false;
        }
    }

    /// <summary>
    /// Un Gerente cierra con arqueo el turno abierto de otro cajero en otra terminal. Los tres valores "leídos" son los
    /// de la fila que vio: si el turno siguió operando, el servicio rechaza el cierre y la lista se vuelve a cargar.
    /// </summary>
    [RelayCommand]
    public async Task CerrarTurnoDeOtraTerminalAsync(TurnoAbiertoDto? turno)
    {
        if (turno is null || !_session.EsGerente)
        {
            return;
        }

        if (await CerrarConArqueoAsync(turno.IdTurno, turno.SaldoTeoricoEfectivo, turno.TotalVentasElectronicas, turno.CantidadVentas))
        {
            await CargarTurnoActivoAsync();
        }
    }

    private async Task<bool> CerrarConArqueoAsync(int idTurno, decimal saldoTeoricoLeido, decimal totalElectronicoLeido, int cantidadVentasLeida)
    {
        if (_session.IdUsuario is not int idActor)
        {
            _dialogService.MostrarAdvertencia("Sesión incompleta", "No se pudo identificar el usuario de la sesión. Vuelva a iniciar sesión.");
            return false;
        }

        if (_dialogService.MostrarDialogoArqueo(idTurno, out decimal saldoDeclarado, out decimal montoRetenido) != true)
        {
            return false;
        }

        try
        {
            var dto = new ArqueoCiegoDto
            {
                IdTurno = idTurno,
                IdUsuarioActor = idActor,
                SaldoTeoricoLeido = saldoTeoricoLeido,
                TotalElectronicoLeido = totalElectronicoLeido,
                CantidadVentasLeida = cantidadVentasLeida,
                SaldoDeclaradoEfectivo = saldoDeclarado,
                MontoRetenidoEnCaja = montoRetenido
            };

            var resultado = await _cajaService.CerrarTurnoConArqueoCiegoAsync(dto);

            _dialogService.MostrarInformacion("Arqueo de Caja",
                $"Turno cerrado con éxito.\nSaldo Teórico: ${resultado.SaldoTeoricoEfectivo:N2}\nDeclarado: ${resultado.SaldoDeclaradoEfectivo:N2}\nDiferencia: ${resultado.DiferenciaEfectivo:N2}");
            return true;
        }
        catch (Exception ex)
        {
            await InformarFallaAsync("Error", "Error al cerrar el turno", ex);
            return false;
        }
    }

    /// <summary>
    /// Las reglas de negocio y de concurrencia (turno ajeno, turno modificado, operación no autorizada, conflicto) son
    /// información para quien opera: se muestran como advertencia con el mensaje tal cual. Si el turno cambió mientras
    /// se miraba, se vuelve a leer para que la pantalla muestre lo que hay ahora. Lo inesperado se informa como error.
    /// </summary>
    private async Task InformarFallaAsync(string titulo, string accion, Exception ex)
    {
        switch (ex)
        {
            case TurnoAjenoException or OperacionNoAutorizadaException:
                _dialogService.MostrarAdvertencia(titulo, ex.Message);
                break;
            case TurnoModificadoException or ConflictoDeConcurrenciaException:
                _dialogService.MostrarAdvertencia(titulo, ex.Message);
                await CargarTurnoActivoAsync();
                break;
            case ValidationException validacion:
                _dialogService.MostrarAdvertencia(titulo, string.Join("\n", validacion.Errors.Select(e => e.ErrorMessage).Distinct()));
                break;
            case DomainException:
                _dialogService.MostrarAdvertencia(titulo, ex.Message);
                break;
            default:
                _dialogService.MostrarError(titulo, $"{accion}: {ex.Message}");
                break;
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
