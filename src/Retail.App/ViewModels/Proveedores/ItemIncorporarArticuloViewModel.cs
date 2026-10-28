using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Converters;
using Retail.App.Services;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Entities;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Modelo de fila para la grilla del modal de incorporación de artículos a tienda.
/// Contiene datos del ítem mayorista, margen de ganancia individual editable y recálculo reactivo del precio de venta final.
/// Con las sugerencias activas (Track 6.6) lleva además la categoría y la marca elegidas y lo que sugirió el sistema.
/// </summary>
public partial class ItemIncorporarArticuloViewModel : ObservableObject
{
    // Listas de opciones del modal (empiezan por "Sin clasificar"; la de marcas sigue con "Sin marca"). Se comparten por
    // referencia: así el comando "volver a la sugerencia" encuentra también las categorías creadas durante el recorrido.
    private readonly IReadOnlyCollection<OpcionDiccionario> _opcionesCategoria;
    private readonly IReadOnlyCollection<OpcionDiccionario> _opcionesMarca;

    public int IdCatalogo { get; }
    public string CodigoProveedor { get; }
    public string? CodigoBarras { get; }
    public string Descripcion { get; }
    public decimal CostoReposicion { get; }

    /// <summary>
    /// Línea de códigos bajo la descripción: el del proveedor y, si lo tiene, el de barras.
    /// </summary>
    public string CodigosTexto => string.IsNullOrWhiteSpace(CodigoBarras)
        ? CodigoProveedor
        : $"{CodigoProveedor} · {CodigoBarras}";

    [ObservableProperty]
    private decimal _porcentajeGanancia;

    [ObservableProperty]
    private decimal _precioVenta;

    /// <summary>
    /// Categoría elegida para el ítem; la sugerencia precarga este valor y la persona puede corregirlo. Empieza en
    /// "Sin clasificar" (M-70, C1): una categoría vacía siempre queda pendiente, así que no hay opción "Sin categoría".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Estado), nameof(EstadoTexto), nameof(SinCategoria), nameof(PuedeVolverALaSugerencia))]
    [NotifyCanExecuteChangedFor(nameof(VolverALaSugerenciaCommand))]
    private OpcionDiccionario? _categoriaSeleccionada = OpcionDiccionario.SinClasificar;

    /// <summary>
    /// Marca elegida: "Sin clasificar" (la inicial) es "sin decidir"; "Sin marca" es la confirmación de que el producto
    /// no tiene marca (M-7). Las dos tienen Id nulo y se distinguen por la opción.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Estado), nameof(EstadoTexto), nameof(SinMarca), nameof(PuedeVolverALaSugerencia))]
    [NotifyCanExecuteChangedFor(nameof(VolverALaSugerenciaCommand))]
    private OpcionDiccionario? _marcaSeleccionada = OpcionDiccionario.SinClasificar;

    /// <summary>
    /// Categoría que sugirió el motor y su motivo; nulos si no sugirió ninguna. La UI los reenvía tal cual al servidor,
    /// que deriva el origen del artículo (M-34).
    /// </summary>
    public int? IdCategoriaSugerida { get; private set; }

    public MotivoSugerenciaEnum? MotivoCategoria { get; private set; }

    public int? IdMarcaSugerida { get; private set; }

    public MotivoSugerenciaEnum? MotivoMarca { get; private set; }

    public bool SinCategoria => CategoriaSeleccionada is not { Id: not null };

    /// <summary>
    /// La persona confirmó que el producto no tiene marca (M-7): eligió la opción "Sin marca".
    /// </summary>
    public bool SinMarca => MarcaSeleccionada == OpcionDiccionario.SinMarca;

    public string? MotivoCategoriaTexto => MotivoCategoria is { } motivo ? EnumATextoConverter.ATexto(motivo) : null;

    public string? MotivoMarcaTexto => MotivoMarca is { } motivo ? EnumATextoConverter.ATexto(motivo) : null;

    public bool TieneMotivoCategoria => MotivoCategoria is not null;

    public bool TieneMotivoMarca => MotivoMarca is not null;

    public EstadoSugerenciaItemEnum Estado
    {
        get
        {
            int? idCategoria = CategoriaSeleccionada?.Id;
            int? idMarca = MarcaSeleccionada?.Id;

            bool marcaSinDecidir = MarcaSeleccionada is null || MarcaSeleccionada == OpcionDiccionario.SinClasificar;
            bool sinNadaElegido = idCategoria is null && marcaSinDecidir;
            if (sinNadaElegido && IdCategoriaSugerida is null && IdMarcaSugerida is null)
            {
                return EstadoSugerenciaItemEnum.SinSugerencia;
            }

            if (idCategoria == IdCategoriaSugerida && idMarca == IdMarcaSugerida && !SinMarca)
            {
                return EstadoSugerenciaItemEnum.Sugerida;
            }

            if (SinMarca && idCategoria == IdCategoriaSugerida && IdMarcaSugerida is null)
            {
                return EstadoSugerenciaItemEnum.SinMarca;
            }

            return EstadoSugerenciaItemEnum.Corregida;
        }
    }

