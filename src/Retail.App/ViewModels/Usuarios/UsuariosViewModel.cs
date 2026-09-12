using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Services;
using Retail.Application.DTOs.Usuarios;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Usuarios;

/// <summary>
/// ViewModel principal para el panel gerencial de administración de usuarios y roles RBAC (RF-03).
/// </summary>
public partial class UsuariosViewModel : ObservableObject
{
    private readonly IUsuarioService _usuarioService;
    private readonly IUsuarioDialogService _dialogService;
    private readonly ILogger<UsuariosViewModel> _logger;
    private List<UsuarioDto> _cacheUsuarios = new();

    public ObservableCollection<UsuarioDto> Usuarios { get; } = new();

    [ObservableProperty]
    private UsuarioDto? _usuarioSeleccionado;

    [ObservableProperty]
    private string _filtroBusqueda = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    public UsuariosViewModel(
        IUsuarioService usuarioService,
        IUsuarioDialogService dialogService,
        ILogger<UsuariosViewModel>? logger = null)
    {
        _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? NullLogger<UsuariosViewModel>.Instance;
    }

    partial void OnFiltroBusquedaChanged(string value)
    {
        AplicarFiltroLocal();
    }

    [RelayCommand]
    public async Task CargarUsuariosAsync()
    {
        try
        {
            IsBusy = true;
            MensajeError = null;
            MensajeEstado = "Cargando operadores...";

            var lista = await _usuarioService.ListarUsuariosAsync();
            _cacheUsuarios = lista.ToList();

            AplicarFiltroLocal();

            MensajeEstado = $"Se cargaron {Usuarios.Count} operadores.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de operadores: {Mensaje}", ex.Message);
            MensajeError = $"Error al obtener usuarios: {ex.Message}";
            _dialogService.MostrarError("Error de Carga", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task NuevoUsuarioAsync()
    {
        try
        {
            UsuarioDto? nuevo = null;
            var dto = _dialogService.MostrarDialogoCrear(async nuevoDto =>
            {
                nuevo = await _usuarioService.RegistrarUsuarioAsync(nuevoDto);
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

        try
        {
            UsuarioDto? actualizado = null;
            var dto = _dialogService.MostrarDialogoModificar(target, async modDto =>
            {
                actualizado = await _usuarioService.ModificarUsuarioAsync(modDto);
            });

            if (dto == null)
            {
                return;
            }

            _dialogService.MostrarInformacion("Operación Exitosa", $"Los datos de '{dto.NombreCompleto}' fueron actualizados.");

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

            await _usuarioService.BajaUsuarioAsync(target.IdUsuario);
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

        try
        {
            var dto = _dialogService.MostrarDialogoCambiarPassword(target, async passDto =>
            {
                await _usuarioService.CambiarPasswordAsync(passDto);
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

    private void AplicarFiltroLocal()
    {
        Usuarios.Clear();

        var query = _cacheUsuarios.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FiltroBusqueda))
        {
            var texto = FiltroBusqueda.Trim();
            query = query.Where(u =>
                u.NombreUsuario.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                u.NombreCompleto.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var u in query)
        {
            Usuarios.Add(u);
        }
    }
}
