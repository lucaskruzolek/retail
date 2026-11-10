using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Simula meses de operación para demostrar las funciones y los reportes del sistema (RF-24, RF-25). Cada operación
/// pasa por los métodos del Dominio, que calculan totales, stock y saldos; lo único que se reescribe son sus fechas.
/// </summary>
/// <remarks>
/// El Dominio fecha las ventas y los turnos con el reloj del sistema y no admite otra fecha, a propósito: una venta
/// real no se puede antedatar. Por eso el pasado se fabrica acá, en Infrastructure, a través del Change Tracker de
/// EF Core, que escribe la propiedad aunque su setter sea privado. El modelo no gana ninguna puerta para antedatar.
/// <para>
/// Todo corre en una única transacción: si algo falla, la base queda como estaba. Se rechaza una base que ya tenga
/// operaciones, porque <c>Retail.Cli</c> viaja con la aplicación y un historial ficticio no debe mezclarse con uno real.
/// </para>
/// </remarks>
public static class SemilleroHistorialDemo
{
    /// <summary>Meses simulados: alcanzan para agrupar por mes y comparar contra el período anterior.</summary>
    internal const int MesesDeHistorial = 6;

    // Semilla fija: dos ejecuciones con la misma fecha generan el mismo historial, que así se puede explicar y defender.
    private const int SemillaDelAzar = 20261110;

    // Demanda semanal total estimada, para repartirla entre los artículos según su popularidad.
    private const double UnidadesVendidasPorSemana = 480d;

    private const double PesoDePresentacion = 0.25d;

    /// <summary>
    /// Genera el historial hasta el día anterior a <paramref name="hoy"/> y devuelve lo creado.
    /// </summary>
    /// <param name="hoy">Fecha local: el historial se calcula hacia atrás para que el tablero siempre tenga datos recientes.</param>
    /// <param name="zona">Zona horaria local: las ventas ocurren en horario de comercio y se guardan en UTC.</param>
    public static async Task<ResultadoHistorialDemo> CargarAsync(
        RetailDbContext context,
        IPasswordHasher hasher,
        DateOnly hoy,
        TimeZoneInfo zona,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(zona);

        await VerificarBaseSinOperacionesAsync(context, cancellationToken);
        var idGerente = await ObtenerGerenteAsync(context, cancellationToken);

        await using var transaccion = await context.Database.BeginTransactionAsync(cancellationToken);

        // Es idempotente: garantiza las categorías y marcas del catálogo, se hayan cargado antes o no.
        await SemilleroDatosDemo.CargarAsync(context, cancellationToken);

        var desde = hoy.AddMonths(-MesesDeHistorial);
        var alta = AUtc(desde.AddDays(-1), new TimeOnly(9, 0), zona);
        var azar = new Random(SemillaDelAzar);

        var idProveedorEscolar = await CrearProveedorAsync(context, "Mayorista Escolar Demo S.A.", CatalogoHistorialDemo.CuitProveedorEscolar, alta, cancellationToken);
        var idProveedorBazar = await CrearProveedorAsync(context, "Bazar y Juguetes Demo S.R.L.", CatalogoHistorialDemo.CuitProveedorBazar, alta, cancellationToken);
        var idsCajeros = await CrearCajerosAsync(context, hasher, alta, cancellationToken);
        var clientes = await CrearClientesAsync(context, alta, cancellationToken);
        var articulos = await CrearArticulosAsync(context, azar, idProveedorEscolar, idProveedorBazar, alta, cancellationToken);

        var simulacion = new SimulacionHistorialDemo(
            context,
            azar,
            desde,
            hoy,
            zona,
            idGerente,
            idsCajeros,
            idProveedorEscolar,
            idProveedorBazar,
            articulos,
            clientes.IdsMorosos);
        var resultado = await simulacion.EjecutarAsync(clientes.Cantidad, cancellationToken);

        await transaccion.CommitAsync(cancellationToken);
        return resultado;
    }

    /// <summary>
    /// Fija la fecha de una venta en seguimiento y la de creación de sus ítems y pagos, que nacieron con ella.
    /// </summary>
    internal static void FijarFechaDeVenta(RetailDbContext context, Venta venta, DateTime fechaUtc)
    {
        context.Entry(venta).Property(v => v.FechaHora).CurrentValue = fechaUtc;
        venta.CreatedAt = fechaUtc;

        foreach (var detalle in venta.Detalles)
        {
            detalle.CreatedAt = fechaUtc;
        }

        foreach (var pago in venta.Pagos)
        {
            pago.CreatedAt = fechaUtc;
        }
    }

