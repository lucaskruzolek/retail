using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado de la lista de artículos (RF-30): una plantilla reutilizable de artículos y cantidades (por
/// ejemplo, una lista de útiles escolares) que se vende a muchos clientes. No guarda precios ni vence: el precio
/// siempre se lee del catálogo al imprimir o vender (decisión L-08, mismo principio que PS-08: no se guarda lo que
/// se puede calcular). Invariante I5: la lista no sabe nada de precios ni de stock.
/// </summary>
/// <remarks>
/// El control de concurrencia (<c>RowVersion</c>) no es una propiedad del dominio: se configura como propiedad
/// sombra en Infraestructura, igual que en <see cref="Pedido"/>. La unicidad del nombre entre listas activas la
/// garantiza un índice único filtrado en la base (L-10), no el agregado.
/// </remarks>
public class ListaArticulos : BaseEntity, IAggregateRoot
{
    public const int LongitudMaximaNombre = 100;

    private readonly List<RenglonLista> _renglones = new();

    public string Nombre { get; private set; } = string.Empty;

    public int IdUsuarioCreador { get; private set; }

    public Usuario? UsuarioCreador { get; private set; }

    /// <summary>
    /// Última modificación de la lista (UTC): nombre o renglones.
    /// </summary>
    public DateTime FechaModificacion { get; private set; }

    public IReadOnlyCollection<RenglonLista> Renglones => _renglones.AsReadOnly();

    protected ListaArticulos() { }

    public static ListaArticulos Crear(string nombre, int idUsuarioCreador, IEnumerable<(int IdArticulo, int Cantidad)> renglones)
    {
        if (idUsuarioCreador <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idUsuarioCreador), "La lista debe registrar al usuario que la crea.");
        }

        ArgumentNullException.ThrowIfNull(renglones);

        var nombreNormalizado = ExigirNombre(nombre);
        var agrupados = Agrupar(renglones);

        var ahora = DateTime.UtcNow;
        var lista = new ListaArticulos
        {
            Nombre = nombreNormalizado,
            IdUsuarioCreador = idUsuarioCreador,
            CreatedAt = ahora,
            FechaModificacion = ahora
        };

        foreach (var (idArticulo, cantidad) in agrupados)
        {
            lista._renglones.Add(new RenglonLista(idArticulo, cantidad));
        }

        return lista;
    }

    public void Renombrar(string nombre)
    {
        Nombre = ExigirNombre(nombre);
        FechaModificacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Agrega un artículo a la lista. Si ya figura, suma la cantidad a la misma línea.
    /// </summary>
    public void AgregarRenglon(int idArticulo, int cantidad)
    {
        ExigirIdArticulo(idArticulo);
        ExigirCantidad(cantidad);

        var existente = _renglones.FirstOrDefault(r => r.IdArticulo == idArticulo);
        if (existente is null)
        {
            _renglones.Add(new RenglonLista(idArticulo, cantidad));
        }
        else
        {
            existente.SumarCantidad(cantidad);
        }

        FechaModificacion = DateTime.UtcNow;
    }

    public void CambiarCantidad(int idArticulo, int cantidad)
    {
        ExigirCantidad(cantidad);

        ObtenerRenglon(idArticulo).CambiarCantidad(cantidad);
        FechaModificacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Quita un renglón de la lista. Una lista no puede quedar vacía: para descartarla se la da de baja
    /// lógicamente. El dominio solo saca el renglón de la colección: al guardar, la persistencia convierte el
    /// renglón huérfano en una baja lógica (Ley 3, D4 del Track 4.3), igual que <see cref="Pedido.QuitarRenglon"/>.
    /// </summary>
    public void QuitarRenglon(int idArticulo)
    {
        var renglon = ObtenerRenglon(idArticulo);

        if (_renglones.Count == 1)
        {
            throw new DomainException("La lista no puede quedar sin artículos: para descartarla, elimínela.");
        }

        _renglones.Remove(renglon);
        FechaModificacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Reemplaza el contenido de la lista por el recibido ("Actualizar «nombre»" desde el POS, L-16). Es todo o nada:
    /// primero se valida la entrada completa y, si algo es inválido, la lista no cambia. Luego se concilia en lugar
    /// de vaciar y volver a agregar: los renglones cuyo artículo sigue presente conservan su identidad y solo
    /// actualizan la cantidad, los ausentes se quitan y los artículos nuevos se agregan. Así no se borra y se inserta
    /// el mismo artículo (lo que chocaría con el índice único lista-artículo al persistir y dejaría bajas lógicas
    /// innecesarias).
    /// </summary>
    public void ReemplazarRenglones(IEnumerable<(int IdArticulo, int Cantidad)> renglones)
    {
        ArgumentNullException.ThrowIfNull(renglones);

        var agrupados = Agrupar(renglones);
        var idsNuevos = agrupados.Select(a => a.IdArticulo).ToHashSet();

        _renglones.RemoveAll(r => !idsNuevos.Contains(r.IdArticulo));

        foreach (var (idArticulo, cantidad) in agrupados)
        {
            var existente = _renglones.FirstOrDefault(r => r.IdArticulo == idArticulo);
            if (existente is null)
            {
                _renglones.Add(new RenglonLista(idArticulo, cantidad));
            }
            else
            {
                existente.CambiarCantidad(cantidad);
            }
        }

        FechaModificacion = DateTime.UtcNow;
    }

    private RenglonLista ObtenerRenglon(int idArticulo)
    {
        return _renglones.FirstOrDefault(r => r.IdArticulo == idArticulo)
            ?? throw new DomainException($"El artículo ID {idArticulo} no figura en la lista.");
    }

    private static string ExigirNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("Debe indicarse el nombre de la lista.");
        }

        var texto = ReglasTexto.NormalizarEspacios(nombre);
        if (texto.Length > LongitudMaximaNombre)
        {
            throw new DomainException($"El nombre de la lista supera los {LongitudMaximaNombre} caracteres.");
        }

        return texto;
    }

    private static void ExigirCantidad(int cantidad)
    {
        if (cantidad < 1)
        {
            throw new DomainException("La cantidad debe ser al menos 1.");
        }
    }

    private static void ExigirIdArticulo(int idArticulo)
    {
        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador del artículo no es válido.");
        }
    }

    /// <summary>
    /// Valida cada renglón y suma las cantidades de los artículos repetidos, conservando el orden de la primera
    /// aparición. Exige al menos un artículo.
    /// </summary>
    private static List<(int IdArticulo, int Cantidad)> Agrupar(IEnumerable<(int IdArticulo, int Cantidad)> renglones)
    {
        var agrupados = new List<(int IdArticulo, int Cantidad)>();

        foreach (var (idArticulo, cantidad) in renglones)
        {
            ExigirIdArticulo(idArticulo);
            ExigirCantidad(cantidad);

            var indice = agrupados.FindIndex(a => a.IdArticulo == idArticulo);
            if (indice < 0)
            {
                agrupados.Add((idArticulo, cantidad));
            }
            else
            {
                agrupados[indice] = (idArticulo, agrupados[indice].Cantidad + cantidad);
            }
        }

        if (agrupados.Count == 0)
        {
            throw new DomainException("La lista debe tener al menos un artículo.");
        }

        return agrupados;
    }
}
