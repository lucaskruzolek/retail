using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel del diálogo "Asociar categorías y marcas del catálogo" (Track 6.6, Fase 8, M-80): antes de clasificar el
/// catálogo de un proveedor, ofrece los valores que traen sus ítems y todavía no tienen equivalencia. Cada asociación
/// se guarda al elegirla (<see cref="TraducirValoresPasoViewModel"/>), así que <b>Cancelar aborta la clasificación</b>
/// pero no pierde lo ya asociado, y "Clasificar catálogo" continúa con las reglas vigentes.
/// </summary>
/// <remarks>
/// Asociar escribe un diccionario, así que exige ser Gerente o Encargado (D-5); sin esa autoridad la tabla es de solo
/// lectura y el usuario igual puede clasificar con las reglas de descripción, como en el paso 1 de la incorporación
/// (P6-6). Las categorías y marcas de destino se cargan por una <see cref="CargaSerializada"/>: el diálogo comparte el
/// DbContext de la pantalla del explorador (H-19).
/// </remarks>
public partial class AsociarValoresCatalogoViewModel : ObservableObject, IDisposable
{
    private readonly ICategoriaService _categoriaService;
    private readonly IMarcaService _marcaService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<AsociarValoresCatalogoViewModel> _logger;
    private readonly CargaSerializada _carga;

    private readonly ObservableCollection<CategoriaDto> _categorias = new();
    private readonly ObservableCollection<MarcaDto> _marcas = new();

    /// <summary>La tabla de valores sin asociar; la dibuja <c>AsociarValoresPanel</c>.</summary>
    public TraducirValoresPasoViewModel Paso { get; }

    [ObservableProperty]
    private ProveedorDto? _proveedor;

    /// <summary>Verdadero si el usuario eligió "Clasificar catálogo" (y no canceló).</summary>
    public bool DialogResult { get; private set; }

    /// <summary>Solo el Gerente y el Encargado escriben diccionarios de clasificación (D-5).</summary>
    public bool PuedeAdministrar => _session.EsGerente || _session.EsEncargado;

    public AsociarValoresCatalogoViewModel(
        IEquivalenciaClasificacionService equivalenciaService,
        ICategoriaService categoriaService,
        IMarcaService marcaService,
        ICurrentUserSession session,
        ILogger<AsociarValoresCatalogoViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(equivalenciaService);
        _categoriaService = categoriaService ?? throw new ArgumentNullException(nameof(categoriaService));
        _marcaService = marcaService ?? throw new ArgumentNullException(nameof(marcaService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _carga = new CargaSerializada(_logger);

        Paso = new TraducirValoresPasoViewModel(equivalenciaService, session, logger, _categorias, _marcas);
    }

    /// <summary>Indica el proveedor y los valores a asociar; se invoca antes de mostrar el diálogo.</summary>
    public void Inicializar(ProveedorDto proveedor, IReadOnlyList<ValorSinTraducirDto> valores)
    {
        ArgumentNullException.ThrowIfNull(proveedor);
        ArgumentNullException.ThrowIfNull(valores);

        Proveedor = proveedor;
        DialogResult = false;
        Paso.Cargar(proveedor.IdProveedor, valores, PuedeAdministrar);
    }

    /// <summary>
    /// Carga las categorías y marcas de la tienda que se ofrecen como destino. Fuera del Dispatcher y en la fila de la
    /// pantalla (Ley 4, H-19); un fallo queda como error del paso.
    /// </summary>
    public async Task CargarDestinosAsync()
    {
        await _carga.EjecutarAsync(
            token => Task.Run(
                async () =>
                {
                    // Secuenciales: comparten el DbContext.
                    var categorias = await _categoriaService.ListarAsync(null, null, token);
                    var marcas = await _marcaService.ListarAsync(null, token);
                    return (Categorias: categorias, Marcas: marcas);
                },
                token),
            destinos =>
            {
                Reemplazar(_categorias, destinos.Categorias);
                Reemplazar(_marcas, destinos.Marcas);
            },
            ex =>
            {
                _logger.LogError(ex, "Error al cargar las categorías y marcas de destino: {Mensaje}", ex.Message);
                Paso.MensajeError = "No se pudieron cargar las categorías y marcas de la tienda.";
            });
    }

    private static void Reemplazar<T>(ObservableCollection<T> destino, IEnumerable<T> origen)
    {
        destino.Clear();
        foreach (var elemento in origen)
        {
            destino.Add(elemento);
        }
    }

    [RelayCommand]
    private void Clasificar(Window? window)
    {
        DialogResult = true;
        Cerrar(window, true);
    }

    [RelayCommand]
    private void Cancelar(Window? window)
    {
        DialogResult = false;
        Cerrar(window, false);
    }

    private static void Cerrar(Window? window, bool resultado)
    {
        if (window is null)
        {
            return;
        }

        window.DialogResult = resultado;
        window.Close();
    }

    /// <summary>Cancela las operaciones en curso al cerrar el diálogo.</summary>
    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }
}
