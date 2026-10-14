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

    /// <summary>
    /// Destino de los futuros correos de recuperación de contraseña. Obligatorio para el Gerente, que es la
    /// raíz de autoridad del sistema; opcional para el resto, cuya contraseña puede blanquear otro usuario.
    /// </summary>
    public string? EmailRecuperacion { get; private set; }

    /// <summary>
    /// Indica que la contraseña vigente la eligió otra persona (alta o blanqueo) y que el titular debe
    /// reemplazarla en su próximo inicio de sesión. Evita que quien creó la cuenta pueda operar en su nombre.
    /// </summary>
    public bool DebeCambiarPassword { get; private set; }

    public int IdRol { get; private set; }

    public Rol? Rol { get; private set; }

    public ICollection<TurnoCaja> TurnosCaja { get; set; } = new List<TurnoCaja>();

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    public ICollection<Presupuesto> Presupuestos { get; set; } = new List<Presupuesto>();

    public ICollection<Compra> Compras { get; set; } = new List<Compra>();

    public ICollection<CobranzaCliente> CobranzasClientes { get; set; } = new List<CobranzaCliente>();

    /// <summary>
    /// Método de creación del agregado: es la única forma de dar de alta un operador. La contraseña inicial la
    /// elige quien da el alta, por eso la cuenta nace marcada con <see cref="DebeCambiarPassword"/>.
    /// </summary>
    /// <param name="passwordHash">Hash ya calculado: el dominio nunca recibe la contraseña en texto plano.</param>
    public static Usuario Crear(
        string nombreUsuario,
        string nombre,
        string apellido,
        string passwordHash,
        RolUsuarioEnum rol,
        string? emailRecuperacion)
    {
        var usuario = new Usuario
        {
            NombreUsuario = ReglasTexto.ExigirNombreUsuario(nombreUsuario)
        };

        usuario.ActualizarDatos(nombre, apellido, rol, emailRecuperacion);
        usuario.BlanquearPassword(passwordHash);
        return usuario;
    }

    /// <summary>
    /// Modifica los datos personales, el rol y el email de recuperación. El nombre de usuario no cambia: es la
    /// identidad con la que el operador inicia sesión y figura en el historial de ventas y turnos.
    /// El rol y el email se reciben juntos porque la invariante "un Gerente tiene email" depende de ambos.
    /// </summary>
    public void ActualizarDatos(string nombre, string apellido, RolUsuarioEnum rol, string? emailRecuperacion)
    {
        if (!Enum.IsDefined(rol))
        {
            throw new DomainException($"El rol '{rol}' no es un rol de usuario válido.");
        }

        // Se validan todos los valores antes de asignar alguno: si el apellido es inválido, el nombre no debe
        // quedar modificado (el agregado nunca queda a medio actualizar).
        var nombreNormalizado = ReglasTexto.ExigirNombreDePersona(nombre, "nombre");
        var apellidoNormalizado = ReglasTexto.ExigirNombreDePersona(apellido, "apellido");
        var emailNormalizado = ReglasContacto.ExigirEmailOpcional(emailRecuperacion);

        if (rol == RolUsuarioEnum.Gerente && emailNormalizado is null)
        {
            throw new DomainException(
                "Un Gerente debe tener un email de recuperación: es el único rol que puede recuperar el acceso al sistema.");
        }

        Nombre = nombreNormalizado;
        Apellido = apellidoNormalizado;
        EmailRecuperacion = emailNormalizado;
        IdRol = (int)rol;
    }

    /// <summary>
    /// Otro usuario (Gerente o Administrador) asigna una contraseña temporal: el titular deberá reemplazarla
    /// en su próximo inicio de sesión.
    /// </summary>
    public void BlanquearPassword(string hashTemporal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashTemporal);
        PasswordHash = hashTemporal;
        DebeCambiarPassword = true;
    }

    /// <summary>
    /// El titular de la cuenta elige su propia contraseña, lo que levanta la obligación de cambiarla.
    /// </summary>
    public void CambiarPasswordPropia(string nuevoPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoPasswordHash);
        PasswordHash = nuevoPasswordHash;
        DebeCambiarPassword = false;
    }

    public bool EsGerente() => IdRol == (int)RolUsuarioEnum.Gerente;

    public bool EsAdministrador() => IdRol == (int)RolUsuarioEnum.Administrador;

    /// <summary>
    /// Matriz de autoridad sobre cuentas (RF-03): el Gerente gestiona cualquier rol; el Administrador solo
    /// Cajeros y Encargados, para que no pueda otorgarse a sí mismo ni a otros un rol con más poder que el suyo.
    /// </summary>
    public bool PuedeGestionarCuentasDeRol(RolUsuarioEnum rolObjetivo)
    {
        if (!Enum.IsDefined(rolObjetivo))
        {
            return false;
        }

        if (EsGerente())
        {
            return true;
        }

        return EsAdministrador()
            && rolObjetivo is RolUsuarioEnum.Cajero or RolUsuarioEnum.Encargado;
    }
}
