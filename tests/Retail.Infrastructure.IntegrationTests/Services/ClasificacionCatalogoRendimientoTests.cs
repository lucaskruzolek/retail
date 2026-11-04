using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Services;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;
using Xunit.Abstractions;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Medición de la clasificación del catálogo de un proveedor grande (Track 6.6, Fase 8; Fase A del análisis de
/// rendimiento): 30.000 ítems con descripciones armadas con la semilla real de diccionarios. Informa en la salida el
/// tiempo de cada fase, los viajes a la base, las compilaciones de SQL y los escalamientos de bloqueo de la tabla,
/// para decidir qué optimizar con datos y no con intuiciones. Con esta medición se encontró que el guardado compilaba
/// un plan por lote (M-82). Como en el importador, el tiempo no se afirma: depende de SQL Server Express y del
/// disco. Se afirman el resultado y la memoria retenida (RNF-03).
/// </summary>
public class ClasificacionCatalogoRendimientoTests : IAsyncLifetime, IDisposable
{
    private const int CantidadDeItems = 30_000;

    private readonly string _dbName = $"RetailDb_Test_ClasifRend_{Guid.NewGuid():N}";
    private readonly ITestOutputHelper _salida;
    private readonly MedidorDeComandosSql _comandos = new();
    private readonly MedidorDeGuardado _guardado = new();
    private DbContextOptions<RetailDbContext> _options = null!;
    private RetailDbContext _context = null!;

    public ClasificacionCatalogoRendimientoTests(ITestOutputHelper salida)
    {
        _salida = salida;
    }

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(_options);
        await _context.Database.CrearDesdePlantillaAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    [Trait("Category", "Rendimiento")]
    public async Task ClasificarAsync_CatalogoDe30000Items_ClasificaTodoEInformaElTiempoDeCadaFase()
    {
        // Arrange: diccionarios de la semilla versionada y un catálogo con términos, pares, marcas y prefijos reales
        var semilla = LectorSemillaDiccionarios.Leer(Path.Combine(AppContext.BaseDirectory, "Semilla"));
        await SemilleroDiccionarios.CargarAsync(_context, semilla);
        int idProveedor = await SembrarCatalogoAsync(semilla);
        _context.ChangeTracker.Clear();

        // Contexto medido: el mismo armado que en la aplicación, con los dos medidores enganchados.
        await using var medido = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>(_options)
            .AddInterceptors(_comandos, _guardado)
            .Options);
        var servicio = new ClasificacionCatalogoService(
            new Repository<Proveedor>(medido),
            new CatalogoProveedorQueryService(medido),
            new DiccionariosClasificacionQueryService(medido),
            new UnitOfWork(medido));

        // Fase 1: valores sin traducir (antes del diálogo de asociación)
        var cronometro = Stopwatch.StartNew();
        var valores = await servicio.ObtenerValoresSinTraducirAsync(idProveedor);
        Informar("1. Valores sin traducir", cronometro.Elapsed, $"{valores.Count} valores");

        // Fase 2: solo la lectura con tracking de los ítems, en un contexto aparte (lo que ClasificarAsync hace primero)
        await using (var lectura = new RetailDbContext(_options))
        {
            cronometro.Restart();
            var catalogos = await new CatalogoProveedorQueryService(lectura)
                .ObtenerParaClasificarAsync(idProveedor, soloPendientes: false);
            Informar("2. Lectura aislada de los ítems con tracking", cronometro.Elapsed, $"{catalogos.Count} entidades");
        }

        // Fase 3: la clasificación completa (botón "Clasificar catálogo")
        _comandos.Reiniciar();
        _guardado.Reiniciar();
        long escalamientosAntes = await EscalamientosDeBloqueoAsync();
        long compilacionesAntes = await CompilacionesDelServidorAsync();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long memoriaInicial = GC.GetTotalMemory(forceFullCollection: true);

        cronometro.Restart();
        var resultado = await servicio.ClasificarAsync(idProveedor, soloPendientes: false);
        var tiempoClasificacion = cronometro.Elapsed;

        long memoriaFinal = GC.GetTotalMemory(forceFullCollection: true);
        double incrementoMb = (memoriaFinal - memoriaInicial) / (1024.0 * 1024.0);
        long escalamientos = await EscalamientosDeBloqueoAsync() - escalamientosAntes;
        long compilaciones = await CompilacionesDelServidorAsync() - compilacionesAntes;
        Informar("3. ClasificarAsync (total)", tiempoClasificacion,
            $"{resultado.ItemsClasificados} ítems, {resultado.ConCategoria} con categoría, {resultado.ConMarca} con marca");
        Informar("   3a. SaveChanges (detección de cambios + lotes)", _guardado.Duracion, $"{_guardado.Guardados} guardado(s)");
        Informar("   3b. Comandos de escritura", _comandos.DuracionEscrituras, $"{_comandos.Escrituras} lotes enviados");
        Informar("   3c. Comandos de lectura (solo ejecución)", _comandos.DuracionLecturas, $"{_comandos.Lecturas} consultas");
        Informar("   3d. Resto (materialización + motor)", tiempoClasificacion - _guardado.Duracion - _comandos.DuracionLecturas, string.Empty);
        _salida.WriteLine($"   Escalamientos de bloqueo en CATALOGOS_PROVEEDORES: {escalamientos}");
        _salida.WriteLine($"   Compilaciones de SQL en el servidor: {compilaciones}");
        _salida.WriteLine($"   Memoria administrada retenida al terminar: {incrementoMb:N1} MB");
        _salida.WriteLine($"   Entidades en el Change Tracker: {medido.ChangeTracker.Entries().Count()}");
        _salida.WriteLine($"   Textos distintos entre los lotes de escritura: {_comandos.TextosDeEscritura.Distinct().Count()} (M-82: uno para los lotes completos y otro para el último)");

        // Fase 4: la recarga de la grilla que sigue a la clasificación, sin filtro y con "Sin categoría"
        await using (var grilla = new RetailDbContext(_options))
        {
            var consultas = new CatalogoProveedorQueryService(grilla);
            cronometro.Restart();
            var pagina = await consultas.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = idProveedor });
            Informar("4. Primera página de la grilla", cronometro.Elapsed, $"{pagina.TotalRegistros} registros");

            cronometro.Restart();
            var sinCategoria = await consultas.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto
            {
                IdProveedor = idProveedor,
                IdsCategoria = [ConsultaCatalogoProveedorDto.IdSinCategoria]
            });
            Informar("   4b. Filtro \"Sin categoría\"", cronometro.Elapsed, $"{sinCategoria.TotalRegistros} registros");
        }

        // Fase 5: reclasificar sin nada pendiente (lo que hace una importación que no cambió textos)
        medido.ChangeTracker.Clear();
        cronometro.Restart();
        var sinPendientes = await servicio.ClasificarAsync(idProveedor, soloPendientes: true);
        Informar("5. ClasificarAsync solo pendientes (sin pendientes)", cronometro.Elapsed, $"{sinPendientes.ItemsClasificados} ítems");

        // Assert
        resultado.ItemsClasificados.Should().Be(CantidadDeItems);
        resultado.ConCategoria.Should().BeGreaterThan(CantidadDeItems / 2, "la mayoría de las descripciones empieza con un término de la semilla");
        sinPendientes.ItemsClasificados.Should().Be(0);
        (await _context.CatalogosProveedores.CountAsync(c => c.IdProveedor == idProveedor && c.FechaClasificacion == null))
            .Should().Be(0);
        incrementoMb.Should().BeLessThan(300, "RNF-03 limita la aplicación entera a 300 MB");
    }

    private void Informar(string fase, TimeSpan duracion, string detalle)
    {
        _salida.WriteLine($"{fase}: {duracion.TotalMilliseconds:N0} ms {detalle}".TrimEnd());
    }

    /// <summary>
    /// Escalamientos de bloqueo (de fila o página a tabla) acumulados en los índices de la tabla del catálogo desde que
    /// SQL Server cargó sus metadatos. Se compara antes y después de la operación.
    /// </summary>
    private async Task<long> EscalamientosDeBloqueoAsync()
    {
        return await _context.Database
            .SqlQuery<long>($"""
                SELECT ISNULL(SUM(index_lock_promotion_count), 0) AS [Value]
                FROM sys.dm_db_index_operational_stats(DB_ID(), OBJECT_ID('CATALOGOS_PROVEEDORES'), NULL, NULL)
                """)
            .SingleAsync();
    }

    /// <summary>
    /// Compilaciones de SQL acumuladas por la instancia (contador "SQL Compilations/sec", que pese al nombre es un
    /// total). Se compara antes y después de la operación; incluye la poca actividad ajena que haya en el servidor.
    /// </summary>
    private async Task<long> CompilacionesDelServidorAsync()
    {
        return await _context.Database
            .SqlQuery<long>($"""
                SELECT cntr_value AS [Value]
                FROM sys.dm_os_performance_counters
                WHERE counter_name = 'SQL Compilations/sec' AND object_name LIKE '%SQL Statistics%'
                """)
            .SingleAsync();
    }

    /// <summary>
    /// Siembra el catálogo con una mezcla fija (semilla 42 del generador): 80 % empieza con un término de la semilla
    /// (algunos ambiguos con su segunda palabra), la mitad nombra una marca, un tercio trae un EAN con prefijo de una
    /// marca, un cuarto trae la marca en la columna del proveedor y el 20 % restante empieza con un término desconocido.
    /// </summary>
    private async Task<int> SembrarCatalogoAsync(SemillaDiccionarios semilla)
    {
        var proveedor = Proveedor.Crear("Distribuidora Grande", "30-11111111-8");
        proveedor.ConfigurarClasificacionDelCatalogo(true);
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        var aleatorio = new Random(42);
        var terminos = semilla.Terminos.Select(t => t.Termino).ToArray();
        var marcas = semilla.Marcas.Select(m => m.Marca).ToArray();
        var pares = semilla.Pares.ToArray();
        var prefijos = semilla.Prefijos.ToArray();

        var catalogos = new List<CatalogoProveedor>(CantidadDeItems);
        for (var i = 1; i <= CantidadDeItems; i++)
        {
            int tipo = aleatorio.Next(100);
            var par = pares[aleatorio.Next(pares.Length)];
            string inicio = tipo switch
            {
                < 20 => $"ZZDESCONOCIDO{aleatorio.Next(500)}",
                < 30 => $"{par.Termino} {par.SegundaPalabra}",
                _ => terminos[aleatorio.Next(terminos.Length)]
            };
            string marca = aleatorio.Next(2) == 0 ? $" {marcas[aleatorio.Next(marcas.Length)]}" : string.Empty;

            var catalogo = new CatalogoProveedor
            {
                IdProveedor = proveedor.Id,
                CodigoProveedor = $"G-{i:D6}",
                DescripcionProveedor = $"{inicio}{marca} MODELO {i}",
                CostoReposicion = 100m,
                CodigoBarras = aleatorio.Next(3) == 0 ? Ean13(prefijos[aleatorio.Next(prefijos.Length)].Prefijo, i) : null
            };

            if (aleatorio.Next(4) == 0)
            {
                catalogo.ActualizarClasificacionDelProveedor(null, null, marcas[aleatorio.Next(marcas.Length)]);
            }

            catalogos.Add(catalogo);
        }

        _context.CatalogosProveedores.AddRange(catalogos);
        await _context.SaveChangesAsync();
        return proveedor.Id;
    }

    /// <summary>EAN-13 válido: el prefijo del titular, un número de artículo y el dígito verificador que lo cierra.</summary>
    private static string Ean13(string prefijo, int numero)
    {
        // Los prefijos de la semilla tienen 6 o más dígitos: con seis del número siempre alcanzan los 12 del cuerpo.
        string cuerpo = $"{prefijo}{numero:D6}"[..12];
        return Enumerable.Range(0, 10)
            .Select(d => cuerpo + d)
            .First(ReglasCodigoBarras.TieneDigitoVerificadorValido);
    }
}
