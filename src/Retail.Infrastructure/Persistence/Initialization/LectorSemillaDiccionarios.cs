using System.Text;
using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Lee los cinco archivos de la semilla de diccionarios (M-59) y los valida completos antes de que se toque la base.
/// </summary>
/// <remarks>
/// Cualquier defecto del archivo (formato, un texto que el Dominio rechaza, un duplicado o una referencia a una
/// categoría, marca o término que la semilla no define) es un error de la semilla: se lanza
/// <see cref="InvalidDataException"/> con el archivo y la línea, y no se carga nada (S-1). Lo que choca con lo que el
/// comercio ya cargó no se decide acá: lo informa el semillero como conflicto.
/// Los archivos van separados por <c>;</c>, en UTF-8 con o sin BOM. Los nombres se comparan en la forma de comparación
/// del motor (<see cref="ReglasClasificacion.NormalizarTexto"/>), la misma que usan las reglas de unicidad (M-39).
/// </remarks>
public static class LectorSemillaDiccionarios
{
    public const string ArchivoTaxonomia = "taxonomia.csv";
    public const string ArchivoTerminos = "terminos.csv";
    public const string ArchivoMarcas = "marcas.csv";
    public const string ArchivoPrefijos = "prefijos.csv";
    public const string ArchivoPares = "pares.csv";

    private const string MarcaDeAmbiguo = "1";

    // Rechaza los bytes que no son UTF-8 válido (por ejemplo, una planilla guardada en ANSI desde Excel) en lugar de
    // reemplazarlos en silencio por '�'. El BOM, si está, lo descarta el lector de texto.
    private static readonly Encoding Utf8Estricto = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static SemillaDiccionarios Leer(string carpeta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);

        var categorias = LeerTaxonomia(carpeta);
        var rubroPorCategoria = categorias.ToDictionary(
            c => ReglasClasificacion.NormalizarTexto(c.Categoria),
            c => c.Rubro);
        var categoriaPorForma = categorias.ToDictionary(
            c => ReglasClasificacion.NormalizarTexto(c.Categoria),
            c => c.Categoria);

        var terminos = LeerTerminos(carpeta, rubroPorCategoria, categoriaPorForma);
        var marcas = LeerMarcas(carpeta);
        var prefijos = LeerPrefijos(carpeta, marcas);
        var pares = LeerPares(carpeta, terminos, categoriaPorForma);

