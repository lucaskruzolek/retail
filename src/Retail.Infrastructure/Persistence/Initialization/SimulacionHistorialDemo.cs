using Microsoft.EntityFrameworkCore;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Services;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Recorre los días del historial en orden y opera cada uno como lo haría el comercio: lista de precios del mes,
/// reposición, fraccionamiento, turnos de caja con sus ventas y cierre con arqueo.
/// </summary>
/// <remarks>
/// El orden cronológico es lo que mantiene la coherencia: cada venta encuentra el stock que dejaron las compras y las
/// ventas anteriores y toma la foto del costo de ese momento (RF-25). Las secuencias replican las de los servicios de
/// Application, que no se usan directamente porque abren el turno vigente y fechan con el reloj real.
/// </remarks>
internal sealed class SimulacionHistorialDemo
{
    private const int DiasSinReponerAlFinal = 35;
    private const int DiasDesdeLaUltimaVentaDiscontinuada = 110;
    private const int VentasBasePorTurno = 12;
    private const decimal SaldoInicialDeCaja = 25000m;
    private const decimal MontoRetenidoMaximo = 25000m;

    private static readonly (MedioPagoEnum Medio, int Peso)[] MediosDePago =
    [
        (MedioPagoEnum.Efectivo, 40),
        (MedioPagoEnum.TarjetaDebito, 25),
        (MedioPagoEnum.TarjetaCredito, 15),
        (MedioPagoEnum.TransferenciaQr, 20)
    ];

    private static readonly (int Items, int Peso)[] ItemsPorVenta = [(1, 35), (2, 30), (3, 20), (4, 10), (5, 5)];

    private static readonly decimal[] DiferenciasDeArqueo = [-800m, -300m, -100m, 50m, 200m, 500m];

    private readonly RetailDbContext _context;
    private readonly Random _azar;
    private readonly DateOnly _desde;
    private readonly DateOnly _hoy;
    private readonly TimeZoneInfo _zona;
    private readonly int _idGerente;
    private readonly IReadOnlyList<int> _idsCajeros;
    private readonly int _idProveedorEscolar;
    private readonly int _idProveedorBazar;
    private readonly IReadOnlyList<ArticuloSimulado> _articulos;
    private readonly IReadOnlyDictionary<int, ArticuloSimulado> _simuladoPorId;
    private readonly IReadOnlySet<int> _idsMorosos;
    private readonly RedondeoPrecioEnum _redondeo;

    private Dictionary<int, Articulo> _articulosDelDia = [];
    private List<Cliente> _clientesDelDia = [];
    private decimal _saldoEnCaja = SaldoInicialDeCaja;
    private int _turnos;
    private int _ventas;
    private int _compras;
    private int _cobranzas;

    public SimulacionHistorialDemo(
        RetailDbContext context,
        Random azar,
        DateOnly desde,
        DateOnly hoy,
        TimeZoneInfo zona,
        int idGerente,
        IReadOnlyList<int> idsCajeros,
        int idProveedorEscolar,
        int idProveedorBazar,
        IReadOnlyList<ArticuloSimulado> articulos,
        IReadOnlySet<int> idsMorosos,
        RedondeoPrecioEnum redondeo)
    {
        _context = context;
        _azar = azar;
        _desde = desde;
        _hoy = hoy;
        _zona = zona;
        _idGerente = idGerente;
        _idsCajeros = idsCajeros;
        _idProveedorEscolar = idProveedorEscolar;
        _idProveedorBazar = idProveedorBazar;
        _articulos = articulos;
        _simuladoPorId = articulos.ToDictionary(a => a.Id);
        _idsMorosos = idsMorosos;
        _redondeo = redondeo;
    }

