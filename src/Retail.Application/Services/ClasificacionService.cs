using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services.Clasificacion;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Sugerencia de categoría y marca por niveles (Track 6.6, sección 4; RF-27). Cada nivel resuelve lo que puede y deja
/// pasar el resto, de lo más confiable a lo menos (M-14). Todos son reglas aprobadas por una persona (M-45, M-49):
/// <list type="number">
/// <item>Lo que informó el proveedor: su marca coincide con una del comercio, o una equivalencia traduce su valor.</item>
/// <item>Los diccionarios: término no ambiguo, o par "término ambiguo + segunda palabra" (Fase 5, M-48); marca nombrada
/// en la descripción, con el prefijo EAN solo para desempatar (M-25, M-69).</item>
/// <item>Términos nuevos frecuentes: se agrupan para decidirlos en bloque (M-26).</item>
/// </list>
/// Lo que ninguna regla resuelve queda sin sugerencia.
/// Todo ocurre en memoria después de dos lecturas (el lote y sus diccionarios); no modifica nada (M-4). Las reglas
/// viven en <see cref="MotorDeClasificacion"/>, que también usa la clasificación del catálogo (M-77); este servicio
/// arma los bloques de términos nuevos (M-26).
/// </summary>
public class ClasificacionService : IClasificacionService
{
    /// <summary>
    /// Cantidad mínima de ítems con el mismo término nuevo para decidirlos en bloque (M-26).
    /// </summary>
    public const int TamanoMinimoDeBloque = 3;

    private const int EjemplosPorBloque = 3;

    private readonly ICatalogoProveedorQueryService _catalogo;
    private readonly IDiccionariosClasificacionQueryService _diccionarios;
    private readonly IValidator<SugerirClasificacionDto> _validator;

    public ClasificacionService(
        ICatalogoProveedorQueryService catalogo,
        IDiccionariosClasificacionQueryService diccionarios,
        IValidator<SugerirClasificacionDto> validator)
    {
        _catalogo = catalogo;
        _diccionarios = diccionarios;
        _validator = validator;
    }

    public async Task<SugerenciasClasificacionDto> SugerirAsync(SugerirClasificacionDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _validator.ValidateAndThrowAsync(dto, cancellationToken);

        var items = await CargarItemsAsync(dto, cancellationToken);
        var diccionarios = new DiccionariosDelLote(await _diccionarios.ObtenerParaLoteAsync(
            dto.IdProveedor,
            items.Select(i => i.Termino).Where(t => t.Length > 0).Distinct().ToList(),
            items.Select(i => i.PrefijoTitular).OfType<string>().Distinct().ToList(),
            cancellationToken));

        var sinTraducir = new Dictionary<(TipoEquivalenciaEnum Tipo, string? Rubro, string Texto), int>();
        var marcas = items.ToDictionary(i => i.IdCatalogo, i => MotorDeClasificacion.SugerirMarca(i, diccionarios, sinTraducir));
        var categorias = items.ToDictionary(i => i.IdCatalogo, i => MotorDeClasificacion.ClasificarCategoria(i, diccionarios, sinTraducir));

        // Términos nuevos frecuentes: se deciden en bloque (M-26). Un ítem sin descripción no tiene término que
        // registrar.
        var terminosEnBloque = items
            .Where(i => categorias[i.IdCatalogo].Estado == MotorDeClasificacion.EstadoCategoria.TerminoDesconocido && i.Termino.Length > 0)
            .GroupBy(i => i.Termino)
            .Where(g => g.Count() >= TamanoMinimoDeBloque)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new SugerenciasClasificacionDto
        {
            Items = items.Select(i => new SugerenciaItemDto
            {
                IdCatalogo = i.IdCatalogo,
                Descripcion = i.Descripcion,
                Termino = i.Termino,
                Categoria = terminosEnBloque.ContainsKey(i.Termino) ? null : categorias[i.IdCatalogo].Sugerencia,
                Marca = marcas[i.IdCatalogo],
                PerteneceABloque = terminosEnBloque.ContainsKey(i.Termino)
            }).ToList(),
            Bloques = terminosEnBloque
                .Select(g => ArmarBloque(g.Key, g.Value))
                .OrderByDescending(b => b.IdsCatalogo.Count)
                .ThenBy(b => b.Termino, StringComparer.Ordinal)
                .ToList(),
            ValoresSinTraducir = sinTraducir
                .Select(v => new ValorSinTraducirDto { Tipo = v.Key.Tipo, TextoRubro = v.Key.Rubro, Texto = v.Key.Texto, CantidadDeItems = v.Value })
                .OrderByDescending(v => v.CantidadDeItems)
                .ThenBy(v => v.Texto, StringComparer.Ordinal)
                .ToList()
        };
    }

    private async Task<List<ItemAClasificar>> CargarItemsAsync(SugerirClasificacionDto dto, CancellationToken cancellationToken)
    {
        var ids = dto.IdsCatalogo.Distinct().ToList();
        var catalogos = (await _catalogo.ObtenerPorIdsAsync(ids, cancellationToken)).ToDictionary(c => c.Id);

        var inexistentes = ids.Where(id => !catalogos.ContainsKey(id)).ToList();
        if (inexistentes.Count > 0)
        {
            throw new DomainException($"No se encontraron los ítems de catálogo {string.Join(", ", inexistentes)}.");
        }

        if (catalogos.Values.Any(c => c.IdProveedor != dto.IdProveedor))
        {
            throw new DomainException("Todos los ítems a clasificar deben pertenecer al mismo proveedor.");
        }

        // Se respeta el orden pedido: es el de la grilla de la pantalla de incorporación.
        return ids.Select(id => ItemAClasificar.Desde(catalogos[id])).ToList();
    }

    // ---------- Bloques ----------

    /// <summary>
    /// Un bloque llega sin categoría propuesta: la decide el usuario una sola vez para todo el grupo y el término queda
    /// registrado (M-16, M-26). Los ejemplos hacen informada la decisión.
    /// </summary>
    private static BloqueTerminoNuevoDto ArmarBloque(string termino, IReadOnlyList<ItemAClasificar> items)
    {
        return new BloqueTerminoNuevoDto
        {
            Termino = termino,
            IdsCatalogo = items.Select(i => i.IdCatalogo).ToList(),
            Ejemplos = items.Take(EjemplosPorBloque).Select(i => i.Descripcion).ToList()
        };
    }
}
