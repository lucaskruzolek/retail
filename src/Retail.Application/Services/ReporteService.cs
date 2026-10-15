using FluentValidation;
using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Reportes.Consultas;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services.Reportes;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Reportes gerenciales (RF-24, RF-25). Autoriza al Gerente (D-11), valida el filtro, convierte los días locales a un
/// rango UTC (D-9), consulta el puerto de lectura y redondea con el criterio único (<see cref="ReglasMonetarias"/>).
/// </summary>
/// <remarks>
/// Las consultas al puerto se hacen EN SECUENCIA (<c>await</c> una por una) y nunca con <c>Task.WhenAll</c>: todas
/// comparten el mismo <c>DbContext</c> y EF Core no admite dos operaciones simultáneas sobre una misma instancia
/// (Ley 8 de SISTEMA_DE_PERSISTENCIA).
/// </remarks>
public class ReporteService : IReporteService
{
    private const int MaximoDiasEvolucionDiaria = 62;
    private const int MaximoDiasEvolucionSemanal = 366;

    private readonly IReporteQueryService _consultas;
    private readonly IRepository<Usuario> _usuarios;
    private readonly TimeProvider _reloj;
    private readonly IValidator<FiltroPeriodoDto> _validadorPeriodo;
    private readonly IValidator<FiltroVentasPorPeriodoDto> _validadorVentas;
    private readonly IValidator<FiltroRankingDto> _validadorRanking;
    private readonly IValidator<FiltroRentabilidadDto> _validadorRentabilidad;
    private readonly IValidator<FiltroInventarioDto> _validadorInventario;
    private readonly IValidator<FiltroSinMovimientoDto> _validadorSinMovimiento;

    public ReporteService(
        IReporteQueryService consultas,
        IRepository<Usuario> usuarios,
        TimeProvider reloj,
        IValidator<FiltroPeriodoDto> validadorPeriodo,
        IValidator<FiltroVentasPorPeriodoDto> validadorVentas,
        IValidator<FiltroRankingDto> validadorRanking,
        IValidator<FiltroRentabilidadDto> validadorRentabilidad,
        IValidator<FiltroInventarioDto> validadorInventario,
        IValidator<FiltroSinMovimientoDto> validadorSinMovimiento)
    {
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _validadorPeriodo = validadorPeriodo ?? throw new ArgumentNullException(nameof(validadorPeriodo));
        _validadorVentas = validadorVentas ?? throw new ArgumentNullException(nameof(validadorVentas));
        _validadorRanking = validadorRanking ?? throw new ArgumentNullException(nameof(validadorRanking));
        _validadorRentabilidad = validadorRentabilidad ?? throw new ArgumentNullException(nameof(validadorRentabilidad));
        _validadorInventario = validadorInventario ?? throw new ArgumentNullException(nameof(validadorInventario));
        _validadorSinMovimiento = validadorSinMovimiento ?? throw new ArgumentNullException(nameof(validadorSinMovimiento));
    }

