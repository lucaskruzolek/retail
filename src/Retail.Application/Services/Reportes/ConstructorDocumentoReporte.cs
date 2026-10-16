using System.Globalization;
using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Reportes.Exportacion;

namespace Retail.Application.Services.Reportes;

/// <summary>
/// Traduce el resultado que la pantalla muestra a un <see cref="DocumentoReporte"/> neutro (D-12). Las celdas
/// conservan su tipo (<see cref="int"/>, <see cref="decimal"/>, <see cref="DateOnly"/>): el formato lo decide cada
/// exportador. Los valores de columnas <see cref="TipoColumnaEnum.Porcentaje"/> están en unidades de porcentaje
/// (25,5 significa 25,5 %).
/// </summary>
internal static class ConstructorDocumentoReporte
{
    private const string TodasLasCategorias = "Todas";
    private const string TodasLasMarcas = "Todas";
    private const string TodosLosCajeros = "Todos";

    /// <summary>
    /// Máximo de barras de un gráfico horizontal (D-26): con más, las etiquetas se amontonan y dejan de leerse. La tabla
    /// del documento conserva todas las filas.
    /// </summary>
    private const int MaximoBarrasHorizontales = 15;

    /// <summary>Arma el documento que corresponde al tipo concreto de <paramref name="resultado"/>.</summary>
    /// <exception cref="ArgumentException">El tipo de resultado no tiene un documento definido.</exception>
    public static DocumentoReporte Crear(ResultadoReporteDto resultado, string generadoPor, NombresFiltro nombres)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(nombres);

        var (filtros, tablas, graficos) = resultado switch
        {
            TableroDto tablero => ArmarTablero(tablero),
            VentasPorPeriodoDto ventas => ArmarVentasPorPeriodo(ventas, nombres),
            RankingArticulosDto ranking => ArmarRanking(ranking, nombres),
            RentabilidadDto rentabilidad => ArmarRentabilidad(rentabilidad, nombres),
            StockCriticoDto stock => ArmarStockCritico(stock, nombres),
            SinMovimientoDto sinMovimiento => ArmarSinMovimiento(sinMovimiento, nombres),
            _ => throw new ArgumentException(
                $"No hay un documento definido para el reporte '{resultado.GetType().Name}'.",
                nameof(resultado))
        };

