using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Common;

namespace Retail.Application.Services.Clasificacion;

/// <summary>
/// Busca marcas del comercio dentro de un texto del proveedor por su nombre o sus alias (Track 6.6, sección 4). Reproduce
/// <c>marcas_en</c> de <c>ml/scripts/comun.py</c>: una forma coincide solo como palabra o frase completa, nunca como
/// parte de otra palabra ("BIC" no aparece en "BICOLOR").
/// </summary>
internal sealed class BuscadorDeMarcas
{
    // Forma de comparación (nombre o alias) → marca. Una forma que comparten dos marcas no sugiere ninguna: una
    // sugerencia equivocada cuesta más que una ausente (M-1). La unicidad entre agregados la cuida la Fase 4b.
    private readonly Dictionary<string, int> _marcaPorForma = new(StringComparer.Ordinal);
    private readonly int _palabrasDeLaFormaMasLarga;

    public BuscadorDeMarcas(IEnumerable<MarcaDiccionarioDto> marcas)
    {
        var ambiguas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marca in marcas)
        {
            var formas = marca.Alias.Prepend(ReglasClasificacion.NormalizarTexto(marca.NombreMarca)).Distinct();
            foreach (var forma in formas)
            {
                if (!_marcaPorForma.TryAdd(forma, marca.IdMarca) && _marcaPorForma[forma] != marca.IdMarca)
                {
                    ambiguas.Add(forma);
                }
            }
        }

        foreach (var forma in ambiguas)
        {
            _marcaPorForma.Remove(forma);
        }

        _palabrasDeLaFormaMasLarga = _marcaPorForma.Keys.Select(f => f.Split(' ').Length).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Marca cuya forma es exactamente el texto (ya normalizado): la columna de marca de la planilla (sección 4.1).
    /// </summary>
    public int? BuscarExacta(string formaNormalizada)
    {
        return _marcaPorForma.TryGetValue(formaNormalizada, out var idMarca) ? idMarca : null;
    }

    /// <summary>
    /// Marcas nombradas en las palabras de una descripción (<c>ReglasClasificacion.PalabrasParaMarca</c>), probando
    /// frases de 1 hasta tantas palabras como tenga la forma más larga del diccionario.
    /// </summary>
    public IReadOnlySet<int> MarcasEn(IReadOnlyList<string> palabras)
    {
        var encontradas = new HashSet<int>();
        for (var largo = 1; largo <= _palabrasDeLaFormaMasLarga; largo++)
        {
            for (var inicio = 0; inicio + largo <= palabras.Count; inicio++)
            {
                var frase = string.Join(' ', palabras.Skip(inicio).Take(largo));
                if (_marcaPorForma.TryGetValue(frase, out var idMarca))
                {
                    encontradas.Add(idMarca);
                }
            }
        }

        return encontradas;
    }
}
