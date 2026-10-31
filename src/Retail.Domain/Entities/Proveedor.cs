using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un distribuidor o proveedor mayorista.
/// Sus datos solo cambian a través de <see cref="Crear"/> y <see cref="ActualizarDatos"/>, que los normalizan y
/// validan con las reglas de <c>Retail.Domain.Common</c>.
/// </summary>
public class Proveedor : BaseEntity, IAggregateRoot
{
    private readonly List<EquivalenciaClasificacion> _equivalencias = new();

    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    private Proveedor()
    {
    }

    public string RazonSocial { get; private set; } = string.Empty;

    /// <summary>
    /// Siempre 11 dígitos, sin guiones: guardarlo en una única forma es lo que permite que el índice único
    /// detecte que "20-12345678-6" y "20123456786" son el mismo proveedor.
    /// </summary>
    public string Cuit { get; private set; } = string.Empty;

    public string? Telefono { get; private set; }

    public string? Email { get; private set; }

    public ICollection<CatalogoProveedor> Catalogos { get; set; } = new List<CatalogoProveedor>();

    public ICollection<Compra> Compras { get; set; } = new List<Compra>();

    /// <summary>
    /// Reglas de traducción de la clasificación de este proveedor (Track 6.6, M-13), vigentes y dadas de baja en esta
    /// unidad de trabajo; las dadas de baja no cuentan para las reglas.
    /// </summary>
    public IReadOnlyCollection<EquivalenciaClasificacion> Equivalencias => _equivalencias.AsReadOnly();

    /// <summary>
    /// Método de creación del agregado: es la única forma de dar de alta un proveedor.
    /// </summary>
    public static Proveedor Crear(string razonSocial, string cuit, string? telefono = null, string? email = null)
    {
        var proveedor = new Proveedor();
        proveedor.ActualizarDatos(razonSocial, cuit, telefono, email);
        return proveedor;
    }

    public void ActualizarDatos(string razonSocial, string cuit, string? telefono = null, string? email = null)
    {
        // Se validan todos los valores antes de asignar alguno: el agregado nunca queda a medio actualizar.
        var razonSocialNormalizada = ReglasTexto.ExigirRazonSocial(razonSocial, "razón social del proveedor");
        var cuitNormalizado = ReglasDocumento.Exigir(Enums.TipoDocumentoEnum.Cuit, cuit);
        var telefonoNormalizado = ReglasContacto.ExigirTelefonoOpcional(telefono);
        var emailNormalizado = ReglasContacto.ExigirEmailOpcional(email);

        RazonSocial = razonSocialNormalizada;
        Cuit = cuitNormalizado;
        Telefono = telefonoNormalizado;
        Email = emailNormalizado;
    }

    /// <summary>
    /// Traduce un valor de la clasificación del proveedor a una categoría del comercio, o lo ignora si
    /// <paramref name="idCategoria"/> es nulo. Si la equivalencia ya existe, reemplaza su destino (M-32). Que la
    /// categoría exista y esté activa lo verifica la capa de Aplicación.
    /// </summary>
    /// <param name="textoRubro">Rubro del proveedor como contexto (M-31); vacío si la planilla no lo trae.</param>
    /// <param name="texto">Categoría del proveedor, o su rubro si es lo único que trae la planilla.</param>
    public EquivalenciaClasificacion AsignarEquivalenciaCategoria(string? textoRubro, string texto, int? idCategoria)
    {
        ExigirIdentificadorOpcional(idCategoria, "La categoría de la equivalencia no es válida.");
        var (rubro, valor) = NormalizarClaveDeCategoria(textoRubro, texto);
        return Asignar(TipoEquivalenciaEnum.Categoria, rubro, valor, idCategoria, idMarca: null);
    }

