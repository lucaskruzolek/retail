using Retail.Domain.Enums;

namespace Retail.App.Navegacion;

/// <summary>
/// Decide si un rol puede abrir una página. Es una función pura del rol: se prueba sin interfaz, y el menú lateral, los
/// selectores de sección y los atajos F1 a F11 la consultan por igual, así que no pueden desincronizarse.
/// </summary>
public static class PoliticaAcceso
{
    public static bool Permite(AccesoPagina acceso, RolUsuarioEnum? rol)
    {
        if (rol is null)
        {
            return false;
        }

        return acceso switch
        {
            AccesoPagina.Todos => true,
            AccesoPagina.Operadores => rol != RolUsuarioEnum.Administrador,
            AccesoPagina.Abastecimiento => rol is RolUsuarioEnum.Gerente or RolUsuarioEnum.Encargado,
            AccesoPagina.Gerente => rol == RolUsuarioEnum.Gerente,
            AccesoPagina.GestionUsuarios => rol is RolUsuarioEnum.Gerente or RolUsuarioEnum.Administrador,
            _ => false
        };
    }
}
