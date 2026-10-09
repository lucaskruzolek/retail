using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un operador del sistema con credenciales protegidas y rol asignado.
/// Sus datos solo cambian a través de <see cref="Crear"/> y de los métodos de negocio, que normalizan y validan
/// cada valor con las reglas de <see cref="ReglasTexto"/>: ningún llamador puede guardar un operador inválido.
/// </summary>
public class Usuario : BaseEntity, IAggregateRoot
{
    // Lo usa EF Core para materializar la entidad desde la base; el código de negocio usa Crear.
    private Usuario()
    {
    }

    public string NombreUsuario { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string Apellido { get; private set; } = string.Empty;

    /// <summary>
    /// Nombre para mostrar en el Shell, el POS y los tickets. Es calculado y no se persiste: guardarlo
    /// duplicaría datos derivables de <see cref="Nombre"/> y <see cref="Apellido"/> (3FN).
    /// </summary>
    public string NombreCompleto => $"{Nombre} {Apellido}";

    public int IdRol { get; private set; }

    public Rol? Rol { get; private set; }

    public ICollection<TurnoCaja> TurnosCaja { get; set; } = new List<TurnoCaja>();

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    public ICollection<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();

    public ICollection<Compra> Compras { get; set; } = new List<Compra>();

    public ICollection<CobranzaCliente> CobranzasClientes { get; set; } = new List<CobranzaCliente>();

    /// <summary>
    /// Método de creación del agregado: es la única forma de dar de alta un operador.
    /// </summary>
    /// <param name="passwordHash">Hash ya calculado: el dominio nunca recibe la contraseña en texto plano.</param>
    public static Usuario Crear(
        string nombreUsuario,
        string nombre,
        string apellido,
        string passwordHash,
        RolUsuarioEnum rol)
    {
        var usuario = new Usuario
        {
            NombreUsuario = ReglasTexto.ExigirNombreUsuario(nombreUsuario)
        };

        usuario.ActualizarDatos(nombre, apellido, rol);
        usuario.ActualizarPassword(passwordHash);
        return usuario;
    }

    /// <summary>
    /// Modifica los datos personales y el rol. El nombre de usuario no cambia: es la identidad con la que el
    /// operador inicia sesión y figura en el historial de ventas y turnos.
    /// </summary>
    public void ActualizarDatos(string nombre, string apellido, RolUsuarioEnum rol)
    {
        if (!Enum.IsDefined(rol))
        {
            throw new DomainException($"El rol '{rol}' no es un rol de usuario válido.");
        }

        // Se validan todos los valores antes de asignar alguno: si el apellido es inválido, el nombre no debe
        // quedar modificado (el agregado nunca queda a medio actualizar).
        var nombreNormalizado = ReglasTexto.ExigirNombreDePersona(nombre, "nombre");
        var apellidoNormalizado = ReglasTexto.ExigirNombreDePersona(apellido, "apellido");

        Nombre = nombreNormalizado;
        Apellido = apellidoNormalizado;
        IdRol = (int)rol;
    }

    public void ActualizarPassword(string nuevoPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoPasswordHash);
        PasswordHash = nuevoPasswordHash;
    }

    public bool EsGerente() => IdRol == (int)RolUsuarioEnum.Gerente;
}