        return new SemillaDiccionarios(categorias, terminos, marcas, prefijos, pares);
    }

    private static List<CategoriaSemilla> LeerTaxonomia(string carpeta)
    {
        var categorias = new List<CategoriaSemilla>();
        var vistas = new HashSet<string>();

        foreach (var (linea, campos) in LeerFilas(carpeta, ArchivoTaxonomia, "rubro;categoria"))
        {
            var rubro = Exigir(ArchivoTaxonomia, linea, () => ReglasTexto.ExigirNombreClasificacion(campos[0], "rubro"));
            var categoria = Exigir(
                ArchivoTaxonomia,
                linea,
                () => ReglasTexto.ExigirNombreClasificacion(campos[1], "nombre de la categoría"));

            // La categoría es única en todo el catálogo (D-8): repetida, aunque sea en otro rubro, es un error.
            if (!vistas.Add(ReglasClasificacion.NormalizarTexto(categoria)))
            {
                throw Error(ArchivoTaxonomia, linea, $"la categoría '{categoria}' ya figura en una línea anterior.");
            }

            categorias.Add(new CategoriaSemilla(rubro, categoria));
        }

        return categorias;
    }

    private static List<TerminoSemilla> LeerTerminos(
        string carpeta,
        Dictionary<string, string> rubroPorCategoria,
        Dictionary<string, string> categoriaPorForma)
    {
        var terminos = new List<TerminoSemilla>();
        var vistos = new HashSet<string>();

        foreach (var (linea, campos) in LeerFilas(carpeta, ArchivoTerminos, "termino;rubro;categoria;ambiguo"))
        {
            var termino = Exigir(ArchivoTerminos, linea, () => ReglasClasificacion.ExigirTermino(campos[0]));
            if (!vistos.Add(termino))
            {
                throw Error(ArchivoTerminos, linea, $"el término '{termino}' ya figura en una línea anterior.");
            }

            var rubro = campos[1].Trim();
            var categoria = campos[2].Trim();
            var ambiguo = campos[3].Trim();

            if (ambiguo == MarcaDeAmbiguo)
            {
                // consolidar_diccionarios.py conserva la categoría de algunos ambiguos como orientación para quien
                // edita la planilla. En el Dominio un término ambiguo no tiene categoría (M-27): se descarta.
                terminos.Add(new TerminoSemilla(termino, null));
                continue;
            }

            if (ambiguo.Length > 0)
            {
                throw Error(
                    ArchivoTerminos,
                    linea,
                    $"la columna 'ambiguo' solo admite '{MarcaDeAmbiguo}' o vacío, no '{ambiguo}'.");
            }

            var forma = ReglasClasificacion.NormalizarTexto(categoria);
            if (!rubroPorCategoria.TryGetValue(forma, out var rubroDeLaCategoria))
            {
                throw Error(
                    ArchivoTerminos,
                    linea,
                    $"la categoría '{categoria}' del término '{termino}' no figura en {ArchivoTaxonomia}.");
            }

            if (ReglasClasificacion.NormalizarTexto(rubro) != ReglasClasificacion.NormalizarTexto(rubroDeLaCategoria))
            {
                throw Error(
                    ArchivoTerminos,
                    linea,
                    $"la categoría '{categoria}' pertenece al rubro '{rubroDeLaCategoria}', no a '{rubro}'.");
            }

            terminos.Add(new TerminoSemilla(termino, categoriaPorForma[forma]));
        }

        return terminos;
    }

    private static List<MarcaSemilla> LeerMarcas(string carpeta)
    {
        var filas = new List<(int Linea, string Marca, string? Alias)>();
        var nombres = new HashSet<string>();

        foreach (var (linea, campos) in LeerFilas(carpeta, ArchivoMarcas, "marca;alias"))
        {
            var marca = Exigir(
                ArchivoMarcas,
                linea,
                () => ReglasTexto.ExigirNombreClasificacion(campos[0], "nombre de la marca"));

            // "Nandu" y "Ñandú" son la misma marca para el motor (M-39).
            if (!nombres.Add(ReglasClasificacion.NormalizarTexto(marca)))
            {
                throw Error(ArchivoMarcas, linea, $"la marca '{marca}' ya figura en una línea anterior.");
            }

            var alias = string.IsNullOrWhiteSpace(campos[1])
                ? null
                : Exigir(ArchivoMarcas, linea, () => ReglasClasificacion.ExigirAlias(campos[1]));
            filas.Add((linea, marca, alias));
        }

        // Los alias se revisan cuando ya se conocen todos los nombres: un alias no puede ser el nombre de ninguna
        // marca, ni siquiera de una que aparece más abajo (M-38).
        var aliasVistos = new HashSet<string>();
        foreach (var (linea, marca, alias) in filas.Where(f => f.Alias is not null))
        {
            if (nombres.Contains(alias!))
            {
                throw Error(ArchivoMarcas, linea, $"el alias '{alias}' de '{marca}' es el nombre de una marca.");
            }

            if (!aliasVistos.Add(alias!))
            {
                throw Error(ArchivoMarcas, linea, $"el alias '{alias}' ya figura en una línea anterior.");
            }
        }

        return filas.Select(f => new MarcaSemilla(f.Marca, f.Alias)).ToList();
    }

    private static List<PrefijoSemilla> LeerPrefijos(string carpeta, List<MarcaSemilla> marcas)
    {
        var marcaPorForma = marcas.ToDictionary(m => ReglasClasificacion.NormalizarTexto(m.Marca), m => m.Marca);
        var prefijos = new List<PrefijoSemilla>();
        var vistos = new HashSet<string>();

        foreach (var (linea, campos) in LeerFilas(carpeta, ArchivoPrefijos, "prefijo;marca"))
        {
            var prefijo = Exigir(ArchivoPrefijos, linea, () => ReglasCodigoBarras.ExigirPrefijoTitular(campos[0]));
            if (!vistos.Add(prefijo))
            {
                throw Error(ArchivoPrefijos, linea, $"el prefijo '{prefijo}' ya figura en una línea anterior.");
            }

            if (!marcaPorForma.TryGetValue(ReglasClasificacion.NormalizarTexto(campos[1]), out var marca))
            {
                throw Error(ArchivoPrefijos, linea, $"la marca '{campos[1].Trim()}' no figura en {ArchivoMarcas}.");
            }

            prefijos.Add(new PrefijoSemilla(prefijo, marca));
        }

        return prefijos;
    }

    private static List<ParSemilla> LeerPares(
        string carpeta,
        List<TerminoSemilla> terminos,
        Dictionary<string, string> categoriaPorForma)
    {
        var ambiguos = terminos.Where(t => t.Categoria is null).Select(t => t.Termino).ToHashSet();
        var pares = new List<ParSemilla>();
        var vistos = new HashSet<(string, string)>();

        foreach (var (linea, campos) in LeerFilas(carpeta, ArchivoPares, "termino;segunda_palabra;categoria"))
        {
            var termino = Exigir(ArchivoPares, linea, () => ReglasClasificacion.ExigirTermino(campos[0]));
            var segundaPalabra = Exigir(
                ArchivoPares,
                linea,
                () => ReglasClasificacion.ExigirSegundaPalabra(campos[1]));

            // Un par solo existe mientras su término es ambiguo (M-52).
            if (!ambiguos.Contains(termino))
            {
                throw Error(
                    ArchivoPares,
                    linea,
                    $"el término '{termino}' no figura como ambiguo en {ArchivoTerminos}: no puede tener pares.");
            }

            if (!vistos.Add((termino, segundaPalabra)))
            {
                throw Error(
                    ArchivoPares,
                    linea,
                    $"el par '{termino} {segundaPalabra}' ya figura en una línea anterior.");
            }

            if (!categoriaPorForma.TryGetValue(ReglasClasificacion.NormalizarTexto(campos[2]), out var categoria))
            {
                throw Error(ArchivoPares, linea, $"la categoría '{campos[2].Trim()}' no figura en {ArchivoTaxonomia}.");
            }

            pares.Add(new ParSemilla(termino, segundaPalabra, categoria));
        }

        return pares;
    }

    /// <summary>
    /// Devuelve las filas de datos con su número de línea (el encabezado es la línea 1). Exige el encabezado exacto,
    /// la cantidad de columnas de cada fila y que ningún campo traiga comillas; saltea las líneas vacías.
    /// </summary>
    private static List<(int Linea, string[] Campos)> LeerFilas(
        string carpeta,
        string archivo,
        string encabezado)
    {
        var ruta = Path.Combine(carpeta, archivo);
        if (!File.Exists(ruta))
        {
            throw new InvalidDataException($"Falta el archivo {archivo} en la carpeta '{carpeta}'.");
        }

        string[] lineas;
        try
        {
            lineas = File.ReadAllLines(ruta, Utf8Estricto);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException($"{archivo}: el archivo no está en UTF-8.", ex);
        }

        if (lineas.Length == 0 || lineas[0].Trim() != encabezado)
        {
            throw Error(archivo, 1, $"se esperaba el encabezado '{encabezado}'.");
        }

        var columnas = encabezado.Split(';').Length;
        var filas = new List<(int Linea, string[] Campos)>();
        for (var indice = 1; indice < lineas.Length; indice++)
        {
            var linea = indice + 1;
            if (string.IsNullOrWhiteSpace(lineas[indice]))
            {
                continue;
            }

            // Un campo entre comillas es señal de una planilla guardada desde Excel con otro formato.
            if (lineas[indice].Contains('"'))
            {
                throw Error(archivo, linea, "los campos no pueden llevar comillas.");
            }

            var campos = lineas[indice].Split(';');
            if (campos.Length != columnas)
            {
                throw Error(archivo, linea, $"se esperaban {columnas} columnas y hay {campos.Length}.");
            }

            filas.Add((linea, campos));
        }

        return filas;
    }

    private static string Exigir(string archivo, int linea, Func<string> regla)
    {
        try
        {
            return regla();
        }
        catch (DomainException ex)
        {
            throw Error(archivo, linea, ex.Message);
        }
    }

    private static InvalidDataException Error(string archivo, int linea, string mensaje)
    {
        return new InvalidDataException($"{archivo}, línea {linea}: {mensaje}");
    }
}
