using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Paso 2 del modal de incorporación, "Términos nuevos" (Track 6.6, P6-3, P6-4): una tarjeta por bloque de ítems cuyo
/// término el sistema no conoce. Registrar el término alimenta la tabla de términos (M-16), así que exige D-5 (P6-6);
/// sin ella, el paso es de solo lectura.
/// </summary>
public partial class TerminosNuevosPasoViewModel : ObservableObject
{
    private readonly ITerminoClasificacionService _terminoService;
    private readonly IRubroService _rubroService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger _logger;

    public ObservableCollection<BloqueTerminoNuevoViewModel> Bloques { get; } = new();

    /// <summary>
    /// Categorías del modal, compartidas: la que se crea en este paso se suma y queda disponible en la grilla.
    /// </summary>
    public ObservableCollection<CategoriaDto> Categorias { get; }

    public ObservableCollection<RubroDto> Rubros { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoloLectura))]
    private bool _puedeAdministrar;

    public bool SoloLectura => !PuedeAdministrar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public TerminosNuevosPasoViewModel(
        ITerminoClasificacionService terminoService,
        IRubroService rubroService,
        ICurrentUserSession session,
        ILogger logger,
        ObservableCollection<CategoriaDto> categorias)
    {
        _terminoService = terminoService ?? throw new ArgumentNullException(nameof(terminoService));
        _rubroService = rubroService ?? throw new ArgumentNullException(nameof(rubroService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Categorias = categorias ?? throw new ArgumentNullException(nameof(categorias));
    }

    /// <summary>
    /// Reemplaza las tarjetas por los bloques que informó el motor en su última respuesta. Los rubros, que solo hacen
    /// falta para crear categorías, se leen la primera vez que hay bloques y la persona puede escribir diccionarios.
    /// </summary>
    public async Task CargarAsync(IEnumerable<BloqueTerminoNuevoDto> bloques, bool puedeAdministrar)
    {
        PuedeAdministrar = puedeAdministrar;
        MensajeError = null;
        Bloques.Clear();
        foreach (var bloque in bloques)
        {
            Bloques.Add(new BloqueTerminoNuevoViewModel(bloque));
        }

        if (puedeAdministrar && Bloques.Count > 0 && Rubros.Count == 0)
        {
            await CargarRubrosAsync();
        }
    }

    [RelayCommand]
    private async Task AsignarCategoriaExistenteAsync(BloqueTerminoNuevoViewModel? bloque)
    {
        if (bloque is null || bloque.Resuelto || !PuedeAdministrar)
        {
            return;
        }

        if (bloque.CategoriaSeleccionada is not { } categoria)
        {
            MensajeError = $"Elija la categoría de «{bloque.Termino}».";
            return;
        }

        var dto = new RegistrarTerminoDto { Termino = bloque.Termino, IdCategoria = categoria.IdCategoria };
        await EjecutarAsync(
            bloque,
            idActor => _terminoService.RegistrarAsync(idActor, dto),
            $"Asignado a {categoria.NombreCategoria}");
    }

    [RelayCommand]
    private async Task CrearCategoriaYAsignarAsync(BloqueTerminoNuevoViewModel? bloque)
    {
        if (bloque is null || bloque.Resuelto || !PuedeAdministrar)
        {
            return;
        }

        string nombre = bloque.NombreCategoriaNueva.Trim();
        if (nombre.Length == 0 || bloque.RubroSeleccionado is not { } rubro)
        {
            MensajeError = "Para crear la categoría escriba su nombre y elija su rubro.";
            return;
        }

        var dto = new RegistrarTerminoConCategoriaNuevaDto
        {
            Termino = bloque.Termino,
            NombreCategoria = nombre,
            IdRubro = rubro.IdRubro
        };

        CategoriaDto? creada = null;
        await EjecutarAsync(
            bloque,
            async idActor => creada = await _terminoService.RegistrarConCategoriaNuevaAsync(idActor, dto),
            $"Categoría nueva «{nombre}»");

        // La colección está enlazada a la UI: se modifica acá, en su hilo, y no dentro de Task.Run.
        if (bloque.Resuelto && creada is not null)
        {
            Categorias.Add(creada);
        }
    }

    [RelayCommand]
    private async Task MarcarAmbiguoAsync(BloqueTerminoNuevoViewModel? bloque)
    {
        if (bloque is null || bloque.Resuelto || !PuedeAdministrar)
        {
            return;
        }

        var dto = new RegistrarTerminoDto { Termino = bloque.Termino };
        await EjecutarAsync(bloque, idActor => _terminoService.RegistrarAsync(idActor, dto), "Marcado como ambiguo");
    }

    private async Task EjecutarAsync(BloqueTerminoNuevoViewModel bloque, Func<int, Task> operacion, string resultado)
    {
        MensajeError = null;
        int idActor = _session.IdUsuario ?? 0;

        try
        {
            await Task.Run(() => operacion(idActor));
            bloque.Resultado = resultado;
            bloque.Resuelto = true;
        }
        catch (ValidationException ex)
        {
            MensajeError = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (DomainException ex)
        {
            // Incluye OperacionNoAutorizadaException (D-5) y el aviso de que otro usuario registró lo mismo.
            _logger.LogWarning(ex, "Regla de negocio al decidir el término {Termino}: {Mensaje}", bloque.Termino, ex.Message);
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al decidir el término {Termino}", bloque.Termino);
            MensajeError = "No se pudo guardar la decisión.";
        }
    }

    private async Task CargarRubrosAsync()
    {
        try
        {
            var rubros = await Task.Run(() => _rubroService.ListarAsync(null));
            Rubros.Clear();
            foreach (var rubro in rubros)
            {
                Rubros.Add(rubro);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer los rubros para crear categorías desde la incorporación");
            MensajeError = "No se pudieron leer los rubros: no se podrán crear categorías nuevas.";
        }
    }
}