        return new DocumentoReporte(resultado.Titulo, filtros, resultado.GeneradoEl, generadoPor, tablas, graficos);
    }

    private static Armado ArmarTablero(TableroDto t)
    {
        var filtros = new List<FiltroAplicado>
        {
            new("Período", Periodo(t.Filtro.Desde, t.Filtro.Hasta)),
            new("Período anterior", Periodo(t.DesdeAnterior, t.HastaAnterior))
        };

        var pesos = Tabla(
            "Indicadores en pesos",
            [Col("Indicador", TipoColumnaEnum.Texto), Col("Actual", TipoColumnaEnum.Moneda), Col("Anterior", TipoColumnaEnum.Moneda), Col("Variación %", TipoColumnaEnum.Porcentaje)],
            [
                Fila("Total vendido", t.TotalVendido.Valor, t.TotalVendido.ValorAnterior, t.TotalVendido.VariacionPorcentual),
                Fila("Margen bruto", t.MargenBruto.Valor, t.MargenBruto.ValorAnterior, t.MargenBruto.VariacionPorcentual),
                Fila("Valorización del inventario a costo", t.ValorizacionInventarioCosto, null, null)
            ]);

        var actividad = Tabla(
            "Indicadores de actividad",
            [Col("Indicador", TipoColumnaEnum.Texto), Col("Actual", TipoColumnaEnum.Entero), Col("Anterior", TipoColumnaEnum.Entero), Col("Variación %", TipoColumnaEnum.Porcentaje)],
            [
                Fila("Cantidad de ventas", (int)t.CantidadVentas.Valor, (int)t.CantidadVentas.ValorAnterior, t.CantidadVentas.VariacionPorcentual),
                Fila("Artículos bajo el stock mínimo", t.ArticulosBajoStockMinimo, null, null)
            ]);

        var margen = Tabla(
            "Margen porcentual",
            [Col("Indicador", TipoColumnaEnum.Texto), Col("Actual", TipoColumnaEnum.Porcentaje), Col("Anterior", TipoColumnaEnum.Porcentaje), Col("Variación (puntos)", TipoColumnaEnum.Porcentaje)],
            [Fila("Margen bruto sobre lo vendido", t.MargenPorcentual, t.MargenPorcentualAnterior, t.VariacionMargenPuntos)]);

        var evolucion = TablaPuntos("Evolución de ventas", t.EvolucionVentas);
        var porCategoria = TablaRentabilidad("Margen por categoría", t.MargenPorCategoria, total: null);

        var graficos = new List<GraficoReporte>();
        if (t.EvolucionVentas.Count > 0)
        {
            graficos.Add(GraficoEvolucion("Evolución de ventas", t.EvolucionVentas, t.AgrupacionEvolucion));
        }

        if (t.MargenPorCategoria.Count > 0)
        {
            graficos.Add(GraficoMargen("Margen bruto por categoría", t.MargenPorCategoria));
        }

        return new Armado(filtros, [pesos, actividad, margen, evolucion, porCategoria], graficos);
    }

    private static Armado ArmarVentasPorPeriodo(VentasPorPeriodoDto v, NombresFiltro nombres)
    {
        var filtros = new List<FiltroAplicado>
        {
            new("Período", Periodo(v.Filtro.Desde, v.Filtro.Hasta)),
            new("Agrupación", NombreAgrupacion(v.Filtro.Agrupacion)),
            new("Cajero", nombres.Cajero ?? TodosLosCajeros)
        };

        var resumen = Tabla(
            "Totales del período",
            [Col("Ventas", TipoColumnaEnum.Entero), Col("Total vendido", TipoColumnaEnum.Moneda), .. ColumnasMedios(), Col("Descuentos", TipoColumnaEnum.Moneda)],
            [Fila(v.CantidadVentas, v.TotalVendido, v.Efectivo, v.TarjetaDebito, v.TarjetaCredito, v.TransferenciaQr, v.CuentaCorriente, v.Descuentos)]);

        var graficos = new List<GraficoReporte>();
        if (v.Puntos.Count > 0)
        {
            graficos.Add(GraficoEvolucion("Ventas por período", v.Puntos, v.Filtro.Agrupacion));
        }

        return new Armado(filtros, [resumen, TablaPuntosPorMedio("Ventas por período", v.Puntos)], graficos);
    }

    private static Armado ArmarRanking(RankingArticulosDto r, NombresFiltro nombres)
    {
        var filtros = new List<FiltroAplicado>
        {
            new("Período", Periodo(r.Filtro.Desde, r.Filtro.Hasta)),
            new("Categoría", nombres.Categoria ?? TodasLasCategorias),
            new("Marca", nombres.Marca ?? TodasLasMarcas),
            new("Criterio", r.Filtro.Criterio == CriterioRankingEnum.Unidades ? "Unidades" : "Importe"),
            new("Cantidad", r.Filtro.Cantidad.ToString(CultureInfo.InvariantCulture))
        };

        var graficos = new List<GraficoReporte>();
        if (r.MasVendidos.Count > 0)
        {
            var porUnidades = r.Filtro.Criterio == CriterioRankingEnum.Unidades;
            var visibles = r.MasVendidos.Take(MaximoBarrasHorizontales).ToList();
            var valores = visibles
                .Select(a => porUnidades ? a.Unidades : a.IngresoNeto)
                .ToList();

            graficos.Add(new GraficoReporte(
                TituloRecortado("Artículos más vendidos", r.MasVendidos.Count),
                TipoGraficoEnum.BarrasHorizontales,
                visibles.Select(a => a.Descripcion).ToList(),
                [new SerieGrafico(porUnidades ? "Unidades" : "Ingreso neto", valores)]));
        }

        return new Armado(
            filtros,
            [TablaRanking("Más vendidos", r.MasVendidos), TablaRanking("Menos vendidos", r.MenosVendidos)],
            graficos);
    }

    private static Armado ArmarRentabilidad(RentabilidadDto r, NombresFiltro nombres)
    {
        var filtros = new List<FiltroAplicado>
        {
            new("Período", Periodo(r.Filtro.Desde, r.Filtro.Hasta)),
            new("Categoría", nombres.Categoria ?? TodasLasCategorias),
            new("Marca", nombres.Marca ?? TodasLasMarcas),
            new("Agrupado por", r.Filtro.AgruparPor.ToString())
        };

        var graficos = new List<GraficoReporte>();
        if (r.Filas.Count > 0)
        {
            graficos.Add(GraficoMargen("Margen bruto", r.Filas));
        }

        return new Armado(filtros, [TablaRentabilidad("Rentabilidad", r.Filas, r.Total)], graficos);
    }

    private static Armado ArmarStockCritico(StockCriticoDto s, NombresFiltro nombres)
    {
        var filtros = FiltrosInventario(nombres);

        var valorizacion = Tabla(
            "Valorización del inventario",
            [Col("Artículos considerados", TipoColumnaEnum.Entero), Col("Valorización a costo", TipoColumnaEnum.Moneda), Col("Valorización a precio de venta", TipoColumnaEnum.Moneda)],
            [Fila(s.ArticulosConsiderados, s.ValorizacionCosto, s.ValorizacionVenta)]);

        var filas = Tabla(
            "Stock crítico",
            [
                Col("Artículo", TipoColumnaEnum.Texto), Col("Código de barras", TipoColumnaEnum.Texto),
                Col("Categoría", TipoColumnaEnum.Texto), Col("Marca", TipoColumnaEnum.Texto),
                Col("Stock actual", TipoColumnaEnum.Entero), Col("Stock mínimo", TipoColumnaEnum.Entero),
                Col("Faltante", TipoColumnaEnum.Entero), Col("Costo de reposición", TipoColumnaEnum.Moneda),
                Col("Precio de venta", TipoColumnaEnum.Moneda)
            ],
            s.Filas.Select(f => Fila(
                f.Descripcion, f.CodigoBarras, f.Categoria, f.Marca, f.StockActual, f.StockMinimo, f.Faltante,
                f.CostoReposicion, f.PrecioVenta)).ToList());

        return new Armado(filtros, [valorizacion, filas], []);
    }

    private static Armado ArmarSinMovimiento(SinMovimientoDto s, NombresFiltro nombres)
    {
        var filtros = FiltrosInventario(nombres);
        filtros.Add(new FiltroAplicado("Días sin ventas", s.Filtro.Dias.ToString(CultureInfo.InvariantCulture)));
        filtros.Add(new FiltroAplicado("Sin ventas desde", Fecha(s.SinVentasDesde)));

        var resumen = Tabla(
            "Capital inmovilizado",
            [Col("Capital inmovilizado total", TipoColumnaEnum.Moneda)],
            [Fila(s.CapitalInmovilizadoTotal)]);

        var filas = Tabla(
            "Artículos sin movimiento",
            [
                Col("Artículo", TipoColumnaEnum.Texto), Col("Código de barras", TipoColumnaEnum.Texto),
                Col("Categoría", TipoColumnaEnum.Texto), Col("Marca", TipoColumnaEnum.Texto),
                Col("Stock actual", TipoColumnaEnum.Entero), Col("Costo de reposición", TipoColumnaEnum.Moneda),
                Col("Última venta", TipoColumnaEnum.Fecha), Col("Capital inmovilizado", TipoColumnaEnum.Moneda)
            ],
            s.Filas.Select(f => Fila(
                f.Descripcion, f.CodigoBarras, f.Categoria, f.Marca, f.StockActual, f.CostoReposicion,
                f.UltimaVenta is { } ultima ? DateOnly.FromDateTime(ultima) : null, f.CapitalInmovilizado)).ToList());

        return new Armado(filtros, [resumen, filas], []);
    }

    private static List<FiltroAplicado> FiltrosInventario(NombresFiltro nombres)
    {
        return
        [
            new FiltroAplicado("Categoría", nombres.Categoria ?? TodasLasCategorias),
            new FiltroAplicado("Marca", nombres.Marca ?? TodasLasMarcas)
        ];
    }

    private static TablaReporte TablaPuntos(string titulo, IReadOnlyList<PuntoVentasDto> puntos)
    {
        return Tabla(
            titulo,
            [Col("Período", TipoColumnaEnum.Fecha), Col("Ventas", TipoColumnaEnum.Entero), Col("Total vendido", TipoColumnaEnum.Moneda), Col("Descuentos", TipoColumnaEnum.Moneda)],
            puntos.Select(p => Fila(p.InicioPeriodo, p.CantidadVentas, p.TotalVendido, p.Descuentos)).ToList());
    }

    /// <summary>Las cinco columnas del desglose por medio de pago (D-34), con títulos cortos para que entren en el PDF.</summary>
    private static ColumnaReporte[] ColumnasMedios()
    {
        return
        [
            Col("Efectivo", TipoColumnaEnum.Moneda), Col("Débito", TipoColumnaEnum.Moneda), Col("Crédito", TipoColumnaEnum.Moneda),
            Col("Transf./QR", TipoColumnaEnum.Moneda), Col("Cta. cte.", TipoColumnaEnum.Moneda)
        ];
    }

    private static TablaReporte TablaPuntosPorMedio(string titulo, IReadOnlyList<PuntoVentasDto> puntos)
    {
        return Tabla(
            titulo,
            [Col("Período", TipoColumnaEnum.Fecha), Col("Ventas", TipoColumnaEnum.Entero), Col("Total vendido", TipoColumnaEnum.Moneda), .. ColumnasMedios(), Col("Descuentos", TipoColumnaEnum.Moneda)],
            puntos
                .Select(p => Fila(
                    p.InicioPeriodo, p.CantidadVentas, p.TotalVendido, p.Efectivo, p.TarjetaDebito, p.TarjetaCredito,
                    p.TransferenciaQr, p.CuentaCorriente, p.Descuentos))
                .ToList());
    }

    private static TablaReporte TablaRanking(string titulo, IReadOnlyList<RankingArticuloDto> articulos)
    {
        return Tabla(
            titulo,
            [
                Col("Artículo", TipoColumnaEnum.Texto), Col("Código de barras", TipoColumnaEnum.Texto),
                Col("Categoría", TipoColumnaEnum.Texto), Col("Marca", TipoColumnaEnum.Texto),
                Col("Unidades", TipoColumnaEnum.Entero), Col("Ingreso neto", TipoColumnaEnum.Moneda)
            ],
            articulos.Select(a => Fila(a.Descripcion, a.CodigoBarras, a.Categoria, a.Marca, a.Unidades, a.IngresoNeto)).ToList());
    }

    private static TablaReporte TablaRentabilidad(
        string titulo,
        IReadOnlyList<RentabilidadFilaDto> filas,
        RentabilidadFilaDto? total)
    {
        var celdas = filas.Select(FilaRentabilidad).ToList();
        if (total is not null)
        {
            celdas.Add(FilaRentabilidad(total));
        }

        return Tabla(
            titulo,
            [
                Col("Grupo", TipoColumnaEnum.Texto), Col("Unidades", TipoColumnaEnum.Entero),
                Col("Ingreso neto", TipoColumnaEnum.Moneda), Col("Costo", TipoColumnaEnum.Moneda),
                Col("Margen bruto", TipoColumnaEnum.Moneda), Col("Margen %", TipoColumnaEnum.Porcentaje)
            ],
            celdas);
    }

    private static object?[] FilaRentabilidad(RentabilidadFilaDto f)
    {
        return Fila(f.Grupo, f.Unidades, f.IngresoNeto, f.Costo, f.MargenBruto, f.MargenPorcentual);
    }

    private static GraficoReporte GraficoEvolucion(
        string titulo,
        IReadOnlyList<PuntoVentasDto> puntos,
        AgrupacionTemporalEnum agrupacion)
    {
        var formato = agrupacion == AgrupacionTemporalEnum.Mes ? "MM/yyyy" : "dd/MM";

        return new GraficoReporte(
            titulo,
            TipoGraficoEnum.Lineas,
            puntos.Select(p => p.InicioPeriodo.ToString(formato, CultureInfo.InvariantCulture)).ToList(),
            [new SerieGrafico("Total vendido", puntos.Select(p => p.TotalVendido).ToList())]);
    }

    private static GraficoReporte GraficoMargen(string titulo, IReadOnlyList<RentabilidadFilaDto> filas)
    {
        var visibles = filas.Take(MaximoBarrasHorizontales).ToList();

        return new GraficoReporte(
            TituloRecortado(titulo, filas.Count),
            TipoGraficoEnum.BarrasHorizontales,
            visibles.Select(f => f.Grupo).ToList(),
            [new SerieGrafico("Margen bruto", visibles.Select(f => f.MargenBruto).ToList())]);
    }

    private static string TituloRecortado(string titulo, int cantidadTotal)
    {
        return cantidadTotal > MaximoBarrasHorizontales
            ? $"{titulo} (primeros {MaximoBarrasHorizontales})"
            : titulo;
    }

    private static TablaReporte Tabla(string titulo, ColumnaReporte[] columnas, IReadOnlyList<IReadOnlyList<object?>> filas)
    {
        return new TablaReporte(titulo, columnas, filas);
    }

    private static ColumnaReporte Col(string encabezado, TipoColumnaEnum tipo)
    {
        return new ColumnaReporte(encabezado, tipo);
    }

    private static object?[] Fila(params object?[] celdas)
    {
        return celdas;
    }

    private static string Periodo(DateOnly desde, DateOnly hasta)
    {
        return $"{Fecha(desde)} al {Fecha(hasta)}";
    }

    private static string Fecha(DateOnly fecha)
    {
        return fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    private static string NombreAgrupacion(AgrupacionTemporalEnum agrupacion)
    {
        return agrupacion switch
        {
            AgrupacionTemporalEnum.Semana => "Semana",
            AgrupacionTemporalEnum.Mes => "Mes",
            _ => "Día"
        };
    }

    private sealed record Armado(
        IReadOnlyList<FiltroAplicado> Filtros,
        IReadOnlyList<TablaReporte> Tablas,
        IReadOnlyList<GraficoReporte> Graficos);
}
