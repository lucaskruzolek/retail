using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// ViewModel del formulario modal único de alta y edición de rubros, categorías y marcas (Track 6.5, RF-04).
/// Los tres tipos comparten el campo nombre; solo la categoría suma el combo de rubro (D-23).
/// </summary>
public partial class ClasificacionFormViewModel : ObservableObject
{
    private readonly IRubroService _rubroService;
    private readonly ICategoriaService _categoriaService;
    private readonly IMarcaService _marcaService;
    private readonly ICurrentUserSession _session;

    private int _idElemento;

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private RubroDto? _rubroSeleccionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    [NotifyPropertyChangedFor(nameof(EtiquetaNombre))]
    [NotifyPropertyChangedFor(nameof(MostrarRubro))]
    private TipoClasificacion _tipo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    private bool _esModoEdicion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<RubroDto> Rubros { get; } = new();

    public bool MostrarRubro => Tipo == TipoClasificacion.Categoria;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public string TituloVentana => Tipo switch
    {
        TipoClasificacion.Rubro => EsModoEdicion ? "Editar rubro" : "Nuevo rubro",
        TipoClasificacion.Categoria => EsModoEdicion ? "Editar categoría" : "Nueva categoría",
        _ => EsModoEdicion ? "Editar marca" : "Nueva marca"
    };

    public string EtiquetaNombre => Tipo switch
    {
        TipoClasificacion.Rubro => "Nombre del rubro *",
        TipoClasificacion.Categoria => "Nombre de la categoría *",
        _ => "Nombre de la marca *"
    };

    public bool DialogResult { get; private set; }

    public ClasificacionFormViewModel(
        IRubroService rubroService,
        ICategoriaService categoriaService,
        IMarcaService marcaService,
        ICurrentUserSession session)
    {
        _rubroService = rubroService ?? throw new ArgumentNullException(nameof(rubroService));
        _categoriaService = categoriaService ?? throw new ArgumentNullException(nameof(categoriaService));
        _marcaService = marcaService ?? throw new ArgumentNullException(nameof(marcaService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void ConfigurarAltaRubro()
    {
        Reiniciar(TipoClasificacion.Rubro, esEdicion: false, idElemento: 0, nombre: string.Empty);
    }

    public void ConfigurarEdicionRubro(RubroDto rubro)
    {
        ArgumentNullException.ThrowIfNull(rubro);
        Reiniciar(TipoClasificacion.Rubro, esEdicion: true, rubro.IdRubro, rubro.NombreRubro);
    }

    public void ConfigurarAltaCategoria(IReadOnlyList<RubroDto> rubros, int? idRubroPreseleccionado)
    {
        ArgumentNullException.ThrowIfNull(rubros);
        Reiniciar(TipoClasificacion.Categoria, esEdicion: false, idElemento: 0, nombre: string.Empty);
        CargarRubros(rubros, idRubroPreseleccionado);
    }

    public void ConfigurarEdicionCategoria(CategoriaDto categoria, IReadOnlyList<RubroDto> rubros)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        ArgumentNullException.ThrowIfNull(rubros);
        Reiniciar(TipoClasificacion.Categoria, esEdicion: true, categoria.IdCategoria, categoria.NombreCategoria);
        CargarRubros(rubros, categoria.IdRubro);
    }

    public void ConfigurarAltaMarca()
    {
        Reiniciar(TipoClasificacion.Marca, esEdicion: false, idElemento: 0, nombre: string.Empty);
    }

    public void ConfigurarEdicionMarca(MarcaDto marca)
    {
        ArgumentNullException.ThrowIfNull(marca);
        Reiniciar(TipoClasificacion.Marca, esEdicion: true, marca.IdMarca, marca.NombreMarca);
    }

    [RelayCommand]
    private async Task GuardarAsync(Window? window)
    {
        IsBusy = true;
        MensajeError = null;

        try
        {
            await EjecutarGuardadoAsync();

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
            // Incluye OperacionNoAutorizadaException (D-5) y los duplicados detectados por el servicio.
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

    private async Task EjecutarGuardadoAsync()
    {
        int idActor = _session.IdUsuario ?? 0;

        switch (Tipo)
        {
            case TipoClasificacion.Rubro when EsModoEdicion:
                await _rubroService.RenombrarAsync(idActor, new RenombrarRubroDto { IdRubro = _idElemento, Nombre = Nombre });
                break;
            case TipoClasificacion.Rubro:
                await _rubroService.CrearAsync(idActor, new CrearRubroDto { Nombre = Nombre });
                break;
            case TipoClasificacion.Categoria when EsModoEdicion:
                await _categoriaService.ActualizarAsync(idActor, new ActualizarCategoriaDto
                {
                    IdCategoria = _idElemento,
                    Nombre = Nombre,
                    IdRubro = RubroSeleccionado?.IdRubro ?? 0
                });
                break;
            case TipoClasificacion.Categoria:
                await _categoriaService.CrearAsync(idActor, new CrearCategoriaDto
                {
                    Nombre = Nombre,
                    IdRubro = RubroSeleccionado?.IdRubro ?? 0
                });
                break;
            case TipoClasificacion.Marca when EsModoEdicion:
                await _marcaService.RenombrarAsync(idActor, new RenombrarMarcaDto { IdMarca = _idElemento, Nombre = Nombre });
                break;
            default:
                await _marcaService.CrearAsync(idActor, new CrearMarcaDto { Nombre = Nombre });
                break;
        }
    }

    private void Reiniciar(TipoClasificacion tipo, bool esEdicion, int idElemento, string nombre)
    {
        Tipo = tipo;
        EsModoEdicion = esEdicion;
        _idElemento = idElemento;
        Nombre = nombre;
        MensajeError = null;
        DialogResult = false;
        Rubros.Clear();
        RubroSeleccionado = null;
    }

    private void CargarRubros(IReadOnlyList<RubroDto> rubros, int? idRubroSeleccionado)
    {
        foreach (var rubro in rubros)
        {
            Rubros.Add(rubro);
        }

        RubroSeleccionado = idRubroSeleccionado.HasValue
            ? Rubros.FirstOrDefault(r => r.IdRubro == idRubroSeleccionado.Value)
            : null;
    }
}
