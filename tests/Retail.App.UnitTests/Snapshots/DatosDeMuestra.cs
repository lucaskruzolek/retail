using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Listas;
using Retail.Application.DTOs.Presupuestos;
using Retail.Domain.Enums;

namespace Retail.App.UnitTests.Snapshots;

/// <summary>
/// Datos de muestra realistas de una librería para poblar las vistas en las capturas (<see cref="VistaSnapshotTests"/>).
/// Cubren los casos visuales relevantes: stock normal, bajo el mínimo y en cero, servicios, presentaciones derivadas y
/// artículos con varias fuentes.
/// </summary>
internal static class DatosDeMuestra
{
    public static IReadOnlyList<CategoriaDto> Categorias()
    {
        return
        [
            new CategoriaDto { IdCategoria = 1, NombreCategoria = "Cuadernos", IdRubro = 1, NombreRubro = "Papelería" },
            new CategoriaDto { IdCategoria = 2, NombreCategoria = "Escritura", IdRubro = 1, NombreRubro = "Papelería" },
            new CategoriaDto { IdCategoria = 3, NombreCategoria = "Papel para impresión", IdRubro = 2, NombreRubro = "Oficina" },
            new CategoriaDto { IdCategoria = 4, NombreCategoria = "Servicios", IdRubro = 3, NombreRubro = "Servicios" }
        ];
    }

    public static IReadOnlyList<RubroDto> Rubros()
    {
        return
        [
            new RubroDto { IdRubro = 1, NombreRubro = "Papelería" },
            new RubroDto { IdRubro = 2, NombreRubro = "Oficina" },
            new RubroDto { IdRubro = 3, NombreRubro = "Servicios" }
        ];
    }

    public static IReadOnlyList<MarcaDto> Marcas()
    {
        return
        [
            new MarcaDto { IdMarca = 1, NombreMarca = "Rivadavia" },
            new MarcaDto { IdMarca = 2, NombreMarca = "Faber-Castell" },
            new MarcaDto { IdMarca = 3, NombreMarca = "Autor" },
            new MarcaDto { IdMarca = 4, NombreMarca = "Bic" }
        ];
    }

    public static IReadOnlyList<ArticuloDto> ArticulosPagina()
    {
        return
        [
            Articulo(1, "Cuaderno Rivadavia ABC 50 hojas rayado", "7790001000011", 1, "Cuadernos", 1, "Rivadavia", 1850m, 60m, 42, 10, "Distribuidora Sur", 1),
            Articulo(2, "Lápiz negro Faber-Castell HB", "7790002000028", 2, "Escritura", 2, "Faber-Castell", 320m, 80m, 8, 24, "Distribuidora Sur", 2),
            Articulo(3, "Resma A4 Autor 75g x 500 hojas", "7790003000035", 3, "Papel para impresión", 3, "Autor", 6900m, 35m, 0, 5, "Papelera del Plata", 1),
            Articulo(4, "Birome Bic Cristal azul", "7790004000042", 2, "Escritura", 4, "Bic", 280m, 90m, 150, 50, "Distribuidora Sur", 3),
            Articulo(5, "Cuaderno Rivadavia ABC 100 hojas cuadriculado", "7790001000059", 1, "Cuadernos", 1, "Rivadavia", 2900m, 60m, 3, 8, null, 0),
            Articulo(6, "Fotocopia simple faz (servicio)", null, 4, "Servicios", null, null, 40m, 150m, 0, 0, null, 0, esServicio: true),
            Articulo(7, "Sobre manila oficio (pack x100)", "7790005000066", 3, "Papel para impresión", null, null, 4200m, 40m, 12, 4, "Papelera del Plata", 1),
            Articulo(8, "Sobre manila oficio (unidad)", null, 3, "Papel para impresión", null, null, 42m, 40m, 35, 20, "Papelera del Plata", 1, idArticuloOrigen: 7, unidadesPorOrigen: 100),
            Articulo(9, "Corrector líquido Bic Wite-Out", "7790006000073", 2, "Escritura", 4, "Bic", 950m, 70m, 18, 6, "Distribuidora Sur", 2),
            Articulo(10, "Anillado hasta 100 hojas (servicio)", null, 4, "Servicios", null, null, 1500m, 120m, 0, 0, null, 0, esServicio: true)
        ];
    }