    public string EstadoTexto => Estado switch
    {
        EstadoSugerenciaItemEnum.Sugerida => "Sugerida",
        EstadoSugerenciaItemEnum.Corregida => "Corregida",
        EstadoSugerenciaItemEnum.SinMarca => "Sin marca",
        _ => "Sin sugerencia"
    };

    /// <summary>
    /// Solo una fila corregida tiene algo que deshacer.
    /// </summary>
    public bool PuedeVolverALaSugerencia => Estado == EstadoSugerenciaItemEnum.Corregida;

    public ItemIncorporarArticuloViewModel(
        int idCatalogo,
        string codigoProveedor,
        string? codigoBarras,
        string descripcion,
        decimal costoReposicion,
        decimal porcentajeGananciaInicial,
        IReadOnlyCollection<OpcionDiccionario> opcionesCategoria,
        IReadOnlyCollection<OpcionDiccionario> opcionesMarca)
    {
        IdCatalogo = idCatalogo;
        CodigoProveedor = codigoProveedor;
        CodigoBarras = codigoBarras;
        Descripcion = descripcion;
        CostoReposicion = costoReposicion;
        _porcentajeGanancia = porcentajeGananciaInicial;
        _precioVenta = Articulo.CalcularPrecioVenta(costoReposicion, porcentajeGananciaInicial);
        _opcionesCategoria = opcionesCategoria ?? throw new ArgumentNullException(nameof(opcionesCategoria));
        _opcionesMarca = opcionesMarca ?? throw new ArgumentNullException(nameof(opcionesMarca));
    }

    partial void OnPorcentajeGananciaChanged(decimal value)
    {
        PrecioVenta = Articulo.CalcularPrecioVenta(CostoReposicion, value);
    }

    /// <summary>
    /// Registra lo que sugirió el motor y, salvo que la persona ya lo haya corregido, lo precarga en las selecciones
    /// (RF-27: la sugerencia solo se aplica cuando la persona confirma el diálogo). Si la categoría o la marca
    /// sugeridas no están en las listas del modal, la sugerencia se registra pero no se precarga.
    /// </summary>
    public void AplicarSugerencia(SugerenciaItemDto? sugerencia)
    {
        bool seguiaLaSugerencia = Estado is EstadoSugerenciaItemEnum.Sugerida or EstadoSugerenciaItemEnum.SinSugerencia;

        IdCategoriaSugerida = sugerencia?.Categoria?.Id;
        MotivoCategoria = sugerencia?.Categoria?.Motivo;
        IdMarcaSugerida = sugerencia?.Marca?.Id;
        MotivoMarca = sugerencia?.Marca?.Motivo;
        OnPropertyChanged(nameof(MotivoCategoriaTexto));
        OnPropertyChanged(nameof(MotivoMarcaTexto));
        OnPropertyChanged(nameof(TieneMotivoCategoria));
        OnPropertyChanged(nameof(TieneMotivoMarca));

        if (seguiaLaSugerencia)
        {
            AceptarSugerencia();
        }
        else
        {
            NotificarEstado();
        }
    }

    /// <summary>
    /// Vuelve a lo que sugirió el motor, descartando la corrección de la persona (acciones "Restablecer" y "Volver a la
    /// sugerencia"). Sin sugerencia de categoría o de marca, esa selección vuelve a "Sin clasificar".
    /// </summary>
    public void AceptarSugerencia()
    {
        CategoriaSeleccionada = _opcionesCategoria.FirstOrDefault(o => o.Id is not null && o.Id == IdCategoriaSugerida)
            ?? OpcionDiccionario.SinClasificar;
        MarcaSeleccionada = _opcionesMarca.FirstOrDefault(o => o.Id is not null && o.Id == IdMarcaSugerida)
            ?? OpcionDiccionario.SinClasificar;
        NotificarEstado();
    }

    [RelayCommand(CanExecute = nameof(PuedeVolverALaSugerencia))]
    private void VolverALaSugerencia()
    {
        AceptarSugerencia();
    }

    private void NotificarEstado()
    {
        OnPropertyChanged(nameof(Estado));
        OnPropertyChanged(nameof(EstadoTexto));
        OnPropertyChanged(nameof(PuedeVolverALaSugerencia));
        VolverALaSugerenciaCommand.NotifyCanExecuteChanged();
    }
}
