using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Estado del filtro de una columna de fecha (control <c>FiltroFechaColumna</c>): un rango de días, abierto en cualquiera
/// de sus extremos. Igual que <see cref="FiltroColumnaViewModel"/> separa el <b>borrador</b> (lo que se elige con el panel
/// abierto) de lo <b>aplicado</b> (<see cref="DesdeAplicado"/> y <see cref="HastaAplicado"/>), que cambia solo con
/// "Aplicar" y avisa con <see cref="Aplicado"/>. Las fechas son días en hora local, sin hora; el extremo "hasta" es inclusivo.
/// <para>
/// Para consultar a la base, <see cref="RangoUtc"/> las convierte a un intervalo semiabierto en UTC
/// (<c>[desde, hasta)</c>), que se traduce limpio a SQL; para una lista ya cargada en memoria, <see cref="Contiene"/>.
/// </para>
/// <para>
/// Con <see cref="FechaMinima"/> y <see cref="FechaMaxima"/> el calendario no deja elegir fuera de ese intervalo y los atajos
/// se recortan a él (un atajo que cae por completo afuera se deshabilita). Con <c>exigeRangoCompleto</c> (los reportes del
/// tablero) "Aplicar" no se habilita hasta elegir los dos extremos.
/// </para>
/// </summary>
public partial class FiltroFechaColumnaViewModel : ObservableObject, IFiltroEmergente
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    private readonly TimeProvider _reloj;
    private readonly bool _exigeRangoCompleto;

    [ObservableProperty]
    private bool _estaAbierto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBorrador))]
    [NotifyCanExecuteChangedFor(nameof(AplicarCommand))]
    private DateTime? _borradorDesde;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoBorrador))]
    [NotifyCanExecuteChangedFor(nameof(AplicarCommand))]
    private DateTime? _borradorHasta;

    /// <summary>Primer día elegible; <c>null</c> = sin límite.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ElegirAtajoCommand))]
    private DateTime? _fechaMinima;

    /// <summary>Último día elegible; <c>null</c> = sin límite.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ElegirAtajoCommand))]
    private DateTime? _fechaMaxima;

    /// <summary>Mes que muestra el calendario izquierdo ("desde").</summary>
    [ObservableProperty]
    private DateTime _mesDesde;

    /// <summary>Mes que muestra el calendario derecho ("hasta").</summary>
    [ObservableProperty]
    private DateTime _mesHasta;

    public FiltroFechaColumnaViewModel(string titulo, TimeProvider? reloj = null, bool exigeRangoCompleto = false)
    {
        Titulo = titulo ?? throw new ArgumentNullException(nameof(titulo));
        _reloj = reloj ?? TimeProvider.System;
        _exigeRangoCompleto = exigeRangoCompleto;
        MesDesde = MesHasta = Hoy;
    }

    /// <summary>Lo aplicado cambió: la pantalla tiene que volver a consultar o a filtrar.</summary>
    public event EventHandler? Aplicado;

    public string Titulo { get; }

    public IReadOnlyList<OpcionAtajoFecha> Atajos { get; } =
    [
        new(AtajoFecha.Hoy, "Hoy"),
        new(AtajoFecha.Ayer, "Ayer"),
        new(AtajoFecha.EstaSemana, "Esta semana"),
        new(AtajoFecha.SemanaPasada, "Semana pasada"),
        new(AtajoFecha.EsteMes, "Este mes"),
        new(AtajoFecha.MesPasado, "Mes pasado"),
        new(AtajoFecha.EsteAnio, "Este año"),
        new(AtajoFecha.AnioPasado, "Año pasado")
    ];

    public DateTime? DesdeAplicado { get; private set; }

    public DateTime? HastaAplicado { get; private set; }

    public bool HayFiltroActivo => DesdeAplicado is not null || HastaAplicado is not null;

    /// <summary>El rango elegido en el panel, para leerlo de un vistazo ("10/01/2025 – 16/01/2025").</summary>
    public string TextoBorrador => Describir(BorradorDesde, BorradorHasta);

    /// <summary>El rango aplicado, para el tooltip del botón de la cabecera.</summary>
    public string TextoAplicado => Describir(DesdeAplicado, HastaAplicado);

    private DateTime Hoy => TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), _reloj.LocalTimeZone).Date;

    /// <summary>
    /// Intervalo semiabierto en UTC equivalente al rango aplicado: desde el inicio del primer día local, hasta el inicio
    /// del día siguiente al último. Un extremo sin elegir queda <c>null</c> (sin tope).
    /// </summary>
    public (DateTime? DesdeUtc, DateTime? HastaUtcExclusivo) RangoUtc()
    {
        var zona = _reloj.LocalTimeZone;
        DateTime? desde = DesdeAplicado is { } d
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified), zona)
            : null;
        DateTime? hasta = HastaAplicado is { } h
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(h.Date.AddDays(1), DateTimeKind.Unspecified), zona)
            : null;

        return (desde, hasta);
    }

    /// <summary>Si una fecha local cae dentro del rango aplicado (sin filtro, siempre).</summary>
    public bool Contiene(DateTime fechaLocal)
    {
        var dia = fechaLocal.Date;

        return (DesdeAplicado is null || dia >= DesdeAplicado.Value.Date)
            && (HastaAplicado is null || dia <= HastaAplicado.Value.Date);
    }

    /// <summary>Vuelve a "sin filtro" sin avisar: lo usa "Limpiar filtros" de la pantalla, que consulta una sola vez.</summary>
    public void QuitarFiltro()
    {
        EstablecerRango(null, null);
    }

    /// <summary>
    /// Fija el rango aplicado desde afuera (el valor inicial de una pantalla, o "Limpiar filtros") sin avisar con
    /// <see cref="Aplicado"/>: quien lo llama ya sabe que cambió y consulta una sola vez.
    /// </summary>
    public void EstablecerRango(DateTime? desde, DateTime? hasta)
    {
        DesdeAplicado = desde?.Date;
        HastaAplicado = hasta?.Date;
        RestaurarBorrador();
        NotificarAplicado();
    }

    /// <summary>Recorta un día elegible a <see cref="FechaMinima"/> y <see cref="FechaMaxima"/>.</summary>
    public DateTime Acotar(DateTime fecha)
    {
        var dia = fecha.Date;
        if (FechaMinima is { } minima && dia < minima.Date)
        {
            dia = minima.Date;
        }

        if (FechaMaxima is { } maxima && dia > maxima.Date)
        {
            dia = maxima.Date;
        }

        return dia;
    }

    [RelayCommand(CanExecute = nameof(PuedeElegirAtajo))]
    private void ElegirAtajo(AtajoFecha atajo)
    {
        var (desde, hasta) = RangoAcotado(atajo);
        BorradorDesde = desde;
        BorradorHasta = hasta;
        MesDesde = desde;
        MesHasta = hasta;
    }

    // Un atajo que cae por completo fuera de los límites (el mes pasado, si todavía no hay ventas de entonces) no se ofrece
    private bool PuedeElegirAtajo(AtajoFecha atajo)
    {
        var (desde, hasta) = Rango(atajo, Hoy);

        return (FechaMinima is null || hasta >= FechaMinima.Value.Date) && (FechaMaxima is null || desde <= FechaMaxima.Value.Date);
    }

    private (DateTime Desde, DateTime Hasta) RangoAcotado(AtajoFecha atajo)
    {
        var (desde, hasta) = Rango(atajo, Hoy);

        return (Acotar(desde), Acotar(hasta));
    }

    // Con rango completo exigido, "Aplicar" espera a que estén los dos extremos
    private bool PuedeAplicar()
    {
        return !_exigeRangoCompleto || (BorradorDesde is not null && BorradorHasta is not null);
    }

    [RelayCommand(CanExecute = nameof(PuedeAplicar))]
    private void Aplicar()
    {
        var desde = BorradorDesde?.Date;
        var hasta = BorradorHasta?.Date;

        // Un rango al revés se interpreta como el usuario lo pensó: del menor al mayor
        if (desde is not null && hasta is not null && desde > hasta)
        {
            (desde, hasta) = (hasta, desde);
        }

        bool cambio = desde != DesdeAplicado || hasta != HastaAplicado;
        DesdeAplicado = desde;
        HastaAplicado = hasta;
        NotificarAplicado();
        EstaAbierto = false;

        if (cambio)
        {
            Aplicado?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        EstaAbierto = false;
    }

    /// <summary>Borra el borrador; hace falta "Aplicar" para quitar el filtro.</summary>
    [RelayCommand]
    private void Limpiar()
    {
        BorradorDesde = null;
        BorradorHasta = null;
    }

    partial void OnEstaAbiertoChanged(bool value)
    {
        // Al abrir se parte siempre de lo aplicado: un borrador cancelado no reaparece
        if (value)
        {
            RestaurarBorrador();
        }
    }

    private void RestaurarBorrador()
    {
        BorradorDesde = DesdeAplicado;
        BorradorHasta = HastaAplicado;
        MesDesde = DesdeAplicado ?? Hoy;
        MesHasta = HastaAplicado ?? Hoy;
    }

    private void NotificarAplicado()
    {
        OnPropertyChanged(nameof(DesdeAplicado));
        OnPropertyChanged(nameof(HastaAplicado));
        OnPropertyChanged(nameof(HayFiltroActivo));
        OnPropertyChanged(nameof(TextoAplicado));
    }

    private static string Describir(DateTime? desde, DateTime? hasta)
    {
        string Formato(DateTime fecha) => fecha.ToString("dd/MM/yyyy", Cultura);

        return (desde, hasta) switch
        {
            (null, null) => "Sin rango",
            ({ } d, null) => $"Desde {Formato(d)}",
            (null, { } h) => $"Hasta {Formato(h)}",
            ({ } d, { } h) => $"{Formato(d)} – {Formato(h)}"
        };
    }

    // La semana empieza el lunes (uso argentino)
    private static (DateTime Desde, DateTime Hasta) Rango(AtajoFecha atajo, DateTime hoy)
    {
        int diasDesdeLunes = ((int)hoy.DayOfWeek + 6) % 7;
        var lunes = hoy.AddDays(-diasDesdeLunes);
        var primeroDelMes = new DateTime(hoy.Year, hoy.Month, 1);

        return atajo switch
        {
            AtajoFecha.Hoy => (hoy, hoy),
            AtajoFecha.Ayer => (hoy.AddDays(-1), hoy.AddDays(-1)),
            AtajoFecha.EstaSemana => (lunes, lunes.AddDays(6)),
            AtajoFecha.SemanaPasada => (lunes.AddDays(-7), lunes.AddDays(-1)),
            AtajoFecha.EsteMes => (primeroDelMes, primeroDelMes.AddMonths(1).AddDays(-1)),
            AtajoFecha.MesPasado => (primeroDelMes.AddMonths(-1), primeroDelMes.AddDays(-1)),
            AtajoFecha.EsteAnio => (new DateTime(hoy.Year, 1, 1), new DateTime(hoy.Year, 12, 31)),
            AtajoFecha.AnioPasado => (new DateTime(hoy.Year - 1, 1, 1), new DateTime(hoy.Year - 1, 12, 31)),
            _ => throw new ArgumentOutOfRangeException(nameof(atajo), atajo, "Atajo de fecha desconocido.")
        };
    }
}