    public static IReadOnlyList<ClienteDto> Clientes()
    {
        return
        [
            Cliente(1, "Librería Central S.R.L.", TipoDocumentoEnum.Cuit, "30712345678", CondicionIvaEnum.ResponsableInscripto, "Av. Corrientes 1234, CABA", "011 4321-5566", "compras@libreriacentral.com.ar", true, 500000m, 185000m),
            Cliente(2, "Colegio San Martín", TipoDocumentoEnum.Cuit, "30554433221", CondicionIvaEnum.Exento, "Belgrano 450, Rosario", "0341 422-7788", "administracion@sanmartin.edu.ar", true, 300000m, 0m),
            Cliente(3, "María Laura Gómez", TipoDocumentoEnum.Dni, "27456789", CondicionIvaEnum.ConsumidorFinal, null, "11 5555-1234", "mlgomez@mail.com", false, 0m, 0m),
            Cliente(4, "Estudio Contable Ferrari", TipoDocumentoEnum.Cuit, "20301234569", CondicionIvaEnum.Monotributo, "San Lorenzo 88, Córdoba", "0351 489-2200", "info@estudioferrari.com.ar", true, 120000m, 118500m),
            Cliente(5, "Juan Pablo Rossi", TipoDocumentoEnum.Dni, "33123456", CondicionIvaEnum.ConsumidorFinal, null, null, null, false, 0m, 0m),
            Cliente(6, "Papelera Los Andes S.A.", TipoDocumentoEnum.Cuit, "30698765432", CondicionIvaEnum.ResponsableInscripto, "Ruta 7 km 12, Mendoza", "0261 431-9090", "ventas@papeleralosandes.com.ar", true, 800000m, 410000m),
            Cliente(7, "Instituto Belgrano", TipoDocumentoEnum.Cuit, "30667788990", CondicionIvaEnum.Exento, "Mitre 920, La Plata", "0221 455-3030", null, true, 200000m, 45000m),
            Cliente(8, "Lucía Fernández", TipoDocumentoEnum.Dni, "38765432", CondicionIvaEnum.ConsumidorFinal, null, "11 6677-8899", "lucia.fernandez@mail.com", false, 0m, 0m)
        ];
    }

    private static ArticuloDto Articulo(
        int id,
        string descripcion,
        string? codigoBarras,
        int idCategoria,
        string categoria,
        int? idMarca,
        string? marca,
        decimal costo,
        decimal porcentajeGanancia,
        int stock,
        int stockMinimo,
        string? proveedor,
        int cantidadFuentes,
        bool esServicio = false,
        int? idArticuloOrigen = null,
        int? unidadesPorOrigen = null)
    {
        return new ArticuloDto
        {
            IdArticulo = id,
            CodigoBarras = codigoBarras,
            Descripcion = descripcion,
            IdCategoria = idCategoria,
            CategoriaNombre = categoria,
            IdMarca = idMarca,
            MarcaNombre = marca,
            IdCatalogoProveedor = proveedor is null ? null : 100 + id,
            ProveedorNombre = proveedor,
            CantidadFuentes = cantidadFuentes,
            IdArticuloOrigen = idArticuloOrigen,
            UnidadesPorOrigen = unidadesPorOrigen,
            CostoReposicion = costo,
            PorcentajeGanancia = porcentajeGanancia,
            PrecioVenta = Math.Round(costo * (1 + (porcentajeGanancia / 100m)), 0),
            StockActual = stock,
            StockMinimo = stockMinimo,
            EsServicio = esServicio
        };
    }

    private static ClienteDto Cliente(
        int id,
        string nombre,
        TipoDocumentoEnum tipoDocumento,
        string numeroDocumento,
        CondicionIvaEnum condicionIva,
        string? domicilio,
        string? telefono,
        string? email,
        bool tieneCuentaCorriente,
        decimal limiteCredito,
        decimal saldo)
    {
        return new ClienteDto
        {
            IdCliente = id,
            RazonSocialONombre = nombre,
            TipoDocumento = tipoDocumento,
            NumeroDocumento = numeroDocumento,
            CondicionIva = condicionIva,
            DomicilioFiscal = domicilio,
            Telefono = telefono,
            Email = email,
            TieneCuentaCorriente = tieneCuentaCorriente,
            LimiteCredito = limiteCredito,
            SaldoCuentaCorriente = saldo
        };
    }

