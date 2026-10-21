using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Datos de demostración que carga <c>Retail.Cli datos-demo</c> (RF-23): un proveedor, rubros con sus categorías y marcas.
/// No crea usuarios (el primer Gerente lo crea <c>Retail.Cli inicializar</c>) ni artículos (la demostración los
/// incorpora importando la planilla del proveedor).
/// </summary>
/// <remarks>
/// Rubros, categorías y marcas se cargan acá para tener un catálogo de demostración (la taxonomía completa la
/// carga el comando del Track 6.6). Es idempotente elemento por elemento: se puede ejecutar varias veces y solo
/// agrega lo que falta, sin duplicar ni modificar lo que el usuario ya cargó. Primero crea y guarda los rubros que
/// falten para tener su Id, y recién después cuelga de cada uno las categorías que falten (D-16, D-25).
/// </remarks>
public static class SemilleroDatosDemo
{
    /// <summary>
    /// Cada rubro con las categorías que se le cuelgan (D-25).
    /// </summary>
    public static readonly IReadOnlyList<(string Rubro, IReadOnlyList<string> Categorias)> RubrosConCategorias =
    [
        ("Librería", ["Cuadernos y Blocks", "Escritura", "Oficina"]),
        ("Arte y Manualidades", ["Pinturas", "Pinceles y Herramientas"]),
        ("Marroquinería", ["Mochilas", "Cartucheras"]),
        ("Bazar", ["Viandas y Botellas"]),
        ("Juguetería", ["Juegos de Mesa y Encastre", "Muñecas y Peluches"]),
        ("Cotillón", ["Globos"]),
        ("Electrónica", ["Audio"])
    ];

    /// <summary>
    /// Nombres de todas las categorías de demostración, sin su rubro.
    /// </summary>
    public static IEnumerable<string> Categorias => RubrosConCategorias.SelectMany(r => r.Categorias);

    public static readonly IReadOnlyList<string> Marcas =
    [
        "Rivadavia",
        "Bic",
        "Faber-Castell",
        "Maped",
        "Pelikan",
        "Creaciones Artesanales"
    ];

    // CUIT ficticio con dígito verificador válido: pasa por las mismas reglas que un alta desde la aplicación.
    public const string CuitProveedorDemo = "30709998885";

    /// <summary>
    /// Carga lo que falte y devuelve la cantidad de registros agregados.
    /// </summary>
    public static async Task<int> CargarAsync(RetailDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rubrosExistentes = await context.Rubros
            .Select(r => r.NombreRubro)
            .ToListAsync(cancellationToken);
        var nuevosRubros = RubrosConCategorias
            .Select(r => r.Rubro)
            .Except(rubrosExistentes, StringComparer.OrdinalIgnoreCase)
            .Select(nombre => Rubro.Crear(nombre))
            .ToList();

        // Se guardan primero para que tengan Id: las categorías lo necesitan para referenciar a su rubro.
        await context.Rubros.AddRangeAsync(nuevosRubros, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var idsDeRubros = await context.Rubros
            .ToDictionaryAsync(r => r.NombreRubro, r => r.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var categoriasExistentes = await context.Categorias
            .Select(c => c.NombreCategoria)
            .ToListAsync(cancellationToken);
        var nuevasCategorias = RubrosConCategorias
            .SelectMany(r => r.Categorias.Select(nombre => (Nombre: nombre, IdRubro: idsDeRubros[r.Rubro])))
            .Where(c => !categoriasExistentes.Contains(c.Nombre, StringComparer.OrdinalIgnoreCase))
            .Select(c => Categoria.Crear(c.Nombre, c.IdRubro))
            .ToList();

        var marcasExistentes = await context.Marcas
            .Select(m => m.NombreMarca)
            .ToListAsync(cancellationToken);
        var nuevasMarcas = Marcas
            .Except(marcasExistentes, StringComparer.OrdinalIgnoreCase)
            .Select(nombre => Marca.Crear(nombre))
            .ToList();

        await context.Categorias.AddRangeAsync(nuevasCategorias, cancellationToken);
        await context.Marcas.AddRangeAsync(nuevasMarcas, cancellationToken);

        var agregados = nuevosRubros.Count + nuevasCategorias.Count + nuevasMarcas.Count;

        if (!await context.Proveedores.AnyAsync(p => p.Cuit == CuitProveedorDemo, cancellationToken))
        {
            // Pasa por el agregado: normaliza y valida razón social, CUIT, teléfono y email como cualquier alta.
            var proveedor = Proveedor.Crear(
                "Distribuidora Papelera Demo S.A.",
                "30-70999888-5",
                "(011) 4555-0000",
                "ventas@papelerademo.com.ar");

            await context.Proveedores.AddAsync(proveedor, cancellationToken);
            agregados++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return agregados;
    }
}