    public async Task<TableroDto> ObtenerTableroAsync(
        int idActor,
        FiltroPeriodoDto filtro,
        CancellationToken cancellationToken = default)
    {
        await AutorizarGerenteAsync(idActor, cancellationToken);
        await _validadorPeriodo.ValidateAndThrowAsync(filtro, cancellationToken);

        var dias = filtro.Hasta.DayNumber - filtro.Desde.DayNumber + 1;
        var hastaAnterior = filtro.Desde.AddDays(-1);
        var desdeAnterior = filtro.Desde.AddDays(-dias);
        var agrupacion = ElegirAgrupacionDeEvolucion(dias);

        var rango = CrearRango(filtro.Desde, filtro.Hasta);
        var rangoAnterior = CrearRango(desdeAnterior, hastaAnterior);

        // En secuencia: comparten el DbContext (ver el comentario de la clase).
        var actual = await _consultas.ObtenerResumenVentasAsync(rango, cancellationToken);
        var anterior = await _consultas.ObtenerResumenVentasAsync(rangoAnterior, cancellationToken);
        var evolucion = await _consultas.ObtenerVentasPorPeriodoAsync(rango, agrupacion, null, cancellationToken);
        var porCategoria = await _consultas.ObtenerRentabilidadAsync(
            rango,
            new FiltroRentabilidadDto
            {
                Desde = filtro.Desde,
                Hasta = filtro.Hasta,
                AgruparPor = AgrupacionRentabilidadEnum.Categoria
            },
            cancellationToken);
        var inventario = await _consultas.ObtenerResumenInventarioAsync(new FiltroInventarioDto(), cancellationToken);

        var totalActual = ReglasMonetarias.Redondear(actual.TotalVendido);
        var totalAnterior = ReglasMonetarias.Redondear(anterior.TotalVendido);
        var margenActual = totalActual - ReglasMonetarias.Redondear(actual.CostoTotal);
        var margenAnterior = totalAnterior - ReglasMonetarias.Redondear(anterior.CostoTotal);
        var porcentajeActual = CalcularPorcentaje(margenActual, totalActual);
        var porcentajeAnterior = CalcularPorcentaje(margenAnterior, totalAnterior);

        return new TableroDto
        {
            GeneradoEl = _reloj.AhoraLocal(),
            Filtro = filtro,
            DesdeAnterior = desdeAnterior,
            HastaAnterior = hastaAnterior,
            TotalVendido = CrearIndicador(totalActual, totalAnterior),
            CantidadVentas = CrearIndicador(actual.CantidadVentas, anterior.CantidadVentas),
            MargenBruto = CrearIndicador(margenActual, margenAnterior),
            MargenPorcentual = porcentajeActual,
            MargenPorcentualAnterior = porcentajeAnterior,
            VariacionMargenPuntos = porcentajeActual is null || porcentajeAnterior is null
                ? null
                : porcentajeActual.Value - porcentajeAnterior.Value,
            ArticulosBajoStockMinimo = inventario.ArticulosBajoMinimo,
            ValorizacionInventarioCosto = ReglasMonetarias.Redondear(inventario.ValorizacionCosto),
            EvolucionVentas = CompletarPeriodos(evolucion, filtro.Desde, filtro.Hasta, agrupacion),
            AgrupacionEvolucion = agrupacion,
            MargenPorCategoria = MapearRentabilidad(porCategoria, AgrupacionRentabilidadEnum.Categoria)
        };
    }

    public async Task<VentasPorPeriodoDto> ObtenerVentasPorPeriodoAsync(
        int idActor,
        FiltroVentasPorPeriodoDto filtro,
        CancellationToken cancellationToken = default)
    {
        await AutorizarGerenteAsync(idActor, cancellationToken);
        await _validadorVentas.ValidateAndThrowAsync(filtro, cancellationToken);

        var rango = CrearRango(filtro.Desde, filtro.Hasta);
        var filas = await _consultas.ObtenerVentasPorPeriodoAsync(rango, filtro.Agrupacion, filtro.IdUsuario, cancellationToken);

        return new VentasPorPeriodoDto
        {
            GeneradoEl = _reloj.AhoraLocal(),
            Filtro = filtro,
            Puntos = CompletarPeriodos(filas, filtro.Desde, filtro.Hasta, filtro.Agrupacion),
            TotalVendido = ReglasMonetarias.Redondear(filas.Sum(f => f.TotalVendido)),
            CantidadVentas = filas.Sum(f => f.CantidadVentas),
            Descuentos = ReglasMonetarias.Redondear(filas.Sum(f => f.Descuentos))
        };
    }