    /// <summary>
    /// Traduce una marca escrita por el proveedor a una marca del comercio, o la ignora si <paramref name="idMarca"/>
    /// es nulo. Si la equivalencia ya existe, reemplaza su destino (M-32).
    /// </summary>
    public EquivalenciaClasificacion AsignarEquivalenciaMarca(string texto, int? idMarca)
    {
        ExigirIdentificadorOpcional(idMarca, "La marca de la equivalencia no es válida.");
        var valor = ReglasClasificacion.ExigirTextoDeProveedor(texto, "texto de marca del proveedor");
        return Asignar(TipoEquivalenciaEnum.Marca, textoRubro: null, valor, idCategoria: null, idMarca);
    }

    /// <summary>
    /// Da de baja la equivalencia (borrado lógico, Ley 3): la próxima importación vuelve a ofrecer el valor para
    /// traducirlo.
    /// </summary>
    public void QuitarEquivalencia(TipoEquivalenciaEnum tipo, string? textoRubro, string texto)
    {
        var (rubro, valor) = tipo == TipoEquivalenciaEnum.Categoria
            ? NormalizarClaveDeCategoria(textoRubro, texto)
            : (null, ReglasClasificacion.ExigirTextoDeProveedor(texto, "texto de marca del proveedor"));

        var existente = BuscarEquivalenciaVigente(tipo, rubro, valor)
            ?? throw new DomainException($"El proveedor no tiene una equivalencia para '{valor}'.");
        existente.MarkAsDeleted();
    }

    /// <summary>
    /// Da de baja las equivalencias vigentes que traducen a la categoría o la marca indicada (M-35): sin su destino
    /// no tienen sentido, y la próxima importación vuelve a ofrecer esos valores para traducirlos. Las ignoradas no
    /// apuntan a ningún destino y no se tocan. Devuelve cuántas se dieron de baja.
    /// </summary>
    public int QuitarEquivalenciasHacia(TipoEquivalenciaEnum tipo, int idDestino)
    {
        if (idDestino <= 0)
        {
            throw new DomainException("El destino de las equivalencias no es válido.");
        }

        var afectadas = _equivalencias
            .Where(e => !e.IsDeleted && e.Tipo == tipo)
            .Where(e => (tipo == TipoEquivalenciaEnum.Categoria ? e.IdCategoria : e.IdMarca) == idDestino)
            .ToList();

        foreach (var equivalencia in afectadas)
        {
            equivalencia.MarkAsDeleted();
        }

        return afectadas.Count;
    }

    private EquivalenciaClasificacion Asignar(
        TipoEquivalenciaEnum tipo,
        string? textoRubro,
        string texto,
        int? idCategoria,
        int? idMarca)
    {
        var equivalencia = BuscarEquivalenciaVigente(tipo, textoRubro, texto);
        if (equivalencia is null)
        {
            equivalencia = new EquivalenciaClasificacion(tipo, textoRubro, texto);
            _equivalencias.Add(equivalencia);
        }

        equivalencia.CambiarDestino(idCategoria, idMarca);
        return equivalencia;
    }

    private EquivalenciaClasificacion? BuscarEquivalenciaVigente(TipoEquivalenciaEnum tipo, string? textoRubro, string texto)
    {
        return _equivalencias.FirstOrDefault(e => !e.IsDeleted && e.TieneClave(tipo, textoRubro, texto));
    }

    private static (string? Rubro, string Texto) NormalizarClaveDeCategoria(string? textoRubro, string texto)
    {
        var rubro = string.IsNullOrWhiteSpace(textoRubro)
            ? null
            : ReglasClasificacion.ExigirTextoDeProveedor(textoRubro, "rubro del proveedor");
        var valor = ReglasClasificacion.ExigirTextoDeProveedor(texto, "texto de categoría del proveedor");
        return (rubro, valor);
    }

    private static void ExigirIdentificadorOpcional(int? id, string mensaje)
    {
        if (id.HasValue && id.Value <= 0)
        {
            throw new DomainException(mensaje);
        }
    }
}