    /// <summary>
    /// Fija la apertura y el cierre de un turno en seguimiento que ya pasó por su arqueo.
    /// </summary>
    internal static void FijarFechasDeTurno(RetailDbContext context, TurnoCaja turno, DateTime aperturaUtc, DateTime cierreUtc)
    {
        var entrada = context.Entry(turno);
        entrada.Property(t => t.FechaApertura).CurrentValue = aperturaUtc;
        entrada.Property(t => t.FechaCierre).CurrentValue = cierreUtc;
        turno.CreatedAt = aperturaUtc;
    }

    /// <summary>
    /// Fija las fechas del ciclo de un pedido en seguimiento. Una fecha nula corresponde a un estado no alcanzado.
    /// </summary>
    internal static void FijarFechasDePedido(
        RetailDbContext context,
        Pedido pedido,
        DateTime creacionUtc,
        DateTime? emisionUtc,
        DateTime? cierreUtc)
    {
        var entrada = context.Entry(pedido);
        entrada.Property(p => p.FechaEmision).CurrentValue = emisionUtc;
        entrada.Property(p => p.FechaCierre).CurrentValue = cierreUtc;
        pedido.CreatedAt = creacionUtc;

        foreach (var detalle in pedido.Detalles)
        {
            detalle.CreatedAt = creacionUtc;
        }
    }

