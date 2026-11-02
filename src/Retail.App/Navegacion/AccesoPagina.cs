namespace Retail.App.Navegacion;

/// <summary>
/// Quién puede abrir una página. Es la única fuente de las reglas de acceso del menú, de los selectores de sección y de
/// los atajos de teclado: <see cref="PoliticaAcceso"/> las evalúa contra el rol del operador.
/// </summary>
public enum AccesoPagina
{
    /// <summary>Quien opera el negocio: todos los roles menos el Administrador, que es soporte técnico (RF-03, D-3).</summary>
    Operadores,

    /// <summary>Pedidos a proveedor: solo Encargado y Gerente; el Cajero no tiene acceso (A-14 del Track 4.3, G1).</summary>
    Abastecimiento,

    /// <summary>Solo el Gerente: tablero gerencial y consola fiscal.</summary>
    Gerente,

    /// <summary>Gestión de cuentas y respaldos: Gerente y Administrador (RF-03). Qué cuentas toca cada uno lo decide el servicio.</summary>
    GestionUsuarios
}
