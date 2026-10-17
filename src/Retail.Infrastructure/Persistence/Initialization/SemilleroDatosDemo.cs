using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Datos de demostración que carga <c>Retail.Cli datos-demo</c> (RF-23): un proveedor, categorías y marcas.
/// No crea usuarios (el primer Gerente lo crea <c>Retail.Cli inicializar</c>) ni artículos (la demostración los
/// incorpora importando la planilla del proveedor).
/// </summary>
/// <remarks>
/// Categorías y marcas se cargan acá porque todavía no tienen un ABM propio; cuando exista, este semillero deja de
/// ser necesario para ellas. Es idempotente elemento por elemento: se puede ejecutar varias veces y solo agrega lo
/// que falta, sin duplicar ni modificar lo que el usuario ya cargó.
/// </remarks>
public static class SemilleroDatosDemo
{
    public static readonly IReadOnlyList<string> Categorias =
    [
        "Escolar",
        "Oficina y Comercial",
        "Artística y Dibujo",
        "Textos y Literatura",
        "Regalería y Artesanías"
    ];

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

        var categoriasExistentes = await context.Categorias
            .Select(c => c.NombreCategoria)
            .ToListAsync(cancellationToken);
        var nuevasCategorias = Categorias
            .Except(categoriasExistentes, StringComparer.OrdinalIgnoreCase)
            .Select(nombre => Categoria.Crear(nombre))
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

        var agregados = nuevasCategorias.Count + nuevasMarcas.Count;

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
