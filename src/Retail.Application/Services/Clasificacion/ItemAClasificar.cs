using Retail.Domain.Common;
using Retail.Domain.Entities;

namespace Retail.Application.Services.Clasificacion;

/// <summary>
/// Un ítem del catálogo del proveedor después del nivel 0 de la clasificación (Track 6.6, sección 4): descripción,
/// término, segunda palabra (Fase 5), palabras de marca y prefijo del titular ya normalizados, una sola vez por ítem.
/// </summary>
internal sealed record ItemAClasificar(
    int IdCatalogo,
    string Descripcion,
    string Termino,
    string SegundaPalabra,
    IReadOnlyList<string> PalabrasParaMarca,
    string? PrefijoTitular,
    string? RubroProveedor,
    string? CategoriaProveedor,
    string? MarcaProveedor)
{
    public static ItemAClasificar Desde(CatalogoProveedor catalogo)
    {
        return new ItemAClasificar(
            catalogo.Id,
            catalogo.DescripcionProveedor,
            ReglasClasificacion.TerminoDeDescripcion(catalogo.DescripcionProveedor),
            ReglasClasificacion.SegundaPalabra(catalogo.DescripcionProveedor),
            ReglasClasificacion.PalabrasParaMarca(catalogo.DescripcionProveedor),
            ReglasCodigoBarras.ObtenerPrefijoTitular(GtinUtilizable(catalogo)),
            catalogo.RubroProveedor,
            catalogo.CategoriaProveedor,
            catalogo.MarcaProveedor);
    }

    // Un código de barras igual al código interno del proveedor no identifica al producto aunque tenga forma de EAN
    // (comun.normalizar_ean): depende de la planilla, por eso la regla vive acá y no en ReglasCodigoBarras.
    private static string? GtinUtilizable(CatalogoProveedor catalogo)
    {
        var codigo = catalogo.CodigoBarras?.Trim();
        if (string.IsNullOrEmpty(codigo) || codigo == catalogo.CodigoProveedor.Trim())
        {
            return null;
        }

        return ReglasCodigoBarras.NormalizarGtin(codigo);
    }
}
