using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Detalle de la marca seleccionada en la sección Marcas del diálogo de clasificación (Track 6.6, M-68): sus alias
/// (otros nombres con los que aparece en las descripciones de los distribuidores) y sus prefijos EAN (identifican la
/// marca por el código de barras). Son entidades internas del agregado <c>Marca</c>, así que se administran desde su
/// raíz, como las categorías desde su rubro.
/// </summary>
/// <remarks>
/// Alias y prefijos se traen en una sola carga: el diálogo comparte una <see cref="CargaSerializada"/> y una carga
/// reemplazable nueva cancela la anterior, así que dos cargas separadas se pisarían al cambiar de marca.
/// </remarks>
public partial class DetalleMarcaViewModel : SeccionClasificacionViewModelBase
{
    private readonly IMarcaService _marcaService;

    public ObservableCollection<AliasListadoDto> Alias { get; } = new();

    public ObservableCollection<PrefijoListadoDto> Prefijos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMarca))]
    [NotifyCanExecuteChangedFor(nameof(NuevoAliasCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevoPrefijoCommand))]
    private MarcaDto? _marca;

    public bool HayMarca => Marca is not null;

    public DetalleMarcaViewModel(
        IMarcaService marcaService,
        IClasificacionDialogService dialogService,
        ICurrentUserSession session,
        CargaSerializada carga,
        ILogger logger)
        : base(dialogService, session, carga, logger)
    {
        _marcaService = marcaService ?? throw new ArgumentNullException(nameof(marcaService));
    }

    /// <summary>Muestra el detalle de la marca indicada; sin marca, el detalle queda vacío.</summary>
    public Task MostrarAsync(MarcaDto? marca)
    {
        Marca = marca;
        return CargarAsync();
    }

    private bool PuedeAgregar => PuedeEscribir && Marca is not null;

    [RelayCommand(CanExecute = nameof(PuedeAgregar))]
    private async Task NuevoAliasAsync()
    {
        if (Marca is not { } marca)
        {
            return;
        }

        MensajeError = null;
        var resultado = DialogService.AbrirFormularioAlias(marca);
        if (resultado is null)
        {
            return;
        }

        await EscribirAsync(
            "agregar el alias",
            (idActor, token) => _marcaService.AgregarAliasAsync(
                idActor,
                new AliasMarcaDto { IdMarca = marca.IdMarca, Alias = resultado.Texto },
                token),
            CargarAsync);
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private Task QuitarAliasAsync(AliasListadoDto? alias)
    {
        if (alias is null)
        {
            return Task.CompletedTask;
        }

        return DarDeBajaAsync(
            $"el alias '{alias.Alias}' de la marca '{alias.NombreMarca}'",
            (idActor, token) => _marcaService.QuitarAliasAsync(
                idActor,
                new AliasMarcaDto { IdMarca = alias.IdMarca, Alias = alias.Alias },
                token),
            CargarAsync);
    }

    [RelayCommand(CanExecute = nameof(PuedeAgregar))]
    private async Task NuevoPrefijoAsync()
    {
        if (Marca is not { } marca)
        {
            return;
        }

        MensajeError = null;
        var resultado = DialogService.AbrirFormularioPrefijo(marca);
        if (resultado is null)
        {
            return;
        }

        await EscribirAsync(
            "agregar el prefijo",
            (idActor, token) => _marcaService.AgregarPrefijoAsync(
                idActor,
                new PrefijoMarcaDto { IdMarca = marca.IdMarca, Prefijo = resultado.Texto },
                token),
            CargarAsync);
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private Task QuitarPrefijoAsync(PrefijoListadoDto? prefijo)
    {
        if (prefijo is null)
        {
            return Task.CompletedTask;
        }

        return DarDeBajaAsync(
            $"el prefijo '{prefijo.Prefijo}' de la marca '{prefijo.NombreMarca}'",
            (idActor, token) => _marcaService.QuitarPrefijoAsync(
                idActor,
                new PrefijoMarcaDto { IdMarca = prefijo.IdMarca, Prefijo = prefijo.Prefijo },
                token),
            CargarAsync);
    }

    private async Task CargarAsync()
    {
        if (Marca is not { } marca)
        {
            Alias.Clear();
            Prefijos.Clear();
            return;
        }

        int idMarca = marca.IdMarca;

        IsBusy = true;
        MensajeError = null;

        await Carga.EjecutarAsync(
            async token =>
            {
                // Consultas secuenciales: nunca se solapan sobre el DbContext.
                var alias = await Task.Run(() => _marcaService.ListarAliasAsync(null, idMarca, token), token);
                var prefijos = await Task.Run(() => _marcaService.ListarPrefijosAsync(null, idMarca, token), token);
                return (Alias: alias, Prefijos: prefijos);
            },
            resultado => EjecutarEnDispatcher(() =>
            {
                Reemplazar(Alias, resultado.Alias);
                Reemplazar(Prefijos, resultado.Prefijos);
            }),
            ex => InformarError("cargar los alias y prefijos de la marca", ex));

        IsBusy = Carga.EnCurso;
    }
}
