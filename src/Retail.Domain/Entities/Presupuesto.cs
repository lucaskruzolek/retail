using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado del presupuesto comercial (RF-11, RF-12): una cotización con precios pactados que el cliente
/// puede convertir en venta. Es inmutable una vez emitido (PS-05): solo cambia de estado. Referencia al usuario y
/// al cliente por identidad; no navega hacia <see cref="Venta"/> (PS-19).
/// </summary>
/// <remarks>
/// La vigencia es derivada, no un estado (PS-08): <see cref="EstaVigente"/> compara <see cref="FechaVencimiento"/>
/// con la fecha que le pasa el llamador. El control de concurrencia (<c>RowVersion</c>) no es una propiedad del
/// dominio: se configura como propiedad sombra en Infraestructura (P3), igual que en <see cref="Pedido"/> (D-12).
/// </remarks>
public class Presupuesto : BaseEntity, IAggregateRoot
{
    public const int VigenciaPorDefectoDias = 15;
    public const int VigenciaMinimaDias = 1;
    public const int VigenciaMaximaDias = 90;
    public const int LongitudMaximaTitulo = 100;

    private readonly List<DetallePresupuesto> _detalles = new();

    public int IdUsuario { get; private set; }

    public Usuario? Usuario { get; private set; }

    /// <summary>
    /// Cliente del presupuesto; <c>null</c> es Consumidor Final, igual que en <see cref="Venta"/>.
    /// </summary>
    public int? IdCliente { get; private set; }

    public Cliente? Cliente { get; private set; }

    /// <summary>
    /// Nota interna opcional (PS-20). Nunca se entrega al cliente.
    /// </summary>
    public string? Titulo { get; private set; }

    /// <summary>
    /// Instante de emisión, en UTC.
    /// </summary>
    public DateTime FechaEmision { get; private set; }

    /// <summary>
    /// Último día de validez (inclusivo), como fecha calendario local (PS-09).
    /// </summary>
    public DateOnly FechaVencimiento { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal Descuento { get; private set; }

    public decimal Total { get; private set; }

    public EstadoPresupuestoEnum Estado { get; private set; }

    /// <summary>
    /// Cuándo terminó el presupuesto: al convertirse en venta o al anularse.
    /// </summary>
    public DateTime? FechaCierre { get; private set; }

    public IReadOnlyCollection<DetallePresupuesto> Detalles => _detalles.AsReadOnly();

    protected Presupuesto() { }

    /// <summary>
    /// Emite un presupuesto <see cref="EstadoPresupuestoEnum.Pendiente"/> con sus renglones agrupados por artículo.
    /// </summary>
    /// <param name="idUsuario">Usuario que lo emite.</param>
    /// <param name="idCliente">Cliente, o <c>null</c> para Consumidor Final.</param>
    /// <param name="titulo">Nota interna opcional; vacío o en blanco equivale a sin título.</param>
    /// <param name="renglones">Renglones a presupuestar; al menos uno.</param>
    /// <param name="descuento">Descuento total, entre 0 y el subtotal.</param>
    /// <param name="vigenciaDias">Días de validez, entre <see cref="VigenciaMinimaDias"/> y <see cref="VigenciaMaximaDias"/>.</param>
    /// <param name="hoy">
    /// Fecha calendario local de hoy. El dominio no lee el reloj para la vigencia (PS-09): la capa de aplicación
    /// la obtiene de <c>TimeProvider</c>, lo que mantiene la regla determinista y testeable.
    /// </param>
    public static Presupuesto Emitir(
        int idUsuario,
        int? idCliente,
        string? titulo,
        IReadOnlyList<RenglonAPresupuestar> renglones,
        decimal descuento,
        int vigenciaDias,
        DateOnly hoy)
    {
        if (idUsuario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuario), "El presupuesto debe registrar al usuario que lo emite.");
        }

