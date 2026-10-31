using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Reglas del alta de una categoría que comparten <see cref="CategoriaService"/> y
/// <see cref="TerminoClasificacionService"/> (Track 6.6, paso 6.2, T-2): el rubro tiene que existir y estar activo, y
/// el nombre no puede repetirse en todo el catálogo (D-8). No guarda: cada llamador decide cuándo.
/// </summary>
internal static class AltaCategoria
{
    /// <summary>
    /// Devuelve la categoría creada en memoria, ya agregada al repositorio, y su rubro.
    /// </summary>
    public static async Task<(Categoria Categoria, Rubro Rubro)> PrepararAsync(
        IRepository<Categoria> categorias,
        IRepository<Rubro> rubros,
        string nombre,
        int idRubro,
        CancellationToken cancellationToken)
    {
        var rubro = await ObtenerRubroActivoAsync(rubros, idRubro, cancellationToken);

        // La comparación sin distinguir mayúsculas ni acentos la resuelve la colación de SQL Server (R-3) y el
        // índice único filtrado es la garantía final ante dos altas simultáneas (D-4).
        string normalizado = ReglasTexto.NormalizarNombreClasificacion(nombre);
        var existentes = await categorias.FindAsync(
            x => x.NombreCategoria == normalizado,
            includeDeleted: false,
            cancellationToken);

        if (existentes.Count > 0)
        {
            throw new DomainException($"Ya existe una categoría activa con el nombre '{normalizado}'.");
        }

        var categoria = Categoria.Crear(normalizado, rubro.Id);
        await categorias.AddAsync(categoria, cancellationToken);
        return (categoria, rubro);
    }

    /// <summary>
    /// D-23: el rubro de destino debe existir y estar activo; la FK de la base es la garantía final, pero este chequeo
    /// da un mensaje claro en lugar de un error de integridad referencial.
    /// </summary>
    public static async Task<Rubro> ObtenerRubroActivoAsync(
        IRepository<Rubro> rubros,
        int idRubro,
        CancellationToken cancellationToken)
    {
        var rubro = await rubros.GetByIdAsync(idRubro, includeDeleted: false, cancellationToken);
        if (rubro == null)
        {
            throw new DomainException($"No se encontró el rubro con ID {idRubro}.");
        }

        return rubro;
    }
}
