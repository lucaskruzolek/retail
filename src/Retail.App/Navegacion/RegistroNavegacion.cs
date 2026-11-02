using MahApps.Metro.IconPacks;
using Retail.App.Views.Pages;
using Retail.Domain.Enums;

namespace Retail.App.Navegacion;

/// <summary>
/// Mapa de navegación del sistema: las secciones del menú lateral y las páginas de cada una, con su acceso por rol, su
/// atajo y su ícono. Agregar una página es agregar una línea acá: el menú, los selectores y los atajos salen de estos
/// datos. El orden de las secciones es el del menú, y la primera visible para el rol es la página de aterrizaje.
/// </summary>
public static class RegistroNavegacion
{
    public static IReadOnlyList<SeccionNavegacion> Secciones { get; } =
    [
        new("tablero", "Tablero", PackIconBoxIconsKind.RegularBarChart,
        [
            new("tablero", "Tablero", typeof(DashboardView), AccesoPagina.Gerente)
        ]),

        new("pos", "Punto de venta", PackIconBoxIconsKind.RegularCart,
        [
            new("pos", "Punto de venta", typeof(PosView), AccesoPagina.Operadores, "F1")
        ]),

        new("ventas", "Ventas", PackIconBoxIconsKind.RegularReceipt,
        [
            new("clientes", "Clientes", typeof(ClientesView), AccesoPagina.Operadores, "F4"),
            new("caja", "Caja y turnos", typeof(CajaView), AccesoPagina.Operadores, "F5"),
            new("presupuestos", "Presupuestos", typeof(ModuloEnConstruccionView), AccesoPagina.Operadores, "F6", ConfigurarPresupuestos)
        ]),

        new("inventario", "Inventario", PackIconBoxIconsKind.RegularPackage,
        [
            new("articulos", "Artículos", typeof(ArticulosView), AccesoPagina.Operadores, "F2"),
            new("catalogos", "Catálogos mayoristas", typeof(ImportadorCatalogosView), AccesoPagina.Operadores)
        ]),

        new("compras", "Compras", PackIconBoxIconsKind.RegularTruck,
        [
            new("pedidos", "Pedidos", typeof(PedidosView), AccesoPagina.Abastecimiento),
            new("ingresos", "Ingresos", typeof(ComprasView), AccesoPagina.Operadores),
            new("proveedores", "Proveedores", typeof(ProveedoresView), AccesoPagina.Operadores)
        ]),

        new("administracion", "Administración", PackIconBoxIconsKind.RegularShield,
        [
            new("usuarios", "Usuarios", typeof(UsuariosView), AccesoPagina.GestionUsuarios, "F10"),
            new("copias", "Copias", typeof(BackupView), AccesoPagina.GestionUsuarios, "F11", vista => ((BackupView)vista).ViewModel.MostrandoCopias = true),
            new("restauraciones", "Restauraciones", typeof(BackupView), AccesoPagina.GestionUsuarios, null, vista => ((BackupView)vista).ViewModel.MostrandoCopias = false),
            new("fiscal", "Consola fiscal", typeof(ModuloEnConstruccionView), AccesoPagina.Gerente, "F9", ConfigurarConsolaFiscal)
        ])
    ];

    /// <summary>Secciones con al menos una página que el rol puede abrir, en el orden del menú.</summary>
    public static IReadOnlyList<SeccionNavegacion> SeccionesVisibles(RolUsuarioEnum? rol)
    {
        return Secciones.Where(s => PaginasVisibles(s, rol).Count > 0).ToList();
    }

    /// <summary>Páginas de la sección que el rol puede abrir, en orden.</summary>
    public static IReadOnlyList<PaginaNavegacion> PaginasVisibles(SeccionNavegacion seccion, RolUsuarioEnum? rol)
    {
        return seccion.Paginas.Where(p => PoliticaAcceso.Permite(p.Acceso, rol)).ToList();
    }

    /// <summary>
    /// Página de aterrizaje al iniciar sesión: la primera de la primera sección visible. El Gerente entra al tablero,
    /// el Cajero y el Encargado al punto de venta y el Administrador a la gestión de usuarios.
    /// </summary>
    public static PaginaNavegacion? PaginaInicial(RolUsuarioEnum? rol)
    {
        var secciones = SeccionesVisibles(rol);

        return secciones.Count == 0 ? null : PaginasVisibles(secciones[0], rol)[0];
    }

    public static (SeccionNavegacion Seccion, PaginaNavegacion Pagina)? Buscar(string paginaId)
    {
        foreach (var seccion in Secciones)
        {
            var pagina = seccion.Paginas.FirstOrDefault(p => p.Id == paginaId);
            if (pagina is not null)
            {
                return (seccion, pagina);
            }
        }

        return null;
    }

    public static SeccionNavegacion? BuscarSeccion(string seccionId)
    {
        return Secciones.FirstOrDefault(s => s.Id == seccionId);
    }

    /// <summary>Página que abre un atajo ("F4"), o <c>null</c> si ninguna lo usa.</summary>
    public static PaginaNavegacion? BuscarPorAtajo(string atajo)
    {
        return Secciones.SelectMany(s => s.Paginas).FirstOrDefault(p => p.Atajo == atajo);
    }

    private static void ConfigurarPresupuestos(object vista)
    {
        ((ModuloEnConstruccionView)vista).Configurar(
            titulo: "Presupuestador Independiente y Conversión",
            etapa: "Etapa 5: Épica 5 - Módulo 5.1",
            responsable: "Pablo Fernandez",
            requisitos: "RF-11, RF-12",
            descripcion: "Emisión de cotizaciones independientes con congelamiento de precios por 15 días y conversión adaptativa a venta con control de stock.");
    }

    private static void ConfigurarConsolaFiscal(object vista)
    {
        ((ModuloEnConstruccionView)vista).Configurar(
            titulo: "Consola Fiscal ARCA y Contingencia Resiliente",
            etapa: "Etapa 5: Épica 5 - Módulo 5.2",
            responsable: "Lucas Kruzolek",
            requisitos: "RF-16, RF-17, RF-18",
            descripcion: "Panel exclusivo gerencial para monitoreo de comprobantes en contingencia y reintento secuencial en lote ante ARCA.");
    }
}