        if (idCliente is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idCliente), "El identificador del cliente no es válido.");
        }

        ArgumentNullException.ThrowIfNull(renglones);

        var tituloNormalizado = NormalizarTitulo(titulo);

        if (vigenciaDias < VigenciaMinimaDias || vigenciaDias > VigenciaMaximaDias)
        {
            throw new DomainException($"La vigencia del presupuesto debe estar entre {VigenciaMinimaDias} y {VigenciaMaximaDias} días.");
        }

        if (renglones.Count == 0)
        {
            throw new DomainException("No se puede emitir un presupuesto sin renglones.");
        }

        var presupuesto = new Presupuesto
        {
            IdUsuario = idUsuario,
            IdCliente = idCliente,
            Titulo = tituloNormalizado,
            Estado = EstadoPresupuestoEnum.Pendiente,
            FechaEmision = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            FechaVencimiento = hoy.AddDays(vigenciaDias)
        };

        foreach (var renglon in renglones)
        {
            presupuesto.AgregarRenglon(renglon);
        }

        var subtotal = ReglasMonetarias.Redondear(presupuesto._detalles.Sum(d => d.SubtotalItem));
        if (descuento < 0m || descuento > subtotal)
        {
            throw new DomainException("El descuento del presupuesto debe estar entre cero y el subtotal.");
        }

        presupuesto.Subtotal = subtotal;
        presupuesto.Descuento = descuento;
        presupuesto.Total = subtotal - descuento;

        return presupuesto;
    }

    /// <summary>
    /// Indica si el presupuesto sigue vigente en la fecha dada. El límite es inclusivo: el último día todavía es válido.
    /// </summary>
    /// <param name="hoy">Fecha calendario local de hoy.</param>
    public bool EstaVigente(DateOnly hoy)
    {
        return hoy <= FechaVencimiento;
    }

    /// <summary>
    /// Regla que usa la venta para decidir qué precio cobrar de un artículo (PS-14, I7). Es dominio y no UI ni
    /// servicio para que exista una sola fuente de verdad.
    /// </summary>
    /// <param name="idArticulo">Artículo que se quiere cobrar.</param>
    /// <param name="cantidad">Cantidad que se quiere cobrar; al menos 1.</param>
    /// <param name="hoy">Fecha calendario local de hoy.</param>
    /// <returns>
    /// El precio pactado si el presupuesto está vigente y el artículo figura en él; <c>null</c> si corresponde cobrar
    /// el precio de catálogo (presupuesto vencido o artículo que no figura).
    /// </returns>
    /// <exception cref="DomainException">Si está vigente y la cantidad supera la presupuestada.</exception>
    public decimal? ObtenerPrecioPactado(int idArticulo, int cantidad, DateOnly hoy)
    {
        if (cantidad < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser al menos 1.");
        }

        if (!EstaVigente(hoy))
        {
            return null;
        }

        var renglon = _detalles.FirstOrDefault(d => d.IdArticulo == idArticulo);
        if (renglon is null)
        {
            return null;
        }

        if (cantidad > renglon.Cantidad)
        {
            throw new DomainException(
                $"El presupuesto incluye {renglon.Cantidad} unidades de \"{renglon.Descripcion}\" y se pretenden cobrar {cantidad}.");
        }

        return renglon.PrecioUnitarioPactado;
    }

    /// <summary>
    /// Marca el presupuesto como convertido en venta. Lo invoca la registración de la venta dentro del mismo
    /// <c>SaveChanges</c> (PS-12), igual que <see cref="Pedido.MarcarRecibido"/> en la compra.
    /// </summary>
    public void RegistrarConversion()
    {
        ExigirEstado("convertir en venta", EstadoPresupuestoEnum.Pendiente);

        Estado = EstadoPresupuestoEnum.Convertido;
        FechaCierre = DateTime.UtcNow;
    }

    /// <summary>
    /// Anula un presupuesto pendiente. No pide motivo (PS-16).
    /// </summary>
    public void Anular()
    {
        ExigirEstado("anular", EstadoPresupuestoEnum.Pendiente);

        Estado = EstadoPresupuestoEnum.Anulado;
        FechaCierre = DateTime.UtcNow;
    }

    private static string? NormalizarTitulo(string? titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            return null;
        }

        var texto = ReglasTexto.NormalizarEspacios(titulo);
        if (texto.Length > LongitudMaximaTitulo)
        {
            throw new DomainException($"El título del presupuesto supera los {LongitudMaximaTitulo} caracteres.");
        }

        return texto;
    }

    private void AgregarRenglon(RenglonAPresupuestar renglon)
    {
        if (renglon.IdArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renglon), "El identificador del artículo no es válido.");
        }

        if (renglon.Cantidad < 1)
        {
            throw new DomainException("La cantidad presupuestada debe ser al menos 1.");
        }

        if (renglon.PrecioUnitario < 0m)
        {
            throw new DomainException("El precio presupuestado no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(renglon.Descripcion))
        {
            throw new DomainException("Debe indicarse la descripción del renglón.");
        }

        var descripcion = ReglasTexto.NormalizarEspacios(renglon.Descripcion);
        if (descripcion.Length > DetallePresupuesto.LongitudMaximaDescripcion)
        {
            throw new DomainException($"La descripción del renglón supera los {DetallePresupuesto.LongitudMaximaDescripcion} caracteres.");
        }

        var existente = _detalles.FirstOrDefault(d => d.IdArticulo == renglon.IdArticulo);
        if (existente is null)
        {
            _detalles.Add(new DetallePresupuesto(renglon.IdArticulo, descripcion, renglon.Cantidad, renglon.PrecioUnitario));
            return;
        }

        if (existente.PrecioUnitarioPactado != renglon.PrecioUnitario)
        {
            throw new DomainException($"El artículo ID {renglon.IdArticulo} figura en el presupuesto con precios unitarios distintos.");
        }

        existente.SumarCantidad(renglon.Cantidad);
    }

    private void ExigirEstado(string operacion, params EstadoPresupuestoEnum[] permitidos)
    {
        if (!permitidos.Contains(Estado))
        {
            throw new TransicionPresupuestoInvalidaException(Estado, operacion);
        }
    }
}