    public async Task<RankingArticulosDto> ObtenerRankingAsync(
        int idActor,
        FiltroRankingDto filtro,
        CancellationToken cancellationToken = default)
    {
        await AutorizarGerenteAsync(idActor, cancellationToken);
        await _validadorRanking.ValidateAndThrowAsync(filtro, cancellationToken);

        var rango = CrearRango(filtro.Desde, filtro.Hasta);

        // En secuencia: comparten el DbContext (ver el comentario de la clase).
        var masVendidos = await _consultas.ObtenerMasVendidosAsync(rango, filtro, cancellationToken);
        var menosVendidos = await _consultas.ObtenerMenosVendidosAsync(rango, filtro, cancellationToken);

        return new RankingArticulosDto
        {
            GeneradoEl = _reloj.AhoraLocal(),
            Filtro = filtro,
            MasVendidos = masVendidos.Select(MapearRanking).ToList(),
            MenosVendidos = menosVendidos.Select(MapearRanking).ToList()
        };
    }

    public async Task<RentabilidadDto> ObtenerRentabilidadAsync(
        int idActor,
        FiltroRentabilidadDto filtro,
        CancellationToken cancellationToken = default)
    {
        await AutorizarGerenteAsync(idActor, cancellationToken);
        await _validadorRentabilidad.ValidateAndThrowAsync(filtro, cancellationToken);

        var rango = CrearRango(filtro.Desde, filtro.Hasta);
        var filas = await _consultas.ObtenerRentabilidadAsync(rango, filtro, cancellationToken);

        // El total suma las filas crudas y recién después redondea: sumar importes ya redondeados acumularía error.
        var total = new RentabilidadFila(
            null,
            "Total",
            filas.Sum(f => f.Unidades),
            filas.Sum(f => f.IngresoNeto),
            filas.Sum(f => f.Costo));

        return new RentabilidadDto
        {
            GeneradoEl = _reloj.AhoraLocal(),
            Filtro = filtro,
            Filas = MapearRentabilidad(filas, filtro.AgruparPor),
            Total = MapearFilaRentabilidad(total, filtro.AgruparPor)
        };
    }

    public async Task<StockCriticoDto> ObtenerStockCriticoAsync(
        int idActor,
        FiltroInventarioDto filtro,
        CancellationToken cancellationToken = default)
    {
        await AutorizarGerenteAsync(idActor, cancellationToken);
        await _validadorInventario.ValidateAndThrowAsync(filtro, cancellationToken);

        // En secuencia: comparten el DbContext (ver el comentario de la clase).
        var resumen = await _consultas.ObtenerResumenInventarioAsync(filtro, cancellationToken);
        var filas = await _consultas.ObtenerStockCriticoAsync(filtro, cancellationToken);

        return new StockCriticoDto
        {
            GeneradoEl = _reloj.AhoraLocal(),
            Filtro = filtro,
            Filas = filas
                .Select(f => new StockCriticoFilaDto
                {
                    IdArticulo = f.IdArticulo,
                    Descripcion = f.Descripcion,
                    CodigoBarras = f.CodigoBarras,
                    Categoria = f.Categoria,
                    Marca = f.Marca,
                    StockActual = f.StockActual,
                    StockMinimo = f.StockMinimo,
                    Faltante = f.StockMinimo - f.StockActual,
                    CostoReposicion = ReglasMonetarias.Redondear(f.CostoReposicion),
                    PrecioVenta = ReglasMonetarias.Redondear(f.PrecioVenta)
                })
                .OrderByDescending(f => f.Faltante)
                .ThenBy(f => f.Descripcion)
                .ToList(),
            ArticulosConsiderados = resumen.ArticulosConsiderados,
            ValorizacionCosto = ReglasMonetarias.Redondear(resumen.ValorizacionCosto),
            ValorizacionVenta = ReglasMonetarias.Redondear(resumen.ValorizacionVenta)
        };
    }