    internal static DateTime AUtc(DateOnly dia, TimeOnly hora, TimeZoneInfo zona)
    {
        return TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(hora), zona);
    }

    private static async Task VerificarBaseSinOperacionesAsync(RetailDbContext context, CancellationToken cancellationToken)
    {
        // Sin filtros globales: una venta dada de baja también prueba que la base tuvo operaciones reales.
        var tieneOperaciones = await context.Ventas.IgnoreQueryFilters().AnyAsync(cancellationToken)
            || await context.TurnosCaja.IgnoreQueryFilters().AnyAsync(cancellationToken);

        if (tieneOperaciones)
        {
            throw new DomainException(
                "La base ya tiene ventas o turnos de caja: el historial de demostración solo se carga en una base sin operaciones.");
        }
    }

    private static async Task<int> ObtenerGerenteAsync(RetailDbContext context, CancellationToken cancellationToken)
    {
        var idGerente = await context.Usuarios
            .Where(u => u.IdRol == (int)RolUsuarioEnum.Gerente)
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return idGerente > 0
            ? idGerente
            : throw new DomainException("No hay un Gerente activo, que registra las compras. Ejecute primero: Retail.Cli inicializar");
    }

    private static async Task<int> CrearProveedorAsync(
        RetailDbContext context,
        string razonSocial,
        string cuit,
        DateTime alta,
        CancellationToken cancellationToken)
    {
        var proveedor = Proveedor.Crear(razonSocial, cuit, "(011) 4555-0300", "ventas@mayorista.example.com");
        proveedor.CreatedAt = alta;

        context.Proveedores.Add(proveedor);
        await context.SaveChangesAsync(cancellationToken);
        return proveedor.Id;
    }

    private static async Task<IReadOnlyList<int>> CrearCajerosAsync(
        RetailDbContext context,
        IPasswordHasher hasher,
        DateTime alta,
        CancellationToken cancellationToken)
    {
        var cajeros = new List<Usuario>();

        foreach (var (nombreUsuario, nombre, apellido) in CatalogoHistorialDemo.Cajeros)
        {
            // Contraseña aleatoria que nadie conoce: para usar la cuenta, un Gerente la blanquea desde Usuarios (D-13).
            var hash = hasher.HashPassword(RandomNumberGenerator.GetHexString(32));
            var cajero = Usuario.Crear(nombreUsuario, nombre, apellido, hash, RolUsuarioEnum.Cajero, null);
            cajero.CreatedAt = alta;
            cajeros.Add(cajero);
        }

        context.Usuarios.AddRange(cajeros);
        await context.SaveChangesAsync(cancellationToken);
        return cajeros.Select(c => c.Id).ToList();
    }

    private static async Task<(int Cantidad, IReadOnlySet<int> IdsMorosos)> CrearClientesAsync(
        RetailDbContext context,
        DateTime alta,
        CancellationToken cancellationToken)
    {
        var clientes = new List<(Cliente Cliente, bool EsMoroso)>();

        foreach (var datos in CatalogoHistorialDemo.Clientes)
        {
            var cliente = Cliente.Crear(datos.Nombre, datos.TipoDocumento, datos.NumeroDocumento, datos.CondicionIva, "Av. Demostración 1234, CABA", datos.Telefono, datos.Email);
            cliente.CreatedAt = alta;

            if (datos.LimiteCredito is decimal limite)
            {
                cliente.HabilitarCuentaCorriente(limite);
            }

            clientes.Add((cliente, datos.EsMoroso));
        }

        context.Clientes.AddRange(clientes.Select(c => c.Cliente));
        await context.SaveChangesAsync(cancellationToken);
        return (clientes.Count, clientes.Where(c => c.EsMoroso).Select(c => c.Cliente.Id).ToHashSet());
    }

    private static async Task<IReadOnlyList<ArticuloSimulado>> CrearArticulosAsync(
        RetailDbContext context,
        Random azar,
        int idProveedorEscolar,
        int idProveedorBazar,
        DateTime alta,
        CancellationToken cancellationToken)
    {
        var idCategoriaPorForma = (await context.Categorias.Select(c => new { c.Id, c.NombreCategoria }).ToListAsync(cancellationToken))
            .ToDictionary(c => ReglasClasificacion.NormalizarTexto(c.NombreCategoria), c => c.Id);
        var idMarcaPorForma = (await context.Marcas.Select(m => new { m.Id, m.NombreMarca }).ToListAsync(cancellationToken))
            .ToDictionary(m => ReglasClasificacion.NormalizarTexto(m.NombreMarca), m => m.Id);

        var ordenPorPopularidad = CatalogoHistorialDemo.Productos.ToArray();
        azar.Shuffle(ordenPorPopularidad);

        var pesos = ordenPorPopularidad.Select((_, indice) => 1d / Math.Pow(indice + 1, 0.8d)).ToArray();
        var pesoTotal = pesos.Sum() + (CatalogoHistorialDemo.Presentaciones.Count * PesoDePresentacion);

        var creados = new List<(ProductoDemo Producto, Articulo Articulo, PerfilDemandaDemo Perfil, double Peso)>();

        for (var i = 0; i < ordenPorPopularidad.Length; i++)
        {
            var producto = ordenPorPopularidad[i];
            var perfil = ElegirPerfil(producto, i, ordenPorPopularidad.Length);
            var (minimo, objetivo) = CalcularStock(pesos[i], pesoTotal);

            var articulo = new Articulo
            {
                CodigoBarras = producto.EsServicio ? null : GenerarCodigoDeBarras(i + 1),
                Descripcion = producto.Descripcion,
                CostoReposicion = producto.CostoInicial,
                PorcentajeGanancia = producto.PorcentajeGanancia,
                PrecioVenta = Articulo.CalcularPrecioVenta(producto.CostoInicial, producto.PorcentajeGanancia),
                StockActual = producto.EsServicio ? 0 : objetivo,
                StockMinimo = producto.EsServicio ? 0 : minimo,
                EsServicio = producto.EsServicio,
                CreatedAt = alta
            };

            int? idMarca = producto.Marca is null ? null : idMarcaPorForma[ReglasClasificacion.NormalizarTexto(producto.Marca)];
            articulo.Clasificar(
                idCategoriaPorForma[ReglasClasificacion.NormalizarTexto(producto.Categoria)],
                OrigenClasificacionEnum.Manual,
                idMarca,
                OrigenClasificacionEnum.Manual);

            creados.Add((producto, articulo, perfil, pesos[i]));
        }

        context.Articulos.AddRange(creados.Select(c => c.Articulo));
        await context.SaveChangesAsync(cancellationToken);

        var fuentes = new Dictionary<Articulo, CatalogoProveedor>();
        var proveedorPorArticulo = new Dictionary<Articulo, int>();

        foreach (var (producto, articulo, _, _) in creados.Where(c => !c.Producto.EsServicio))
        {
            var idProveedor = CatalogoHistorialDemo.EsDelMayoristaEscolar(producto.Categoria) ? idProveedorEscolar : idProveedorBazar;
            var fuente = new CatalogoProveedor
            {
                IdProveedor = idProveedor,
                CodigoProveedor = $"DEMO-{articulo.Id:D4}",
                CodigoBarras = articulo.CodigoBarras,
                DescripcionProveedor = articulo.Descripcion,
                CostoReposicion = articulo.CostoReposicion,
                FechaActualizacion = alta,
                CreatedAt = alta
            };
            fuente.AsignarArticulo(articulo);

            fuentes.Add(articulo, fuente);
            proveedorPorArticulo.Add(articulo, idProveedor);
        }

        context.CatalogosProveedores.AddRange(fuentes.Values);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var (articulo, fuente) in fuentes)
        {
            articulo.MarcarFuentePrincipal(fuente);
        }

        var presentaciones = CrearPresentaciones(creados.Select(c => c.Articulo), pesoTotal, alta, context);
        await context.SaveChangesAsync(cancellationToken);

        var simulados = creados
            .Select(c => new ArticuloSimulado(
                c.Articulo.Id,
                c.Articulo.Descripcion,
                c.Perfil,
                c.Peso,
                UnidadesPorSemana(c.Peso, pesoTotal),
                c.Articulo.StockMinimo,
                c.Producto.EsServicio ? 0 : CalcularStock(c.Peso, pesoTotal).Objetivo,
                proveedorPorArticulo.TryGetValue(c.Articulo, out var idProveedor) ? idProveedor : null,
                fuentes.TryGetValue(c.Articulo, out var fuente) ? fuente.Id : null,
                fuentes.TryGetValue(c.Articulo, out var fuenteConCodigo) ? fuenteConCodigo.CodigoProveedor : null,
                null,
                null))
            .ToList();

        simulados.AddRange(presentaciones.Select(p => new ArticuloSimulado(
            p.Derivado.Id,
            p.Derivado.Descripcion,
            PerfilDemandaDemo.Regular,
            PesoDePresentacion,
            UnidadesPorSemana(PesoDePresentacion, pesoTotal),
            p.Derivado.StockMinimo,
            CalcularStock(PesoDePresentacion, pesoTotal).Objetivo,
            null,
            null,
            null,
            p.Derivado.IdArticuloOrigen,
            p.Derivado.UnidadesPorOrigen)));

        return simulados;
    }

    private static List<(Articulo Derivado, PresentacionDemo Datos)> CrearPresentaciones(
        IEnumerable<Articulo> articulos,
        double pesoTotal,
        DateTime alta,
        RetailDbContext context)
    {
        var porDescripcion = articulos.ToDictionary(a => a.Descripcion, StringComparer.Ordinal);
        var (minimo, objetivo) = CalcularStock(PesoDePresentacion, pesoTotal);
        var presentaciones = new List<(Articulo Derivado, PresentacionDemo Datos)>();

        foreach (var datos in CatalogoHistorialDemo.Presentaciones)
        {
            var derivado = new Articulo
            {
                Descripcion = datos.Descripcion,
                PorcentajeGanancia = datos.PorcentajeGanancia,
                StockActual = objetivo,
                StockMinimo = minimo,
                CreatedAt = alta
            };

            var origen = porDescripcion[datos.DescripcionOrigen];
            derivado.DefinirComoPresentacionDe(origen, datos.UnidadesPorOrigen);
            derivado.Clasificar(origen.IdCategoria, origen.OrigenCategoria, origen.IdMarca, origen.OrigenMarca);

            context.Articulos.Add(derivado);
            presentaciones.Add((derivado, datos));
        }

        return presentaciones;
    }

    /// <summary>
    /// Reparte los perfiles sobre el orden de popularidad: los que quedan sin reponer se toman salteados, para que el
    /// stock crítico mezcle artículos de mucha y de poca venta; los de cola no se venden o se discontinúan.
    /// </summary>
    private static PerfilDemandaDemo ElegirPerfil(ProductoDemo producto, int indice, int total)
    {
        // El origen de una presentación sigue en circulación: si no se repusiera, la unidad suelta se quedaría sin stock.
        if (producto.EsServicio || CatalogoHistorialDemo.Presentaciones.Any(p => p.DescripcionOrigen == producto.Descripcion))
        {
            return PerfilDemandaDemo.Regular;
        }

        if (indice >= total - 3)
        {
            return PerfilDemandaDemo.SinVentas;
        }

        if (indice >= total - 10)
        {
            return PerfilDemandaDemo.Discontinuado;
        }

        return indice % 6 == 2 && indice < 72 ? PerfilDemandaDemo.SinReposicionFinal : PerfilDemandaDemo.Regular;
    }

    private static int UnidadesPorSemana(double peso, double pesoTotal)
    {
        return (int)Math.Ceiling(peso / pesoTotal * UnidadesVendidasPorSemana);
    }

    // El mínimo cubre unos días de demanda y el objetivo, unas tres semanas: reponiendo cada semana rara vez se llega al mínimo.
    private static (int Minimo, int Objetivo) CalcularStock(double peso, double pesoTotal)
    {
        var porSemana = UnidadesPorSemana(peso, pesoTotal);
        var minimo = Math.Max(3, (int)Math.Ceiling(porSemana * 0.6d));
        return (minimo, Math.Max(8, (porSemana * 3) + minimo));
    }

    // Prefijo argentino 779 con un titular que no figura en los diccionarios y dígito verificador GS1 válido.
    private static string GenerarCodigoDeBarras(int secuencia)
    {
        var cuerpo = $"7799990{secuencia:D5}";
        var digito = Enumerable.Range(0, 10).First(d => ReglasCodigoBarras.TieneDigitoVerificadorValido($"{cuerpo}{d}"));
        return $"{cuerpo}{digito}";
    }
}
