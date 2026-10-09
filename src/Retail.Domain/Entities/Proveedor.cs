using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un distribuidor o proveedor mayorista.
/// Sus datos solo cambian a través de <see cref="Crear"/> y <see cref="ActualizarDatos"/>, que los normalizan y
/// validan con las reglas de <c>Retail.Domain.Common</c>.
/// </summary>
public class Proveedor : BaseEntity, IAggregateRoot
{
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
}