    public async Task<ResultadoHistorialDemo> EjecutarAsync(int cantidadClientes, CancellationToken cancellationToken)
    {
        var diasTotales = _hoy.DayNumber - _desde.DayNumber;
        int? mesEnCurso = null;

        for (var dia = _desde; dia < _hoy; dia = dia.AddDays(1))
        {
            if (dia.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            await CargarAgregadosDelDiaAsync(cancellationToken);

            if (mesEnCurso is not null && mesEnCurso != dia.Month)
            {
                await AplicarListaDePreciosDelMesAsync(dia, cancellationToken);
            }

            mesEnCurso = dia.Month;

            await ReponerAsync(dia, cancellationToken);
            FraccionarPresentaciones();

            var crecimiento = 0.85d + (0.35d * (dia.DayNumber - _desde.DayNumber) / diasTotales);
            foreach (var turno in TurnosDelDia(dia))
            {
                await SimularTurnoAsync(dia, turno.Apertura, turno.Cierre, turno.Factor * crecimiento, turno.Indice, cancellationToken);
            }

            // Una unidad de trabajo por día (como D-15): el Change Tracker no acumula meses de operaciones.
            _context.ChangeTracker.Clear();
        }

        await CrearPedidosAbiertosAsync(cancellationToken);

        return new ResultadoHistorialDemo(_desde, _hoy.AddDays(-1), _articulos.Count, cantidadClientes, _turnos, _ventas, _compras, _cobranzas);
    }

    private static IEnumerable<(TimeOnly Apertura, TimeOnly Cierre, double Factor, int Indice)> TurnosDelDia(DateOnly dia)
    {
        var manana = (new TimeOnly(9, 0), new TimeOnly(13, 0));
        var tarde = (new TimeOnly(16, 0), new TimeOnly(20, 0));

        // El sábado se abre solo a la mañana, pero con más movimiento.
        if (dia.DayOfWeek == DayOfWeek.Saturday)
        {
            yield return (manana.Item1, manana.Item2, 1.7d, 0);
            yield break;
        }

        var factorDelDia = dia.DayOfWeek switch
        {
            DayOfWeek.Monday => 0.85d,
            DayOfWeek.Tuesday => 0.9d,
            DayOfWeek.Friday => 1.2d,
            _ => 1d
        };

        yield return (manana.Item1, manana.Item2, factorDelDia, 0);
        yield return (tarde.Item1, tarde.Item2, factorDelDia, 1);
    }

    private async Task CargarAgregadosDelDiaAsync(CancellationToken cancellationToken)
    {
        // Lectura fresca de cada día: stock y rowversion vigentes, sin colecciones acumuladas en memoria.
        _articulosDelDia = await _context.Articulos.ToDictionaryAsync(a => a.Id, cancellationToken);
        _clientesDelDia = await _context.Clientes.OrderBy(c => c.Id).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Actualización de costos de cada mes, con el mismo efecto que importar la planilla del proveedor (RF-07): el
    /// catálogo, el artículo y sus presentaciones. Por eso una venta vieja conserva un costo menor que el actual.
    /// </summary>
    private async Task AplicarListaDePreciosDelMesAsync(DateOnly dia, CancellationToken cancellationToken)
    {
        var fecha = SemilleroHistorialDemo.AUtc(dia, new TimeOnly(8, 0), _zona);
        var fuentes = await _context.CatalogosProveedores.OrderBy(f => f.Id).ToListAsync(cancellationToken);
        var aumentoPorProveedor = fuentes
            .Select(f => f.IdProveedor)
            .Distinct()
            .ToDictionary(id => id, _ => 1.025m + (_azar.Next(0, 31) / 1000m));

        foreach (var fuente in fuentes)
        {
            if (fuente.IdArticulo is not int idArticulo)
            {
                continue;
            }

            // En la Argentina no se usan centavos: el costo aumentado de los ítems de la demo se redondea a pesos (D-38).
            var nuevoCosto = Math.Round(fuente.CostoReposicion * aumentoPorProveedor[fuente.IdProveedor], 0, MidpointRounding.AwayFromZero);
            fuente.ActualizarPrecio(nuevoCosto);
            fuente.FechaActualizacion = fecha;
            _articulosDelDia[idArticulo].ActualizarCostoYRecalcularPrecio(nuevoCosto, _redondeo);
        }

        foreach (var simulado in _articulos.Where(a => a.IdOrigen.HasValue))
        {
            _articulosDelDia[simulado.Id].RecalcularCostoDesdeOrigen(_articulosDelDia[simulado.IdOrigen!.Value].CostoReposicion, _redondeo);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Pedido al mayorista y recepción con su factura (RF-28, RF-29): lunes al escolar y miércoles al de bazar.
    /// </summary>
    private async Task ReponerAsync(DateOnly dia, CancellationToken cancellationToken)
    {
        var idProveedor = dia.DayOfWeek switch
        {
            DayOfWeek.Monday => _idProveedorEscolar,
            DayOfWeek.Wednesday => _idProveedorBazar,
            _ => 0
        };

        if (idProveedor == 0)
        {
            return;
        }

        var aReponer = _articulos
            .Where(a => a.IdProveedor == idProveedor && SeRepone(a, dia))
            .Select(a => (Simulado: a, Articulo: _articulosDelDia[a.Id]))
            .Where(x => x.Articulo.StockActual <= x.Simulado.StockMinimo + x.Simulado.UnidadesPorSemana)
            .Select(x => (x.Simulado, x.Articulo, Cantidad: x.Simulado.StockObjetivo - x.Articulo.StockActual))
            .Where(x => x.Cantidad > 0)
            .ToList();

        if (aReponer.Count == 0)
        {
            return;
        }

        var recepcion = SemilleroHistorialDemo.AUtc(dia, new TimeOnly(8, 30), _zona);

        var pedido = Pedido.CrearBorrador(idProveedor, _idGerente);
        foreach (var (simulado, articulo, cantidad) in aReponer)
        {
            pedido.AgregarRenglon(articulo.Id, simulado.IdFuente!.Value, simulado.CodigoProveedor!, simulado.Descripcion, cantidad, articulo.CostoReposicion);
        }

        pedido.Emitir();
        pedido.MarcarRecibido();
        _context.Pedidos.Add(pedido);
        SemilleroHistorialDemo.FijarFechasDePedido(_context, pedido, recepcion.AddDays(-3), recepcion.AddDays(-2), recepcion);
        await _context.SaveChangesAsync(cancellationToken);

        _compras++;
        var compra = Compra.Registrar(idProveedor, _idGerente, Compra.TipoFactura, $"0001-{_compras:D8}", recepcion, pedido.Id);
        foreach (var (_, articulo, cantidad) in aReponer)
        {
            compra.AgregarDetalle(articulo.Id, cantidad, articulo.CostoReposicion);
            articulo.IncrementarStock(cantidad);
        }

        compra.CreatedAt = recepcion;
        foreach (var detalle in compra.Detalles)
        {
            detalle.CreatedAt = recepcion;
        }

        _context.Compras.Add(compra);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private bool SeRepone(ArticuloSimulado simulado, DateOnly dia)
    {
        return simulado.Perfil switch
        {
            PerfilDemandaDemo.SinReposicionFinal => dia < _hoy.AddDays(-DiasSinReponerAlFinal),
            PerfilDemandaDemo.Discontinuado => SeVendeDiscontinuado(dia),
            PerfilDemandaDemo.SinVentas => false,
            _ => true
        };
    }

    private bool SeVendeDiscontinuado(DateOnly dia)
    {
        return dia < _hoy.AddDays(-DiasDesdeLaUltimaVentaDiscontinuada);
    }

    /// <summary>
    /// Abre packs para reponer la unidad suelta, con el servicio de dominio que usa el POS (RF-21).
    /// </summary>
    private void FraccionarPresentaciones()
    {
        foreach (var simulado in _articulos.Where(a => a.IdOrigen.HasValue))
        {
            var derivado = _articulosDelDia[simulado.Id];
            var origen = _articulosDelDia[simulado.IdOrigen!.Value];

            if (derivado.StockActual > simulado.StockMinimo || origen.StockActual < 1)
            {
                continue;
            }

            var necesarios = ServicioFraccionamiento.CalcularOrigenesNecesarios(
                simulado.StockObjetivo - derivado.StockActual,
                simulado.UnidadesPorOrigen!.Value);
            ServicioFraccionamiento.Fraccionar(origen, derivado, Math.Min(necesarios, origen.StockActual));
        }
    }

    private async Task SimularTurnoAsync(
        DateOnly dia,
        TimeOnly apertura,
        TimeOnly cierre,
        double factor,
        int indiceTurno,
        CancellationToken cancellationToken)
    {
        var aperturaUtc = SemilleroHistorialDemo.AUtc(dia, apertura, _zona);
        var cierreUtc = SemilleroHistorialDemo.AUtc(dia, cierre, _zona);
        var idCajero = _idsCajeros[(dia.DayNumber + indiceTurno) % _idsCajeros.Count];

        // Se guarda primero: las ventas y las cobranzas referencian el Id del turno.
        var turno = TurnoCaja.Abrir(idCajero, _saldoEnCaja);
        _context.TurnosCaja.Add(turno);
        await _context.SaveChangesAsync(cancellationToken);
        _turnos++;

        var cantidadVentas = (int)Math.Round(VentasBasePorTurno * factor * (0.8d + (_azar.NextDouble() * 0.4d)));
        var minutos = Enumerable.Range(0, cantidadVentas)
            .Select(_ => _azar.Next(1, (int)(cierreUtc - aperturaUtc).TotalMinutes))
            .Order()
            .ToList();

        foreach (var minuto in minutos)
        {
            RegistrarVenta(turno, dia, aperturaUtc.AddMinutes(minuto));
        }

        RegistrarMovimientos(turno, aperturaUtc, cierreUtc);
        RegistrarCobranza(turno, aperturaUtc, cierreUtc);
        CerrarConArqueo(turno, aperturaUtc, cierreUtc);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private void RegistrarVenta(TurnoCaja turno, DateOnly dia, DateTime fechaUtc)
    {
        var lineas = ElegirLineas(dia);
        if (lineas.Count == 0)
        {
            return;
        }

        var cliente = _azar.NextDouble() < 0.25d ? _clientesDelDia[_azar.Next(_clientesDelDia.Count)] : null;
        var venta = Venta.Registrar(turno.Id, turno.IdUsuario, cliente?.Id);

        foreach (var (articulo, cantidad) in lineas)
        {
            venta.AgregarItem(articulo.Id, cantidad, articulo.PrecioVenta, articulo.CostoReposicion);
        }

        ImputarPagos(venta, cliente);
        venta.ValidarCierre();

        foreach (var (articulo, cantidad) in lineas.Where(l => !l.Articulo.EsServicio))
        {
            articulo.DescontarStock(cantidad);
        }

        if (cliente is not null && venta.TotalCuentaCorriente > 0m)
        {
            cliente.DebitarCuentaCorriente(venta.TotalCuentaCorriente);
        }

        turno.ImputarVenta(venta.TotalEfectivo, venta.TotalElectronico);

        _context.Ventas.Add(venta);
        SemilleroHistorialDemo.FijarFechaDeVenta(_context, venta, fechaUtc);
        _ventas++;
    }

    private List<(Articulo Articulo, int Cantidad)> ElegirLineas(DateOnly dia)
    {
        var vendibles = _articulos
            .Where(a => a.Perfil != PerfilDemandaDemo.SinVentas && (a.Perfil != PerfilDemandaDemo.Discontinuado || SeVendeDiscontinuado(dia)))
            .ToList();
        var pesoTotal = vendibles.Sum(a => a.Peso);
        var elegidos = new Dictionary<int, int>();

        for (var i = ElegirPonderado(ItemsPorVenta); i > 0; i--)
        {
            var simulado = ElegirArticulo(vendibles, pesoTotal);
            var articulo = _articulosDelDia[simulado.Id];
            var cantidad = ElegirCantidad(articulo);

            // Sin stock suficiente, el cliente se lleva lo que hay o el artículo no se vende.
            var disponible = articulo.EsServicio ? cantidad : Math.Min(cantidad, articulo.StockActual);
            if (disponible > 0 && !elegidos.ContainsKey(articulo.Id))
            {
                elegidos.Add(articulo.Id, disponible);
            }
        }

        return elegidos.Select(e => (_articulosDelDia[e.Key], e.Value)).ToList();
    }

    private ArticuloSimulado ElegirArticulo(List<ArticuloSimulado> vendibles, double pesoTotal)
    {
        var punto = _azar.NextDouble() * pesoTotal;

        foreach (var simulado in vendibles)
        {
            punto -= simulado.Peso;
            if (punto <= 0d)
            {
                return simulado;
            }
        }

        return vendibles[^1];
    }

    private int ElegirCantidad(Articulo articulo)
    {
        if (articulo.EsServicio)
        {
            return _azar.Next(1, 21);
        }

        return articulo.PrecioVenta switch
        {
            > 15000m => 1,
            > 1500m => _azar.Next(1, 3),
            _ => _azar.Next(1, 5)
        };
    }

    private void ImputarPagos(Venta venta, Cliente? cliente)
    {
        if (cliente is not null
            && cliente.TieneCuentaCorriente
            && cliente.CreditoDisponible >= venta.Total
            && _azar.NextDouble() < 0.5d)
        {
            venta.ImputarPago(MedioPagoEnum.CuentaCorriente, venta.Total);
            return;
        }

        var medio = ElegirPonderado(MediosDePago);

        // Cada tanto, una parte en efectivo y el resto con tarjeta.
        if (medio == MedioPagoEnum.Efectivo && venta.Total >= 2000m && _azar.NextDouble() < 0.1d)
        {
            var efectivo = ReglasMonetarias.Redondear(venta.Total / 2m);
            venta.ImputarPago(MedioPagoEnum.Efectivo, efectivo);
            venta.ImputarPago(MedioPagoEnum.TarjetaDebito, venta.Total - efectivo, "Pago combinado");
            return;
        }

        venta.ImputarPago(medio, venta.Total);
    }

    private void RegistrarMovimientos(TurnoCaja turno, DateTime aperturaUtc, DateTime cierreUtc)
    {
        if (turno.SaldoTeoricoEfectivo > 60000m && _azar.NextDouble() < 0.3d)
        {
            var monto = Math.Floor(turno.SaldoTeoricoEfectivo * 0.4m / 1000m) * 1000m;
            turno.RegistrarMovimiento(TipoMovimientoCajaEnum.RetiroExtraordinario, monto, "Retiro a tesorería");
            FecharUltimoMovimiento(turno, aperturaUtc, cierreUtc);
        }

        if (_azar.NextDouble() < 0.05d)
        {
            turno.RegistrarMovimiento(TipoMovimientoCajaEnum.IngresoExtraordinario, 10000m, "Cambio para la caja");
            FecharUltimoMovimiento(turno, aperturaUtc, cierreUtc);
        }
    }

    private void FecharUltimoMovimiento(TurnoCaja turno, DateTime aperturaUtc, DateTime cierreUtc)
    {
        var movimiento = turno.Movimientos.Last();
        movimiento.FechaHora = MomentoDelTurno(aperturaUtc, cierreUtc);
        movimiento.CreatedAt = movimiento.FechaHora;
    }

    private void RegistrarCobranza(TurnoCaja turno, DateTime aperturaUtc, DateTime cierreUtc)
    {
        if (_azar.NextDouble() >= 0.3d)
        {
            return;
        }

        var deudores = _clientesDelDia
            .Where(c => c.SaldoCuentaCorriente > 0m && !_idsMorosos.Contains(c.Id))
            .ToList();
        if (deudores.Count == 0)
        {
            return;
        }

        var cliente = deudores[_azar.Next(deudores.Count)];
        var proporcion = (decimal)(0.4d + (_azar.NextDouble() * 0.6d));
        var monto = Math.Min(cliente.SaldoCuentaCorriente, ReglasMonetarias.Redondear(cliente.SaldoCuentaCorriente * proporcion));
        var medio = _azar.Next(2) == 0 ? MedioPagoEnum.Efectivo : MedioPagoEnum.TransferenciaQr;

        var cobranza = cliente.RegistrarCobranza(turno.Id, turno.IdUsuario, monto, medio);
        turno.ImputarCobranza(monto, medio);

        cobranza.FechaHora = MomentoDelTurno(aperturaUtc, cierreUtc);
        cobranza.CreatedAt = cobranza.FechaHora;
        _cobranzas++;
    }

    private void CerrarConArqueo(TurnoCaja turno, DateTime aperturaUtc, DateTime cierreUtc)
    {
        var diferencia = _azar.NextDouble() < 0.75d ? 0m : DiferenciasDeArqueo[_azar.Next(DiferenciasDeArqueo.Length)];
        var declarado = Math.Max(0m, turno.SaldoTeoricoEfectivo + diferencia);
        var retenido = Math.Min(declarado, MontoRetenidoMaximo);

        turno.CerrarConArqueoCiego(declarado, retenido);
        SemilleroHistorialDemo.FijarFechasDeTurno(_context, turno, aperturaUtc, cierreUtc);

        // Lo que queda en la gaveta es el saldo inicial del turno siguiente.
        _saldoEnCaja = retenido;
    }

    /// <summary>
    /// Deja los pedidos que todavía no terminaron, para que la pantalla muestre cada estado además de los recibidos.
    /// </summary>
    private async Task CrearPedidosAbiertosAsync(CancellationToken cancellationToken)
    {
        await CargarAgregadosDelDiaAsync(cancellationToken);

        await CrearPedidoAsync(_idProveedorEscolar, EstadoPedidoEnum.Cancelado, 60, cancellationToken);
        await CrearPedidoAsync(_idProveedorBazar, EstadoPedidoEnum.Cancelado, 30, cancellationToken);
        await CrearPedidoAsync(_idProveedorEscolar, EstadoPedidoEnum.Emitido, 2, cancellationToken);
        await CrearPedidoAsync(_idProveedorBazar, EstadoPedidoEnum.Emitido, 1, cancellationToken);
        await CrearPedidoAsync(_idProveedorEscolar, EstadoPedidoEnum.Borrador, 1, cancellationToken);
    }

    private async Task CrearPedidoAsync(int idProveedor, EstadoPedidoEnum estado, int diasAtras, CancellationToken cancellationToken)
    {
        var fecha = SemilleroHistorialDemo.AUtc(_hoy.AddDays(-diasAtras), new TimeOnly(10, 0), _zona);
        var renglones = _articulos
            .Where(a => a.IdProveedor == idProveedor && a.Perfil == PerfilDemandaDemo.Regular)
            .Take(5);

        var pedido = Pedido.CrearBorrador(idProveedor, _idGerente);
        foreach (var simulado in renglones)
        {
            var articulo = _articulosDelDia[simulado.Id];
            pedido.AgregarRenglon(articulo.Id, simulado.IdFuente!.Value, simulado.CodigoProveedor!, simulado.Descripcion, simulado.StockObjetivo, articulo.CostoReposicion);
        }

        if (estado != EstadoPedidoEnum.Borrador)
        {
            pedido.Emitir();
        }

        if (estado == EstadoPedidoEnum.Cancelado)
        {
            pedido.Cancelar("El proveedor no tenía la mercadería disponible");
        }

        _context.Pedidos.Add(pedido);
        SemilleroHistorialDemo.FijarFechasDePedido(
            _context,
            pedido,
            fecha,
            estado == EstadoPedidoEnum.Borrador ? null : fecha.AddHours(1),
            estado == EstadoPedidoEnum.Cancelado ? fecha.AddDays(2) : null);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private DateTime MomentoDelTurno(DateTime aperturaUtc, DateTime cierreUtc)
    {
        return aperturaUtc.AddMinutes(_azar.Next(1, (int)(cierreUtc - aperturaUtc).TotalMinutes));
    }

    private T ElegirPonderado<T>((T Valor, int Peso)[] opciones)
    {
        var punto = _azar.Next(opciones.Sum(o => o.Peso));

        foreach (var (valor, peso) in opciones)
        {
            punto -= peso;
            if (punto < 0)
            {
                return valor;
            }
        }

        return opciones[^1].Valor;
    }
}