    /// <summary>Día de las capturas de presupuestos: los vencimientos de la muestra se leen respecto de esta fecha.</summary>
    public static readonly DateTimeOffset AhoraPresupuestos = new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Mezcla de estados para la grilla de presupuestos: vigente con título, vencido, convertido, anulado, consumidor
    /// final y un título largo que se recorta.
    /// </summary>
    public static IReadOnlyList<PresupuestoResumenDto> Presupuestos()
    {
        return
        [
            Presupuesto(123, "Lista escolar 3.º grado — Escuela N.º 12", new DateTime(2026, 11, 14, 13, 20, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 29), "Escuela N.º 12 Domingo F. Sarmiento", 184110m, EstadoPresupuestoEnum.Pendiente, true, 5),
            Presupuesto(122, null, new DateTime(2026, 11, 2, 16, 45, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 10), null, 12900m, EstadoPresupuestoEnum.Pendiente, false, 2),
            Presupuesto(121, "Pedido de la oficina", new DateTime(2026, 11, 9, 12, 5, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 24), "Papelera San Martín S.R.L.", 96750m, EstadoPresupuestoEnum.Convertido, true, 4),
            Presupuesto(120, "Quería comparar con la competencia", new DateTime(2026, 11, 6, 19, 30, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 21), "Marcela Benítez", 5400m, EstadoPresupuestoEnum.Anulado, true, 1),
            Presupuesto(119, null, new DateTime(2026, 11, 16, 14, 10, 0, DateTimeKind.Utc), new DateOnly(2026, 12, 1), null, 38200m, EstadoPresupuestoEnum.Pendiente, true, 3),
            Presupuesto(118, "Útiles para el taller de arte y manualidades del turno tarde, segundo cuatrimestre", new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 4), "Instituto Superior de Formación Docente N.º 9", 412300m, EstadoPresupuestoEnum.Pendiente, false, 18),
            Presupuesto(117, null, new DateTime(2026, 10, 15, 15, 20, 0, DateTimeKind.Utc), new DateOnly(2026, 10, 30), "Distribuidora Sur S.R.L.", 27800m, EstadoPresupuestoEnum.Convertido, false, 2)
        ];
    }

    /// <summary>Nombre del cliente del ticket en las capturas del selector "Recuperar presupuesto".</summary>
    public const string ClienteDeLosPresupuestosPendientes = "Escuela N.º 12 Domingo F. Sarmiento";

    /// <summary>
    /// Presupuestos pendientes (vigentes y vencidos) de un mismo cliente, como los devuelve el selector "Recuperar
    /// presupuesto" prefiltrado por el cliente del ticket: uno con título largo que se recorta y uno sin título.
    /// </summary>
    public static IReadOnlyList<PresupuestoResumenDto> PresupuestosPendientesDeUnCliente()
    {
        return
        [
            Presupuesto(123, "Lista escolar 3.º grado — Escuela N.º 12", new DateTime(2026, 11, 14, 13, 20, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 29), ClienteDeLosPresupuestosPendientes, 184110m, EstadoPresupuestoEnum.Pendiente, true, 5),
            Presupuesto(112, null, new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 18), ClienteDeLosPresupuestosPendientes, 42650m, EstadoPresupuestoEnum.Pendiente, true, 4),
            Presupuesto(104, "Útiles para el taller de arte y manualidades del turno tarde, segundo cuatrimestre", new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 4), ClienteDeLosPresupuestosPendientes, 412300m, EstadoPresupuestoEnum.Pendiente, false, 18)
        ];
    }

    /// <summary>El detalle del primer presupuesto de <see cref="Presupuestos"/> (123): con título, descuento y cinco renglones.</summary>
    public static PresupuestoDto PresupuestoDetalle()
    {
        return new PresupuestoDto
        {
            IdPresupuesto = 123,
            IdUsuario = 2,
            UsuarioNombre = "Lucía Fernández",
            IdCliente = 7,
            ClienteNombre = "Escuela N.º 12 Domingo F. Sarmiento",
            Titulo = "Lista escolar 3.º grado — Escuela N.º 12",
            FechaEmision = new DateTime(2026, 11, 14, 13, 20, 0, DateTimeKind.Utc),
            FechaVencimiento = new DateOnly(2026, 11, 29),
            Subtotal = 193800m,
            Descuento = 9690m,
            Total = 184110m,
            Estado = EstadoPresupuestoEnum.Pendiente,
            EstaVigente = true,
            Items =
            [
                new DetallePresupuestoDto { IdArticulo = 1, Descripcion = "Cuaderno tapa dura 48 hojas rayado", Cantidad = 30, PrecioUnitarioPactado = 2450m },
                new DetallePresupuestoDto { IdArticulo = 2, Descripcion = "Lapicera azul Cristal", Cantidad = 50, PrecioUnitarioPactado = 480m },
                new DetallePresupuestoDto { IdArticulo = 3, Descripcion = "Block de dibujo N.º 5 de 24 hojas blancas", Cantidad = 20, PrecioUnitarioPactado = 1950m },
                new DetallePresupuestoDto { IdArticulo = 4, Descripcion = "Resma A4 75 g x 500 hojas", Cantidad = 6, PrecioUnitarioPactado = 8900m },
                new DetallePresupuestoDto { IdArticulo = 5, Descripcion = "Tijera escolar punta redonda", Cantidad = 3, PrecioUnitarioPactado = 1300m }
            ]
        };
    }

    /// <summary>
    /// Presupuesto 123 recuperado para cobrarlo en el POS. Vigente: precios pactados y cantidades como tope. Vencido: precios de
    /// catálogo, con dos diferencias (un aumento y una baja). Con <paramref name="conNovedades"/> suma un faltante de stock
    /// (con sugerencia de fraccionar) y un artículo dado de baja, para la alerta.
    /// </summary>
    public static PresupuestoParaVentaDto PresupuestoParaVenta(bool vigente, bool conNovedades = false)
    {
        var renglones = new List<RenglonPresupuestoParaVentaDto>
        {
            new()
            {
                IdArticulo = 1,
                CodigoBarras = "7790001000011",
                Descripcion = "Cuaderno tapa dura 48 hojas rayado",
                Cantidad = 3,
                PrecioPactado = 2450m,
                PrecioActual = 2700m,
                PrecioACobrar = vigente ? 2450m : 2700m,
                EsServicio = false,
                StockActual = 40
            },
            new()
            {
                IdArticulo = 2,
                CodigoBarras = "7790001000028",
                Descripcion = "Lapicera azul Cristal",
                Cantidad = 5,
                PrecioPactado = 480m,
                PrecioActual = 450m,
                PrecioACobrar = vigente ? 480m : 450m,
                EsServicio = false,
                StockActual = conNovedades ? 2 : 80
            }
        };

        var discrepancias = vigente
            ? new List<DiscrepanciaPrecioPresupuestoDto>()
            : new List<DiscrepanciaPrecioPresupuestoDto>
            {
                new() { IdArticulo = 1, Descripcion = "Cuaderno tapa dura 48 hojas rayado", PrecioPactado = 2450m, PrecioActualCatalogo = 2700m },
                new() { IdArticulo = 2, Descripcion = "Lapicera azul Cristal", PrecioPactado = 480m, PrecioActualCatalogo = 450m }
            };

        var faltantes = conNovedades
            ? new List<Retail.Application.DTOs.Ventas.FaltanteStockDto>
            {
                new()
                {
                    IdArticulo = 2,
                    Descripcion = "Lapicera azul Cristal",
                    CantidadSolicitada = 5,
                    StockActual = 2,
                    DescripcionOrigen = "Lapicera azul Cristal x 12",
                    UnidadesPorOrigen = 12,
                    StockOrigen = 4,
                    OrigenesAFraccionar = 1
                }
            }
            : new List<Retail.Application.DTOs.Ventas.FaltanteStockDto>();

        return new PresupuestoParaVentaDto
        {
            IdPresupuesto = 123,
            Titulo = "Lista escolar 3.º grado — Escuela N.º 12",
            IdCliente = null,
            ClienteNombre = null,
            EstaVigente = vigente,
            FechaVencimiento = new DateOnly(2026, 11, 29),
            Descuento = 500m,
            Renglones = renglones,
            Discrepancias = discrepancias,
            Faltantes = faltantes,
            ArticulosNoDisponibles = conNovedades ? ["Block de dibujo N.º 5 de 24 hojas blancas"] : []
        };
    }

    /// <summary>Día de las capturas de listas: las fechas de modificación se leen en hora local (UTC-3).</summary>
    public static readonly DateTimeOffset AhoraListas = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    /// <summary>Grilla de listas: un nombre largo que se recorta, una lista sin ventas y otra muy usada.</summary>
    public static IReadOnlyList<ListaResumenDto> Listas()
    {
        return
        [
            Lista(12, "3.º grado — Colegio San Martín", 14, 86450m, 9, new DateTime(2026, 11, 18, 13, 20, 0, DateTimeKind.Utc)),
            Lista(11, "Jardín sala de 5", 8, 32900m, 4, new DateTime(2026, 11, 12, 16, 45, 0, DateTimeKind.Utc)),
            Lista(10, "1.º año — Escuela Técnica N.º 3", 22, 154300m, 0, new DateTime(2026, 11, 5, 12, 5, 0, DateTimeKind.Utc)),
            Lista(9, "Taller de arte y manualidades del turno tarde, segundo cuatrimestre — Instituto Superior de Formación Docente N.º 9", 18, 98700m, 2, new DateTime(2026, 10, 28, 19, 30, 0, DateTimeKind.Utc)),
            Lista(8, "Oficina — Papelera San Martín", 5, 12800m, 31, new DateTime(2026, 10, 15, 14, 10, 0, DateTimeKind.Utc))
        ];
    }

    /// <summary>Lista abierta en el editor: tres artículos disponibles y uno dado de baja (L-13) que no suma al total.</summary>
    public static ListaDto ListaDetalle()
    {
        return new ListaDto
        {
            IdLista = 12,
            Nombre = "3.º grado — Colegio San Martín",
            FechaModificacion = new DateTime(2026, 11, 18, 13, 20, 0, DateTimeKind.Utc),
            Renglones =
            [
                RenglonLista(1, "7790001000011", "Cuaderno ABC tapa dura x 48 hojas rayado", 3, 4900m, true),
                RenglonLista(2, "7790001000028", "Lápiz negro HB Faber", 12, 450m, true),
                RenglonLista(3, "7790001000035", "Carpeta N.º 3 oficio con lomo ancho", 1, 7200m, true),
                RenglonLista(4, "7790001000042", "Block de dibujo N.º 5 de 24 hojas blancas", 2, 2600m, false)
            ],
            TotalEstimado = 27300m
        };
    }

    private static ListaResumenDto Lista(int id, string nombre, int articulos, decimal total, int ventas, DateTime modificadaUtc)
    {
        return new ListaResumenDto
        {
            IdLista = id,
            Nombre = nombre,
            CantidadArticulos = articulos,
            TotalEstimado = total,
            CantidadVentas = ventas,
            FechaModificacion = modificadaUtc
        };
    }

    private static RenglonListaDto RenglonLista(int idArticulo, string codigo, string descripcion, int cantidad, decimal precio, bool disponible)
    {
        return new RenglonListaDto
        {
            IdArticulo = idArticulo,
            CodigoBarras = codigo,
            Descripcion = descripcion,
            Cantidad = cantidad,
            PrecioActual = precio,
            Subtotal = cantidad * precio,
            EstaDisponible = disponible
        };
    }

    private static PresupuestoResumenDto Presupuesto(
        int id,
        string? titulo,
        DateTime emisionUtc,
        DateOnly vence,
        string? cliente,
        decimal total,
        EstadoPresupuestoEnum estado,
        bool vigente,
        int renglones)
    {
        return new PresupuestoResumenDto
        {
            IdPresupuesto = id,
            Titulo = titulo,
            FechaEmision = emisionUtc,
            FechaVencimiento = vence,
            ClienteNombre = cliente,
            UsuarioNombre = "Lucía Fernández",
            Total = total,
            Estado = estado,
            EstaVigente = vigente,
            CantidadRenglones = renglones
        };
    }
}
