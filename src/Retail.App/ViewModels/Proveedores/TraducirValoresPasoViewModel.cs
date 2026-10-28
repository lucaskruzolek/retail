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
/// Paso 1 del modal de incorporación, "Asociar categorías y marcas" (Track 6.6, P6-3; antes "Traducir valores"): cada valor de la
/// clasificación del proveedor sin equivalencia se traduce a una categoría o marca del comercio, o se ignora (M-13,
/// M-32). Escribe un diccionario, así que exige D-5 (P6-6); sin ella, el paso es de solo lectura.
/// </summary>
public partial class TraducirValoresPasoViewModel : ObservableObject
{
    private readonly IEquivalenciaClasificacionService _equivalenciaService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger _logger;
    private int _idProveedor;

    public ObservableCollection<ValorSinTraducirViewModel> Valores { get; } = new();

    /// <summary>
    /// Categorías y marcas del modal, compartidas para ofrecerlas como destino.
    /// </summary>
    public ObservableCollection<CategoriaDto> Categorias { get; }

    public ObservableCollection<MarcaDto> Marcas { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoloLectura))]
    private bool _puedeAdministrar;

    public bool SoloLectura => !PuedeAdministrar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public TraducirValoresPasoViewModel(
        IEquivalenciaClasificacionService equivalenciaService,
        ICurrentUserSession session,
        ILogger logger,
        ObservableCollection<CategoriaDto> categorias,
        ObservableCollection<MarcaDto> marcas)
    {
        _equivalenciaService = equivalenciaService ?? throw new ArgumentNullException(nameof(equivalenciaService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Categorias = categorias ?? throw new ArgumentNullException(nameof(categorias));
        Marcas = marcas ?? throw new ArgumentNullException(nameof(marcas));
    }

    /// <summary>
    /// Reemplaza la lista por los valores que informó el motor en su última respuesta.
    /// </summary>
    public void Cargar(int idProveedor, IEnumerable<ValorSinTraducirDto> valores, bool puedeAdministrar)
    {
        _idProveedor = idProveedor;
        PuedeAdministrar = puedeAdministrar;
        MensajeError = null;
        Valores.Clear();
        foreach (var valor in valores)
        {
            Valores.Add(new ValorSinTraducirViewModel(valor));
        }
    }

    [RelayCommand]
    private async Task TraducirAsync(ValorSinTraducirViewModel? fila)
    {
        if (fila is null || fila.Resuelto)
        {
            return;
        }

        if (fila.IdDestinoElegido is not { } idDestino)
        {
            MensajeError = fila.EsCategoria
                ? "Elija la categoría a la que corresponde el valor, o ignórelo."
                : "Elija la marca a la que corresponde el valor, o ignórelo.";
            return;
        }

        string destino = fila.EsCategoria ? fila.CategoriaDestino!.NombreCategoria : fila.MarcaDestino!.NombreMarca;
        await AsignarAsync(fila, idDestino, $"Traducido a {destino}");
    }

    [RelayCommand]
    private async Task IgnorarAsync(ValorSinTraducirViewModel? fila)
    {
        if (fila is null || fila.Resuelto)
        {
            return;
        }

        await AsignarAsync(fila, null, "Ignorado");
    }

    private async Task AsignarAsync(ValorSinTraducirViewModel fila, int? idDestino, string resultado)
    {
        if (!PuedeAdministrar)
        {
            return;
        }

        MensajeError = null;
        int idActor = _session.IdUsuario ?? 0;
        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = _idProveedor,
            Tipo = fila.Valor.Tipo,
            TextoRubro = fila.Valor.TextoRubro,
            Texto = fila.Valor.Texto,
            IdDestino = idDestino
        };

        try
        {
            await Task.Run(() => _equivalenciaService.AsignarAsync(idActor, dto));
            fila.Resultado = resultado;
            fila.Resuelto = true;
        }
        catch (ValidationException ex)
        {
            MensajeError = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (DomainException ex)
        {
            // Incluye OperacionNoAutorizadaException (D-5).
            _logger.LogWarning(ex, "Regla de negocio al traducir el valor {Texto}: {Mensaje}", fila.Valor.Texto, ex.Message);
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al traducir el valor {Texto}", fila.Valor.Texto);
            MensajeError = "No se pudo guardar la equivalencia.";
        }
    }
}
