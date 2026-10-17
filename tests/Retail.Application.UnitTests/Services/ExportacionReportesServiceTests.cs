using FluentAssertions;
using NSubstitute;
using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Reportes.Exportacion;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class ExportacionReportesServiceTests
{
    private const int IdGerente = 1;
    private const int IdCajero = 2;
    private static readonly DateTime Generado = new(2026, 10, 15, 12, 0, 0);
    private static readonly DateOnly Desde = new(2026, 10, 1);
    private static readonly DateOnly Hasta = new(2026, 10, 15);

    private readonly IExportadorReportes _exportador = Substitute.For<IExportadorReportes>();
    private readonly IRepository<Usuario> _usuarios = Substitute.For<IRepository<Usuario>>();
    private readonly IRepository<Categoria> _categorias = Substitute.For<IRepository<Categoria>>();
    private readonly IRepository<Marca> _marcas = Substitute.For<IRepository<Marca>>();
    private readonly ExportacionReportesService _sut;

    public ExportacionReportesServiceTests()
    {
        RegistrarUsuario(IdGerente, "gerente", RolUsuarioEnum.Gerente);
        RegistrarUsuario(IdCajero, "cajero", RolUsuarioEnum.Cajero);
        _sut = new ExportacionReportesService(_exportador, _usuarios, _categorias, _marcas);
    }

    private void RegistrarUsuario(int id, string nombreUsuario, RolUsuarioEnum rol, string nombre = "Juan", string apellido = "Pérez", bool includeDeleted = false)
    {
        var email = rol == RolUsuarioEnum.Gerente ? $"{nombreUsuario}@libreria.com" : null;
        var usuario = Usuario.Crear(nombreUsuario, nombre, apellido, "hash", rol, email);
        usuario.Id = id;
        _usuarios.GetByIdAsync(id, includeDeleted, Arg.Any<CancellationToken>()).Returns(usuario);
    }

    private void RegistrarCategoria(int id, string nombre)
    {
        _categorias.GetByIdAsync(id, includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(CategoriaConId(id, nombre));
    }

    private void RegistrarMarca(int id, string nombre)
    {
        _marcas.GetByIdAsync(id, includeDeleted: true, Arg.Any<CancellationToken>())
            .Returns(MarcaConId(id, nombre));
    }

    /// <summary>Exporta como Gerente y devuelve el documento que recibió el exportador.</summary>
    private async Task<DocumentoReporte> ExportarComoGerenteAsync(ResultadoReporteDto resultado)
    {
        DocumentoReporte? capturado = null;
        _exportador.ExportarAsync(
                Arg.Do<DocumentoReporte>(d => capturado = d),
                Arg.Any<FormatoExportacionEnum>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _sut.ExportarAsync(IdGerente, resultado, FormatoExportacionEnum.Excel, new MemoryStream());

        return capturado!;
    }

    private static object?[] CeldasDe(TablaReporte tabla, int fila) => tabla.Filas[fila].ToArray();

    private static TableroDto CrearTablero() => new()
    {
        GeneradoEl = Generado,
        Filtro = new FiltroPeriodoDto { Desde = Desde, Hasta = Hasta },
        DesdeAnterior = new DateOnly(2026, 9, 16),
        HastaAnterior = new DateOnly(2026, 9, 30),
        TotalVendido = new IndicadorDto { Valor = 1000m, ValorAnterior = 500m, VariacionPorcentual = 100m },
        CantidadVentas = new IndicadorDto { Valor = 10m, ValorAnterior = 8m, VariacionPorcentual = 25m },
        MargenBruto = new IndicadorDto { Valor = 400m, ValorAnterior = 150m, VariacionPorcentual = 166.67m },
        MargenPorcentual = 40m,
        MargenPorcentualAnterior = 30m,
        VariacionMargenPuntos = 10m,
        ArticulosBajoStockMinimo = 3,
        ValorizacionInventarioCosto = 9000m,
        EvolucionVentas = [new PuntoVentasDto { InicioPeriodo = Desde, CantidadVentas = 2, TotalVendido = 300m, Descuentos = 5m }],
        AgrupacionEvolucion = AgrupacionTemporalEnum.Dia,
        MargenPorCategoria = [new RentabilidadFilaDto { IdGrupo = 1, Grupo = "Libros", Unidades = 4, IngresoNeto = 100m, Costo = 60m, MargenBruto = 40m, MargenPorcentual = 40m }]
    };

    [Fact]
    public async Task Exportar_ConActorCajero_LanzaOperacionNoAutorizadaYNoExporta()
    {
        var accion = () => _sut.ExportarAsync(IdCajero, CrearTablero(), FormatoExportacionEnum.Pdf, new MemoryStream());

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        _exportador.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Exportar_ConActorInexistente_LanzaOperacionNoAutorizadaYNoExporta()
    {
        var accion = () => _sut.ExportarAsync(99, CrearTablero(), FormatoExportacionEnum.Pdf, new MemoryStream());

        await accion.Should().ThrowAsync<OperacionNoAutorizadaException>();
        _exportador.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Exportar_ConGerente_EntregaAlExportadorElMismoStreamYFormato()
    {
        using var destino = new MemoryStream();

        await _sut.ExportarAsync(IdGerente, CrearTablero(), FormatoExportacionEnum.Pdf, destino);

        await _exportador.Received(1).ExportarAsync(
            Arg.Any<DocumentoReporte>(), FormatoExportacionEnum.Pdf, Arg.Is<Stream>(s => ReferenceEquals(s, destino)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exportar_ConResultadoDeTipoDesconocido_LanzaArgumentException()
    {
        var accion = () => _sut.ExportarAsync(IdGerente, new ReporteDesconocidoDto(), FormatoExportacionEnum.Excel, new MemoryStream());

        await accion.Should().ThrowAsync<ArgumentException>();
        _exportador.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Exportar_Tablero_ArmaDocumentoConTituloAutorFiltrosYCeldasTipadas()
    {
        var documento = await ExportarComoGerenteAsync(CrearTablero());

        documento.Titulo.Should().Be("Tablero gerencial");
        documento.GeneradoPor.Should().Be("Juan Pérez");
        documento.GeneradoEl.Should().Be(Generado);
        documento.Filtros.Should().Contain(new FiltroAplicado("Período", "01/10/2026 al 15/10/2026"));
        documento.Tablas.Select(t => t.Titulo).Should().Contain("Indicadores en pesos");

        var pesos = documento.Tablas.Single(t => t.Titulo == "Indicadores en pesos");
        CeldasDe(pesos, 0).Should().Equal("Total vendido", 1000m, 500m, 100m);
        pesos.Columnas.Select(c => c.Tipo).Should().Equal(
            TipoColumnaEnum.Texto, TipoColumnaEnum.Moneda, TipoColumnaEnum.Moneda, TipoColumnaEnum.Porcentaje);

        var actividad = documento.Tablas.Single(t => t.Titulo == "Indicadores de actividad");
        CeldasDe(actividad, 0).Should().Equal("Cantidad de ventas", 10, 8, 25m);
        CeldasDe(actividad, 0)[1].Should().BeOfType<int>();

        var evolucion = documento.Tablas.Single(t => t.Titulo == "Evolución de ventas");
        CeldasDe(evolucion, 0).Should().Equal(Desde, 2, 300m, 5m);

        documento.Graficos.Select(g => g.Tipo).Should().Equal(TipoGraficoEnum.Lineas, TipoGraficoEnum.BarrasHorizontales);
        documento.Graficos[0].Series.Single().Valores.Should().Equal(300m);
        documento.Graficos[1].Etiquetas.Should().Equal("Libros");
    }

    [Fact]
    public async Task Exportar_VentasPorPeriodo_ArmaTablaConFechasEnterosYMonedas()
    {
        var resultado = new VentasPorPeriodoDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroVentasPorPeriodoDto { Desde = Desde, Hasta = Hasta, Agrupacion = AgrupacionTemporalEnum.Semana, IdUsuario = 7 },
            Puntos =
            [
                new PuntoVentasDto
                {
                    InicioPeriodo = new DateOnly(2026, 9, 28), CantidadVentas = 3, TotalVendido = 450.5m, Descuentos = 5m,
                    Efectivo = 200m, TarjetaDebito = 100.5m, TarjetaCredito = 50m, TransferenciaQr = 40m, CuentaCorriente = 60m
                }
            ],
            TotalVendido = 450.5m,
            CantidadVentas = 3,
            Descuentos = 5m,
            Efectivo = 200m,
            TarjetaDebito = 100.5m,
            TarjetaCredito = 50m,
            TransferenciaQr = 40m,
            CuentaCorriente = 60m
        };

        RegistrarUsuario(7, "mgomez", RolUsuarioEnum.Cajero, "María", "Gómez", includeDeleted: true);

        var documento = await ExportarComoGerenteAsync(resultado);

        documento.Titulo.Should().Be("Ventas por período");
        documento.Filtros.Should().Contain(new FiltroAplicado("Agrupación", "Semana"));
        documento.Filtros.Should().Contain(new FiltroAplicado("Cajero", "María Gómez"));
        var puntos = documento.Tablas.Single(t => t.Titulo == "Ventas por período");
        CeldasDe(puntos, 0).Should().Equal(new DateOnly(2026, 9, 28), 3, 450.5m, 200m, 100.5m, 50m, 40m, 60m, 5m);
        puntos.Columnas.Select(c => c.Encabezado).Should().Equal(
            "Período", "Ventas", "Total vendido", "Efectivo", "Débito", "Crédito", "Transf./QR", "Cta. cte.", "Descuentos");
        puntos.Columnas.Select(c => c.Tipo).Should().Equal(
            TipoColumnaEnum.Fecha, TipoColumnaEnum.Entero, TipoColumnaEnum.Moneda, TipoColumnaEnum.Moneda, TipoColumnaEnum.Moneda,
            TipoColumnaEnum.Moneda, TipoColumnaEnum.Moneda, TipoColumnaEnum.Moneda, TipoColumnaEnum.Moneda);

        var totales = documento.Tablas.Single(t => t.Titulo == "Totales del período");
        totales.Columnas.Select(c => c.Encabezado).Should().Equal(
            "Ventas", "Total vendido", "Efectivo", "Débito", "Crédito", "Transf./QR", "Cta. cte.", "Descuentos");
        CeldasDe(totales, 0).Should().Equal(3, 450.5m, 200m, 100.5m, 50m, 40m, 60m, 5m);
        documento.Graficos.Should().ContainSingle().Which.Tipo.Should().Be(TipoGraficoEnum.Lineas);
    }

    [Fact]
    public async Task Exportar_Ranking_ArmaDosTablasYGraficoDeBarrasSegunElCriterio()
    {
        var resultado = new RankingArticulosDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroRankingDto { Desde = Desde, Hasta = Hasta, Criterio = CriterioRankingEnum.Importe },
            MasVendidos = [new RankingArticuloDto { IdArticulo = 1, Descripcion = "Cuaderno", Unidades = 30, IngresoNeto = 1500m }],
            MenosVendidos = [new RankingArticuloDto { IdArticulo = 2, Descripcion = "Regla", Unidades = 0, IngresoNeto = 0m }]
        };

        var documento = await ExportarComoGerenteAsync(resultado);

        documento.Titulo.Should().Be("Ranking de artículos");
        documento.Filtros.Should().Contain(new FiltroAplicado("Categoría", "Todas"));
        documento.Tablas.Select(t => t.Titulo).Should().Equal("Más vendidos", "Menos vendidos");
        CeldasDe(documento.Tablas[0], 0).Should().Equal(new object?[] { "Cuaderno", null, null, null, 30, 1500m });
        var grafico = documento.Graficos.Should().ContainSingle().Subject;
        grafico.Tipo.Should().Be(TipoGraficoEnum.BarrasHorizontales);
        grafico.Series.Single().Valores.Should().Equal(1500m);
    }

    [Fact]
    public async Task Exportar_Rentabilidad_AgregaLaFilaDeTotalAlFinalYGraficoDeBarras()
    {
        var resultado = new RentabilidadDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroRentabilidadDto { Desde = Desde, Hasta = Hasta, IdCategoria = 3 },
            Filas = [new RentabilidadFilaDto { IdGrupo = 1, Grupo = "Libros", Unidades = 4, IngresoNeto = 100m, Costo = 60m, MargenBruto = 40m, MargenPorcentual = 40m }],
            Total = new RentabilidadFilaDto { Grupo = "Total", Unidades = 4, IngresoNeto = 100m, Costo = 60m, MargenBruto = 40m, MargenPorcentual = 40m }
        };

        RegistrarCategoria(3, "Librería escolar");

        var documento = await ExportarComoGerenteAsync(resultado);

        documento.Titulo.Should().Be("Rentabilidad");
        documento.Filtros.Should().Contain(new FiltroAplicado("Categoría", "Librería escolar"));
        var tabla = documento.Tablas.Should().ContainSingle().Subject;
        tabla.Filas.Should().HaveCount(2);
        CeldasDe(tabla, 1).Should().Equal("Total", 4, 100m, 60m, 40m, 40m);
        documento.Graficos.Should().ContainSingle().Which.Tipo.Should().Be(TipoGraficoEnum.BarrasHorizontales);
    }

    [Fact]
    public async Task Exportar_StockCritico_ArmaTablasTipadasSinGrafico()
    {
        var resultado = new StockCriticoDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroInventarioDto(),
            Filas = [new StockCriticoFilaDto { IdArticulo = 1, Descripcion = "Lápiz", StockActual = 4, StockMinimo = 5, Faltante = 1, CostoReposicion = 10m, PrecioVenta = 15m }],
            ArticulosConsiderados = 50,
            ValorizacionCosto = 1000m,
            ValorizacionVenta = 2000m
        };

        var documento = await ExportarComoGerenteAsync(resultado);

        documento.Titulo.Should().Be("Stock crítico y valorización");
        documento.Graficos.Should().BeEmpty();
        var filas = documento.Tablas.Single(t => t.Titulo == "Stock crítico");
        CeldasDe(filas, 0).Should().Equal(new object?[] { "Lápiz", null, null, null, 4, 5, 1, 10m, 15m });
        var valorizacion = documento.Tablas.Single(t => t.Titulo == "Valorización del inventario");
        CeldasDe(valorizacion, 0).Should().Equal(50, 1000m, 2000m);
    }

    [Fact]
    public async Task Exportar_SinMovimiento_ConvierteLaUltimaVentaEnFechaYNuncaVendidoEnNulo()
    {
        var resultado = new SinMovimientoDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroSinMovimientoDto { Dias = 90, IdMarca = 5 },
            SinVentasDesde = new DateOnly(2026, 7, 17),
            Filas =
            [
                new ArticuloSinMovimientoDto { IdArticulo = 1, Descripcion = "Agenda", StockActual = 4, CostoReposicion = 10.5m, UltimaVenta = new DateTime(2026, 5, 9, 23, 0, 0), CapitalInmovilizado = 42m },
                new ArticuloSinMovimientoDto { IdArticulo = 2, Descripcion = "Carpeta", StockActual = 100, CostoReposicion = 20m, UltimaVenta = null, CapitalInmovilizado = 2000m }
            ],
            CapitalInmovilizadoTotal = 2042m
        };

        RegistrarMarca(5, "Faber-Castell");

        var documento = await ExportarComoGerenteAsync(resultado);

        documento.Titulo.Should().Be("Artículos sin movimiento");
        documento.Filtros.Should().Contain(new FiltroAplicado("Marca", "Faber-Castell"));
        documento.Filtros.Should().Contain(new FiltroAplicado("Sin ventas desde", "17/07/2026"));
        documento.Graficos.Should().BeEmpty();
        var filas = documento.Tablas.Single(t => t.Titulo == "Artículos sin movimiento");
        CeldasDe(filas, 0).Should().Equal(new object?[] { "Agenda", null, null, null, 4, 10.5m, new DateOnly(2026, 5, 9), 42m });
        CeldasDe(filas, 1)[6].Should().BeNull();
    }

    private static RankingArticulosDto CrearRanking(int cantidad, int? idCategoria = null, int? idMarca = null)
    {
        return new RankingArticulosDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroRankingDto { Desde = Desde, Hasta = Hasta, Cantidad = cantidad, IdCategoria = idCategoria, IdMarca = idMarca },
            MasVendidos = Enumerable.Range(1, cantidad)
                .Select(i => new RankingArticuloDto { IdArticulo = i, Descripcion = $"Artículo {i}", Unidades = i, IngresoNeto = i * 10m })
                .ToList(),
            MenosVendidos = []
        };
    }

    [Fact]
    public async Task Exportar_RankingConMasDeQuinceFilas_RecortaElGraficoAQuinceYLaTablaConservaTodas()
    {
        var documento = await ExportarComoGerenteAsync(CrearRanking(20));

        var grafico = documento.Graficos.Should().ContainSingle().Subject;
        grafico.Titulo.Should().Be("Artículos más vendidos (primeros 15)");
        grafico.Etiquetas.Should().HaveCount(15).And.StartWith("Artículo 1");
        grafico.Series.Single().Valores.Should().HaveCount(15);
        documento.Tablas[0].Filas.Should().HaveCount(20);
    }

    [Fact]
    public async Task Exportar_RankingConQuinceFilas_NoRecortaNiAgregaSufijo()
    {
        var documento = await ExportarComoGerenteAsync(CrearRanking(15));

        var grafico = documento.Graficos.Should().ContainSingle().Subject;
        grafico.Titulo.Should().Be("Artículos más vendidos");
        grafico.Etiquetas.Should().HaveCount(15);
    }

    [Fact]
    public async Task Exportar_RentabilidadConMasDeQuinceFilas_RecortaElGraficoYLaTablaConservaTodas()
    {
        var filas = Enumerable.Range(1, 18)
            .Select(i => new RentabilidadFilaDto { IdGrupo = i, Grupo = $"Grupo {i}", Unidades = 1, IngresoNeto = 10m, Costo = 5m, MargenBruto = 5m, MargenPorcentual = 50m })
            .ToList();
        var resultado = new RentabilidadDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroRentabilidadDto { Desde = Desde, Hasta = Hasta },
            Filas = filas,
            Total = new RentabilidadFilaDto { Grupo = "Total" }
        };

        var documento = await ExportarComoGerenteAsync(resultado);

        var grafico = documento.Graficos.Should().ContainSingle().Subject;
        grafico.Titulo.Should().Be("Margen bruto (primeros 15)");
        grafico.Etiquetas.Should().HaveCount(15);
        documento.Tablas.Single().Filas.Should().HaveCount(19);
    }

    [Fact]
    public async Task Exportar_ConIdsPresentes_MuestraLosNombresResueltosYBuscaIncluyendoBajas()
    {
        RegistrarCategoria(3, "Librería escolar");
        RegistrarMarca(4, "Ñandú");

        var documento = await ExportarComoGerenteAsync(CrearRanking(1, idCategoria: 3, idMarca: 4));

        documento.Filtros.Should().Contain(new FiltroAplicado("Categoría", "Librería escolar"));
        documento.Filtros.Should().Contain(new FiltroAplicado("Marca", "Ñandú"));
        await _categorias.Received(1).GetByIdAsync(3, true, Arg.Any<CancellationToken>());
        await _marcas.Received(1).GetByIdAsync(4, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exportar_ConCajeroEnElFiltro_BuscaIncluyendoBajas()
    {
        RegistrarUsuario(7, "mgomez", RolUsuarioEnum.Cajero, "María", "Gómez", includeDeleted: true);
        var resultado = new VentasPorPeriodoDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroVentasPorPeriodoDto { Desde = Desde, Hasta = Hasta, IdUsuario = 7 }
        };

        var documento = await ExportarComoGerenteAsync(resultado);

        documento.Filtros.Should().Contain(new FiltroAplicado("Cajero", "María Gómez"));
        await _usuarios.Received(1).GetByIdAsync(7, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exportar_ConIdsInexistentes_MuestraNumeralYElId()
    {
        var documento = await ExportarComoGerenteAsync(CrearRanking(1, idCategoria: 99, idMarca: 98));

        documento.Filtros.Should().Contain(new FiltroAplicado("Categoría", "#99"));
        documento.Filtros.Should().Contain(new FiltroAplicado("Marca", "#98"));
    }

    [Fact]
    public async Task Exportar_SinIdsEnElFiltro_MuestraTodasYNoConsultaNada()
    {
        var documento = await ExportarComoGerenteAsync(CrearRanking(1));

        documento.Filtros.Should().Contain(new FiltroAplicado("Categoría", "Todas"));
        documento.Filtros.Should().Contain(new FiltroAplicado("Marca", "Todas"));
        _categorias.ReceivedCalls().Should().BeEmpty();
        _marcas.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Exportar_VentasSinCajero_MuestraTodos()
    {
        var resultado = new VentasPorPeriodoDto
        {
            GeneradoEl = Generado,
            Filtro = new FiltroVentasPorPeriodoDto { Desde = Desde, Hasta = Hasta }
        };

        var documento = await ExportarComoGerenteAsync(resultado);

        documento.Filtros.Should().Contain(new FiltroAplicado("Cajero", "Todos"));
    }

    private sealed record class ReporteDesconocidoDto : ResultadoReporteDto
    {
        public override string Titulo => "Desconocido";
    }

    private static Categoria CategoriaConId(int id, string nombre)
    {
        var categoria = Categoria.Crear(nombre);
        categoria.Id = id;
        return categoria;
    }

    private static Marca MarcaConId(int id, string nombre)
    {
        var marca = Marca.Crear(nombre);
        marca.Id = id;
        return marca;
    }
}
