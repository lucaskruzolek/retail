using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Pestaña "Pares propuestos" del término ambiguo seleccionado (Track 6.6, M-49, M-71): los pares "término + segunda
/// palabra" que los artículos confirmados respaldan y todavía no están en el diccionario. Aceptar un par lo asigna a
/// su categoría (editable); descartarlo lo deja ignorado, para que no se vuelva a proponer.
/// </summary>
/// <remarks>
/// No carga por su cuenta: la sección Términos lee los pares y las propuestas del término en una sola carga
/// (dos cargas reemplazables sobre la misma <see cref="CargaSerializada"/> se cancelarían entre sí) y se las entrega
/// con <see cref="Mostrar"/>. Después de guardar, <c>recargar</c> vuelve a pedir esa carga.
/// </remarks>
public partial class ParesPropuestosViewModel : SeccionClasificacionViewModelBase
{
    private readonly ITerminoClasificacionService _terminoService;
    private readonly Func<Task> _recargar;

    public ObservableCollection<ParPropuestoItemViewModel> Propuestas { get; } = new();

    /// <summary>Categorías del catálogo, para corregir la categoría propuesta de cada fila.</summary>
    public ObservableCollection<CategoriaDto> Categorias { get; } = new();

    public bool HayPropuestas => Propuestas.Count > 0;

    public ParesPropuestosViewModel(
        ITerminoClasificacionService terminoService,
        IClasificacionDialogService dialogService,
        ICurrentUserSession session,
        CargaSerializada carga,
        ILogger logger,
        Func<Task> recargar)
        : base(dialogService, session, carga, logger)
    {
        _terminoService = terminoService ?? throw new ArgumentNullException(nameof(terminoService));
        _recargar = recargar ?? throw new ArgumentNullException(nameof(recargar));
        Propuestas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HayPropuestas));
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private Task AceptarAsync(ParPropuestoItemViewModel? propuesta)
    {
        return propuesta is null ? Task.CompletedTask : AsignarAsync([propuesta], aceptar: true);
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private Task DescartarAsync(ParPropuestoItemViewModel? propuesta)
    {
        return propuesta is null ? Task.CompletedTask : AsignarAsync([propuesta], aceptar: false);
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private Task AceptarSeleccionadasAsync()
    {
        return AsignarAsync(Seleccionadas(), aceptar: true);
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private Task DescartarSeleccionadasAsync()
    {
        return AsignarAsync(Seleccionadas(), aceptar: false);
    }

    /// <summary>
    /// Reemplaza las filas por las propuestas del término. Si la categoría propuesta ya no existe, la fila se descarta:
    /// el servidor la recalcula en la próxima carga.
    /// </summary>
    public void Mostrar(IReadOnlyList<CategoriaDto> categorias, IReadOnlyList<ParPropuestoDto> propuestos)
    {
        Reemplazar(Categorias, categorias);

        var filas = new List<ParPropuestoItemViewModel>();
        foreach (var propuesto in propuestos)
        {
            var categoria = Categorias.FirstOrDefault(c => c.IdCategoria == propuesto.IdCategoria);
            if (categoria is not null)
            {
                filas.Add(new ParPropuestoItemViewModel(propuesto, categoria));
            }
        }

        Propuestas.Clear();
        foreach (var fila in filas)
        {
            Propuestas.Add(fila);
        }
    }

    private List<ParPropuestoItemViewModel> Seleccionadas()
    {
        return Propuestas.Where(p => p.Seleccionado).ToList();
    }

    /// <summary>
    /// Acepta (con la categoría elegida) o descarta (par ignorado) las propuestas indicadas, todas en una sola
    /// operación: se guardan todas o ninguna.
    /// </summary>
    private async Task AsignarAsync(List<ParPropuestoItemViewModel> propuestas, bool aceptar)
    {
        if (propuestas.Count == 0)
        {
            return;
        }

        var dto = new AsignarParesDto
        {
            Pares = propuestas
                .Select(p => new AsignarParDto
                {
                    IdTermino = p.Propuesto.IdTermino,
                    SegundaPalabra = p.Propuesto.SegundaPalabra,
                    IdCategoria = aceptar ? p.CategoriaElegida.IdCategoria : null
                })
                .ToList()
        };

        await EscribirAsync(
            aceptar ? "aceptar los pares propuestos" : "descartar los pares propuestos",
            (idActor, token) => _terminoService.AsignarParesAsync(idActor, dto, token),
            _recargar);
    }
}
