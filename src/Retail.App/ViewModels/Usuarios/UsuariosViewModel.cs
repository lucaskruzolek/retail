using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Usuarios;

/// <summary>
/// ViewModel principal para el panel gerencial de administración de usuarios y roles RBAC (RF-03).
/// </summary>
public partial class UsuariosViewModel : ObservableObject, IDisposable
{
    // Serializa las cargas de operadores sobre el DbContext de esta pantalla y las cancela al salir de ella (H-19).
    private readonly CargaSerializada _cargaUsuarios = new();

    private readonly IUsuarioService _usuarioService;
    private readonly IUsuarioDialogService _dialogService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<UsuariosViewModel> _logger;
    private List<UsuarioDto> _cacheUsuarios = new();

    // Roles que el operador de la sesión puede gestionar. Los calcula el servicio con la matriz del Dominio.
    private IReadOnlyList<RolUsuarioEnum> _rolesAsignables = [];

    public ObservableCollection<UsuarioDto> Usuarios { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DesbloquearCommand))]
    private UsuarioDto? _usuarioSeleccionado;

    [ObservableProperty]
    private string _filtroBusqueda = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DesbloquearCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    [ObservableProperty]
    private int _paginaActual = 1;

    [ObservableProperty]
    private int _tamanoPagina = 10;

    [ObservableProperty]
    private int _totalPaginas = 1;

    [ObservableProperty]
    private int _totalRegistrosFiltrados;

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion => TotalRegistrosFiltrados == 0
        ? "Sin operadores registrados"
        : $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalRegistrosFiltrados)} de {TotalRegistrosFiltrados} operadores";

    /// <summary>
    /// Solo el Gerente ve el botón de desbloqueo (D-31); la autoridad real la decide <c>UsuarioService</c>.
    /// </summary>
    public bool PuedeDesbloquear => _session.EsGerente;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = [10, 20, 50];

    public UsuariosViewModel(
        IUsuarioService usuarioService,
        IUsuarioDialogService dialogService,
        ICurrentUserSession session,
        ILogger<UsuariosViewModel>? logger = null)
    {
        _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? NullLogger<UsuariosViewModel>.Instance;
    }

    /// <summary>
    /// Usuario que ejecuta cada operación. Sin sesión se envía 0 y el servicio rechaza la operación: la UI no
    /// decide permisos, solo informa quién actúa.
    /// </summary>
    private int IdActor => _session.IdUsuario ?? 0;

    private void CambiarPasswordPropia(UsuarioDto cuentaPropia)
    {
        try
        {
            var cambiada = _dialogService.MostrarDialogoCambioPropio(
                cuentaPropia,
                passDto => _usuarioService.CambiarPasswordAsync(IdActor, passDto));

            if (cambiada)
            {
                _dialogService.MostrarInformacion("Contraseña Actualizada", "Su contraseña fue cambiada correctamente.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar la contraseña propia del operador {IdUsuario}: {Mensaje}", IdActor, ex.Message);
            MensajeError = ex.Message;
            _dialogService.MostrarError("Error al Cambiar Contraseña", ex.Message);
        }
    }

    /// <summary>
    /// Aviso anticipado: evita abrir un diálogo cuya operación el servicio va a rechazar. No reemplaza la
    /// autorización de <c>UsuarioService</c>, que es la que decide.
    /// </summary>
    private bool PuedeGestionar(UsuarioDto usuario)
    {
        if (_rolesAsignables.Contains(usuario.Rol))
        {
            return true;
        }

        _dialogService.MostrarError(
            "Operación no permitida",
            $"Su rol no tiene autoridad para gestionar la cuenta de '{usuario.NombreCompleto}' ({usuario.Rol}).");
        return false;
    }

    partial void OnFiltroBusquedaChanged(string value)
    {
        PaginaActual = 1;
        AplicarFiltroLocal();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        AplicarFiltroLocal();
    }

    [RelayCommand]
    public async Task CargarUsuariosAsync()
    {
        IsBusy = true;
        MensajeError = null;
        MensajeEstado = "Cargando operadores...";

        await _cargaUsuarios.EjecutarAsync(
            token => Task.Run(
                async () =>
                {
                    // Secuencial a propósito: ambas consultas comparten el DbContext de la pantalla.
                    var roles = await _usuarioService.ObtenerRolesAsignablesAsync(IdActor, token);
                    var usuarios = await _usuarioService.ListarUsuariosAsync(IdActor, token);
                    return (Usuarios: usuarios, Roles: roles);
                },
                token),
            resultado =>
            {
                var lista = resultado.Usuarios;
                _rolesAsignables = resultado.Roles;
                _cacheUsuarios = lista.ToList();

                PaginaActual = 1;
                AplicarFiltroLocal();

                MensajeEstado = $"Se cargaron {_cacheUsuarios.Count} operadores.";
            },
            ex =>
            {
                _logger.LogError(ex, "Error al obtener la lista de operadores: {Mensaje}", ex.Message);
                MensajeError = $"Error al obtener usuarios: {ex.Message}";
                _dialogService.MostrarError("Error de Carga", ex.Message);
            });

        IsBusy = _cargaUsuarios.EnCurso;
    }

    [RelayCommand]
    public async Task NuevoUsuarioAsync()
    {
        try
        {
            if (_rolesAsignables.Count == 0)
            {
                _dialogService.MostrarError("Operación no permitida", "Su rol no puede dar de alta operadores.");
                return;
            }

            UsuarioDto? nuevo = null;
            var dto = _dialogService.MostrarDialogoCrear(_rolesAsignables, async nuevoDto =>
            {
                nuevo = await _usuarioService.RegistrarUsuarioAsync(IdActor, nuevoDto);
            });

            if (dto == null)
            {
                return;
            }

            _dialogService.MostrarInformacion("Operación Exitosa", $"El operador '{dto.NombreUsuario}' fue creado exitosamente.");

            await CargarUsuariosAsync();
            UsuarioSeleccionado = nuevo != null
                ? Usuarios.FirstOrDefault(u => u.IdUsuario == nuevo.IdUsuario)
                : Usuarios.FirstOrDefault(u => u.NombreUsuario == dto.NombreUsuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar nuevo operador: {Mensaje}", ex.Message);
            MensajeError = ex.Message;
            _dialogService.MostrarError("Error al Crear Operador", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task EditarUsuarioAsync(UsuarioDto? usuario = null)
    {
        var target = usuario ?? UsuarioSeleccionado;
        if (target == null)
        {
            _dialogService.MostrarError("Selección Requerida", "Seleccione un operador de la lista para editar sus datos.");
            return;
        }

        if (!PuedeGestionar(target))
        {
            return;
        }

        try
        {
            UsuarioDto? actualizado = null;
            var dto = _dialogService.MostrarDialogoModificar(target, _rolesAsignables, async modDto =>
            {
                actualizado = await _usuarioService.ModificarUsuarioAsync(IdActor, modDto);
            });

            if (dto == null)
            {
                return;
            }

            // Se muestra el nombre que devolvió el servicio, ya normalizado por el agregado, que es el que se guardó.
            var nombreGuardado = actualizado?.NombreCompleto ?? $"{dto.Nombre} {dto.Apellido}";
            _dialogService.MostrarInformacion("Operación Exitosa", $"Los datos de '{nombreGuardado}' fueron actualizados.");

            await CargarUsuariosAsync();
            UsuarioSeleccionado = Usuarios.FirstOrDefault(u => u.IdUsuario == (actualizado?.IdUsuario ?? dto.IdUsuario));
        }
        catch (UltimoGerenteException ex)
        {
            _logger.LogWarning(ex, "Intento bloqueado de revocar rol de gerente: {Mensaje}", ex.Message);
            MensajeError = ex.Message;
            _dialogService.MostrarError("Invariante de Seguridad Violada", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al modificar operador {IdUsuario}: {Mensaje}", target.IdUsuario, ex.Message);
            MensajeError = ex.Message;
            _dialogService.MostrarError("Error al Modificar Operador", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task BajaUsuarioAsync(UsuarioDto? usuario = null)
    {
        var target = usuario ?? UsuarioSeleccionado;
        if (target == null)
        {
            _dialogService.MostrarError("Selección Requerida", "Seleccione un operador para dar de baja.");
            return;
        }

        if (!target.Activo)
        {
            _dialogService.MostrarInformacion("Aviso", $"El operador '{target.NombreUsuario}' ya se encuentra dado de baja.");
            return;
        }

        if (!PuedeGestionar(target))
        {
            return;
        }

        var confirmar = _dialogService.Confirmar(
            "Confirmar Baja de Operador",
            $"¿Está seguro de que desea dar de baja al operador '{target.NombreCompleto}' ({target.NombreUsuario})?\n\nEsta acción aplicará un borrado lógico preservando el historial contable.");

        if (!confirmar)
        {
            return;
        }

        try
        {
            IsBusy = true;
            MensajeError = null;

            await _usuarioService.BajaUsuarioAsync(IdActor, target.IdUsuario);
            _dialogService.MostrarInformacion("Baja Aplicada", $"El operador '{target.NombreCompleto}' fue dado de baja correctamente.");

            await CargarUsuariosAsync();
        }
        catch (UltimoGerenteException ex)
        {
            _logger.LogWarning(ex, "Intento bloqueado de dar de baja al último gerente: {Mensaje}", ex.Message);
            MensajeError = ex.Message;
            _dialogService.MostrarError("Operación Bloqueada", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al dar de baja al operador {IdUsuario}: {Mensaje}", target.IdUsuario, ex.Message);
            MensajeError = ex.Message;
            _dialogService.MostrarError("Error al Dar de Baja", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool PuedeDesbloquearSeleccionada() => PuedeDesbloquear && !IsBusy && UsuarioSeleccionado?.EstaBloqueada == true;

    /// <summary>
    /// Levanta el bloqueo de la cuenta de Administrador seleccionada (D-31). No cambia su contraseña.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeDesbloquearSeleccionada))]
    public async Task DesbloquearAsync()
    {
        var target = UsuarioSeleccionado;
        if (target is null || !target.EstaBloqueada)
        {
            return;
        }

        var confirmar = _dialogService.Confirmar(
            "Confirmar Desbloqueo",
            $"¿Desea desbloquear la cuenta de '{target.NombreCompleto}' ({target.NombreUsuario})?\n\nLa cuenta fue bloqueada por intentos de autorización fallidos. Su contraseña no se modifica.");

        if (!confirmar)
        {
            return;
        }

        try
        {
            IsBusy = true;
            MensajeError = null;

            // Ley 4: la escritura en la base no corre en el hilo de la UI.
            await Task.Run(() => _usuarioService.DesbloquearUsuarioAsync(IdActor, target.IdUsuario));
            _dialogService.MostrarInformacion("Cuenta Desbloqueada", $"La cuenta de '{target.NombreCompleto}' fue desbloqueada correctamente.");

            await CargarUsuariosAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al desbloquear al operador {IdUsuario}: {Mensaje}", target.IdUsuario, ex.Message);
            MensajeError = ex.Message;
            _dialogService.MostrarError("Error al Desbloquear", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RestablecerPasswordAsync(UsuarioDto? usuario = null)
    {
        await Task.CompletedTask;

        var target = usuario ?? UsuarioSeleccionado;
        if (target == null)
        {
            _dialogService.MostrarError("Selección Requerida", "Seleccione un operador para restablecer su contraseña.");
            return;
        }

        // La propia cuenta no se blanquea: se cambia pidiendo la contraseña actual (el servicio lo exige).
        if (target.IdUsuario == IdActor)
        {
            CambiarPasswordPropia(target);
            return;
        }

        if (!PuedeGestionar(target))
        {
            return;
        }

        try
        {
            var dto = _dialogService.MostrarDialogoCambiarPassword(target, async passDto =>
            {
                await _usuarioService.CambiarPasswordAsync(IdActor, passDto);
            });

            if (dto == null)
            {
                return;
            }

            _dialogService.MostrarInformacion("Contraseña Actualizada", $"Se restableció la contraseña para el operador '{target.NombreUsuario}'.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al restablecer contraseña del operador {IdUsuario}: {Mensaje}", target.IdUsuario, ex.Message);
            MensajeError = ex.Message;
            _dialogService.MostrarError("Error al Cambiar Contraseña", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void PaginaSiguiente()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            ActualizarPaginaActual();
        }
    }

    [RelayCommand]
    public void PaginaAnterior()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            ActualizarPaginaActual();
        }
    }

    [RelayCommand]
    public void PrimeraPagina()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            ActualizarPaginaActual();
        }
    }

    [RelayCommand]
    public void UltimaPagina()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            ActualizarPaginaActual();
        }
    }

    /// <summary>Va a una página puntual (los números de la barra de paginación); fuera de rango no hace nada.</summary>
    [RelayCommand]
    public void IrAPagina(int pagina)
    {
        if (pagina >= 1 && pagina <= TotalPaginas && pagina != PaginaActual)
        {
            PaginaActual = pagina;
            ActualizarPaginaActual();
        }
    }

    private List<UsuarioDto> _cacheFiltrados = new();

    private void AplicarFiltroLocal()
    {
        var query = _cacheUsuarios.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FiltroBusqueda))
        {
            var texto = FiltroBusqueda.Trim();
            query = query.Where(u =>
                u.NombreUsuario.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                u.NombreCompleto.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        _cacheFiltrados = query
            .OrderBy(u => u.Apellido)
            .ThenBy(u => u.Nombre)
            .ToList();
        TotalRegistrosFiltrados = _cacheFiltrados.Count;

        TotalPaginas = Math.Max(1, (int)Math.Ceiling((double)TotalRegistrosFiltrados / Math.Max(1, TamanoPagina)));

        if (PaginaActual > TotalPaginas)
        {
            PaginaActual = TotalPaginas;
        }
        else if (PaginaActual < 1)
        {
            PaginaActual = 1;
        }

        ActualizarPaginaActual();
    }

    private void ActualizarPaginaActual()
    {
        var paginaItems = _cacheFiltrados
            .Skip((PaginaActual - 1) * TamanoPagina)
            .Take(TamanoPagina)
            .ToList();

        Usuarios.Clear();
        foreach (var u in paginaItems)
        {
            Usuarios.Add(u);
        }

        OnPropertyChanged(nameof(PuedeRetrocederPagina));
        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(InformacionPaginacion));
    }

    /// <summary>
    /// Lo invoca el scope de la pantalla al salir de ella: cancela la carga en curso para que no use el
    /// DbContext ya descartado ni muestre errores en otra pantalla (H-19).
    /// </summary>
    public void Dispose()
    {
        _cargaUsuarios.Dispose();
        GC.SuppressFinalize(this);
    }
}