    public async Task<SinMovimientoDto> ObtenerSinMovimientoAsync(
        int idActor,
        FiltroSinMovimientoDto filtro,
        CancellationToken cancellationToken = default)
    {
        await AutorizarGerenteAsync(idActor, cancellationToken);
        await _validadorSinMovimiento.ValidateAndThrowAsync(filtro, cancellationToken);

        var zona = _reloj.LocalTimeZone;
        var sinVentasDesde = _reloj.HoyLocal().AddDays(-filtro.Dias);
        var sinVentasDesdeUtc = ConvertirInicioDeDiaAUtc(sinVentasDesde, zona);

        var filas = await _consultas.ObtenerSinMovimientoAsync(filtro, sinVentasDesdeUtc, cancellationToken);

        return new SinMovimientoDto
        {
            GeneradoEl = _reloj.AhoraLocal(),
            Filtro = filtro,
            SinVentasDesde = sinVentasDesde,
            Filas = filas
                .Select(f => new ArticuloSinMovimientoDto
                {
                    IdArticulo = f.IdArticulo,
                    Descripcion = f.Descripcion,
                    CodigoBarras = f.CodigoBarras,
                    Categoria = f.Categoria,
                    Marca = f.Marca,
                    StockActual = f.StockActual,
                    CostoReposicion = ReglasMonetarias.Redondear(f.CostoReposicion),
                    UltimaVenta = f.UltimaVentaUtc is null
                        ? null
                        : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(f.UltimaVentaUtc.Value, DateTimeKind.Utc), zona),
                    CapitalInmovilizado = ReglasMonetarias.Redondear(f.StockActual * f.CostoReposicion)
                })
                .OrderByDescending(f => f.CapitalInmovilizado)
                .ThenBy(f => f.Descripcion)
                .ToList(),
            CapitalInmovilizadoTotal = ReglasMonetarias.Redondear(filas.Sum(f => f.StockActual * f.CostoReposicion))
        };
    }

    private async Task AutorizarGerenteAsync(int idActor, CancellationToken cancellationToken)
    {
        var actor = idActor > 0
            ? await _usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        if (actor is null)
        {
            throw new OperacionNoAutorizadaException("La operación fue solicitada por un usuario inexistente o dado de baja.");
        }

        if (!actor.EsGerente())
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente puede consultar los reportes gerenciales.");
        }
    }

    /// <summary>
    /// Convierte los días locales a UTC (D-9): <c>[desde 00:00, hasta + 1 día 00:00)</c>.
    /// </summary>
    /// <remarks>
    /// Supuesto (R-2): la zona no cambia de horario dentro del rango, por eso un único desfase alcanza para agrupar.
    /// Argentina no aplica horario de verano desde 2009. Si volviera a aplicarlo, hay que pasar a <c>AT TIME ZONE</c>.
    /// </remarks>
    private RangoUtcDto CrearRango(DateOnly desde, DateOnly hasta)
    {
        var zona = _reloj.LocalTimeZone;
        var desdeUtc = ConvertirInicioDeDiaAUtc(desde, zona);
        var hastaExclusivoUtc = ConvertirInicioDeDiaAUtc(hasta.AddDays(1), zona);
        var desfaseMinutos = (int)zona.GetUtcOffset(desdeUtc).TotalMinutes;

        return new RangoUtcDto(desdeUtc, hastaExclusivoUtc, desfaseMinutos);
    }

    private static DateTime ConvertirInicioDeDiaAUtc(DateOnly dia, TimeZoneInfo zona)
    {
        var inicioLocal = DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(inicioLocal, zona);
    }

    private static AgrupacionTemporalEnum ElegirAgrupacionDeEvolucion(int dias)
    {
        if (dias <= MaximoDiasEvolucionDiaria)
        {
            return AgrupacionTemporalEnum.Dia;
        }

        return dias <= MaximoDiasEvolucionSemanal ? AgrupacionTemporalEnum.Semana : AgrupacionTemporalEnum.Mes;
    }

    private static IndicadorDto CrearIndicador(decimal valor, decimal anterior)
    {
        return new IndicadorDto
        {
            Valor = valor,
            ValorAnterior = anterior,
            VariacionPorcentual = anterior == 0
                ? null
                : ReglasMonetarias.Redondear((valor - anterior) / anterior * 100m)
        };
    }

    private static decimal? CalcularPorcentaje(decimal parte, decimal total)
    {
        return total == 0 ? null : ReglasMonetarias.Redondear(parte / total * 100m);
    }

    /// <summary>
    /// Completa con ceros los períodos sin ventas para que el gráfico no salte días. Trabaja en memoria sobre a lo sumo
    /// un punto por período: no materializa tablas, son agregados que SQL ya calculó.
    /// </summary>
    private static List<PuntoVentasDto> CompletarPeriodos(
        IReadOnlyList<VentasPeriodoFila> filas,
        DateOnly desde,
        DateOnly hasta,
        AgrupacionTemporalEnum agrupacion)
    {
        var porInicio = filas.ToDictionary(f => f.InicioPeriodo);
        var puntos = new List<PuntoVentasDto>();

        for (var inicio = InicioDelPeriodo(desde, agrupacion); inicio <= hasta; inicio = SiguientePeriodo(inicio, agrupacion))
        {
            porInicio.TryGetValue(inicio, out var fila);
            puntos.Add(new PuntoVentasDto
            {
                InicioPeriodo = inicio,
                CantidadVentas = fila?.CantidadVentas ?? 0,
                TotalVendido = ReglasMonetarias.Redondear(fila?.TotalVendido ?? 0m),
                Descuentos = ReglasMonetarias.Redondear(fila?.Descuentos ?? 0m)
            });
        }

        return puntos;
    }

    // La semana empieza el lunes (D-10). DayOfWeek: domingo = 0, lunes = 1.
    private static DateOnly InicioDelPeriodo(DateOnly fecha, AgrupacionTemporalEnum agrupacion)
    {
        return agrupacion switch
        {
            AgrupacionTemporalEnum.Semana => fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7)),
            AgrupacionTemporalEnum.Mes => new DateOnly(fecha.Year, fecha.Month, 1),
            _ => fecha
        };
    }

    private static DateOnly SiguientePeriodo(DateOnly inicio, AgrupacionTemporalEnum agrupacion)
    {
        return agrupacion switch
        {
            AgrupacionTemporalEnum.Semana => inicio.AddDays(7),
            AgrupacionTemporalEnum.Mes => inicio.AddMonths(1),
            _ => inicio.AddDays(1)
        };
    }

    private static RankingArticuloDto MapearRanking(RankingFila fila)
    {
        return new RankingArticuloDto
        {
            IdArticulo = fila.IdArticulo,
            Descripcion = fila.Descripcion,
            CodigoBarras = fila.CodigoBarras,
            Categoria = fila.Categoria,
            Marca = fila.Marca,
            Unidades = fila.Unidades,
            IngresoNeto = ReglasMonetarias.Redondear(fila.IngresoNeto)
        };
    }

    private static List<RentabilidadFilaDto> MapearRentabilidad(
        IReadOnlyList<RentabilidadFila> filas,
        AgrupacionRentabilidadEnum agruparPor)
    {
        return filas
            .Select(f => MapearFilaRentabilidad(f, agruparPor))
            .OrderByDescending(f => f.MargenBruto)
            .ThenBy(f => f.Grupo)
            .ToList();
    }

    private static RentabilidadFilaDto MapearFilaRentabilidad(RentabilidadFila fila, AgrupacionRentabilidadEnum agruparPor)
    {
        var ingreso = ReglasMonetarias.Redondear(fila.IngresoNeto);
        var costo = ReglasMonetarias.Redondear(fila.Costo);
        var margen = ingreso - costo;

        return new RentabilidadFilaDto
        {
            IdGrupo = fila.IdGrupo,
            Grupo = fila.Grupo ?? NombreSinGrupo(agruparPor),
            Unidades = fila.Unidades,
            IngresoNeto = ingreso,
            Costo = costo,
            MargenBruto = margen,
            MargenPorcentual = CalcularPorcentaje(margen, ingreso)
        };
    }

    private static string NombreSinGrupo(AgrupacionRentabilidadEnum agruparPor)
    {
        return agruparPor switch
        {
            AgrupacionRentabilidadEnum.Categoria => "Sin categoría",
            AgrupacionRentabilidadEnum.Marca => "Sin marca",
            _ => string.Empty
        };
    }
}
