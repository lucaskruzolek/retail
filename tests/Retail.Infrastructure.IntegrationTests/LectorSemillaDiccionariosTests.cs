using System.Text;
using FluentAssertions;
using Retail.Infrastructure.Persistence.Initialization;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica la lectura y validación de la semilla de diccionarios (Track 6.6, paso 6.1, S-1) con archivos inventados
/// en una carpeta temporal: nunca con la semilla real ni con la lista del distribuidor.
/// </summary>
public class LectorSemillaDiccionariosTests : IDisposable
{
    private const string Taxonomia = "rubro;categoria\nLibrería;Cuadernos\nArte;Pinturas\n";
    private const string Terminos = "termino;rubro;categoria;ambiguo\ncuaderno;Librería;Cuadernos;\nBASE;;;1\n";
    private const string Marcas = "marca;alias\nÑandú;\nPelikan;PELIKAN ARG\n";
    private const string Prefijos = "prefijo;marca\n0071402;Pelikan\n";
    private const string Pares = "termino;segunda_palabra;categoria\nBASE;ACRILICA;Pinturas\n";

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), $"SemillaTest_{Guid.NewGuid():N}");

    public LectorSemillaDiccionariosTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    public void Dispose()
    {
        Directory.Delete(_carpeta, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Leer_ArchivosValidosConBom_DevuelveCadaFilaEnLaFormaDelDominio()
    {
        EscribirSemilla(conBom: true);

        var semilla = LectorSemillaDiccionarios.Leer(_carpeta);

        semilla.Categorias.Should().Equal(
            new CategoriaSemilla("Librería", "Cuadernos"),
            new CategoriaSemilla("Arte", "Pinturas"));
        semilla.Terminos.Should().Equal(
            new TerminoSemilla("CUADERNO", "Cuadernos"),
            new TerminoSemilla("BASE", null));
        semilla.Marcas.Should().Equal(
            new MarcaSemilla("Ñandú", null),
            new MarcaSemilla("Pelikan", "PELIKAN ARG"));
        semilla.Prefijos.Should().Equal(new PrefijoSemilla("0071402", "Pelikan"));
        semilla.Pares.Should().Equal(new ParSemilla("BASE", "ACRILICA", "Pinturas"));
    }

    [Fact]
    public void Leer_ArchivosSinBomYConFinDeLineaWindows_LosLeeIgual()
    {
        EscribirSemilla(conBom: false);
        Escribir(LectorSemillaDiccionarios.ArchivoTaxonomia, Taxonomia.Replace("\n", "\r\n"), conBom: false);

        var semilla = LectorSemillaDiccionarios.Leer(_carpeta);

        semilla.Categorias.Should().HaveCount(2);
        semilla.Categorias[0].Rubro.Should().Be("Librería");
    }

    [Fact]
    public void Leer_FilaConColumnasDeMas_FallaConArchivoYLinea()
    {
        EscribirSemilla(terminos: Terminos + "LAPIZ;Librería;Cuadernos;;extra\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>()
            .WithMessage("terminos.csv, línea 4: se esperaban 4 columnas y hay 5.");
    }

    [Fact]
    public void Leer_EncabezadoDistinto_Falla()
    {
        EscribirSemilla(marcas: "nombre;alias\nPelikan;\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("marcas.csv, línea 1: *encabezado 'marca;alias'*");
    }

    [Fact]
    public void Leer_CampoEntreComillas_Falla()
    {
        EscribirSemilla(taxonomia: Taxonomia + "Arte;\"Pinceles, Rodillos\"\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("taxonomia.csv, línea 4: *comillas*");
    }

    [Fact]
    public void Leer_TextoQueElDominioRechaza_FallaConArchivoYLineaYElMotivoDelDominio()
    {
        EscribirSemilla(terminos: Terminos + "DOS PALABRAS;Librería;Cuadernos;\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>()
            .WithMessage("terminos.csv, línea 4: El término 'DOS PALABRAS' no es válido*");
    }

    [Fact]
    public void Leer_TerminoConCategoriaFueraDeLaTaxonomia_Falla()
    {
        EscribirSemilla(terminos: Terminos + "MOCHILA;Librería;Mochilas;\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>()
            .WithMessage("terminos.csv, línea 4: la categoría 'Mochilas' *no figura en taxonomia.csv.");
    }

    [Fact]
    public void Leer_TerminoConRubroDistintoAlDeSuCategoria_Falla()
    {
        EscribirSemilla(terminos: Terminos + "OLEO;Librería;Pinturas;\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>()
            .WithMessage("terminos.csv, línea 4: la categoría 'Pinturas' pertenece al rubro 'Arte'*");
    }

    [Fact]
    public void Leer_TerminoAmbiguoConCategoriaDeOrientacion_LoLeeAmbiguoSinCategoria()
    {
        // Formato de consolidar_diccionarios.py: el ambiguo puede conservar una categoría como orientación.
        EscribirSemilla(terminos: Terminos + "GOMA;Librería;Cuadernos;1\n");

        var semilla = LectorSemillaDiccionarios.Leer(_carpeta);

        semilla.Terminos.Should().ContainSingle(t => t.Termino == "GOMA").Which.Categoria.Should().BeNull();
    }

    [Fact]
    public void Leer_TerminoRepetidoConOtraGrafia_Falla()
    {
        EscribirSemilla(terminos: Terminos + "Cuadérno;Librería;Cuadernos;\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("terminos.csv, línea 4: *'CUADERNO' ya figura*");
    }

    [Fact]
    public void Leer_CategoriaRepetidaEnOtroRubro_Falla()
    {
        EscribirSemilla(taxonomia: Taxonomia + "Arte;CUADERNOS\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("taxonomia.csv, línea 4: *ya figura*");
    }

    [Fact]
    public void Leer_MarcasQueSoloDifierenEnTildesYEnie_Falla()
    {
        EscribirSemilla(marcas: Marcas + "Nandu;\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("marcas.csv, línea 4: la marca 'Nandu' ya figura*");
    }

    [Fact]
    public void Leer_AliasQueEsElNombreDeUnaMarcaPosterior_Falla()
    {
        EscribirSemilla(marcas: "marca;alias\nPelikan;MAPED\nMaped;\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>()
            .WithMessage("marcas.csv, línea 2: el alias 'MAPED' de 'Pelikan' es el nombre de una marca.");
    }

    [Fact]
    public void Leer_AliasRepetidoEnDosMarcas_Falla()
    {
        EscribirSemilla(marcas: Marcas + "Maped;PELIKAN ARG\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("marcas.csv, línea 4: *ya figura*");
    }

    [Fact]
    public void Leer_PrefijoDeUnaMarcaQueNoEstaEnLaSemilla_Falla()
    {
        EscribirSemilla(prefijos: Prefijos + "0070330;Bic\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>()
            .WithMessage("prefijos.csv, línea 3: la marca 'Bic' no figura en marcas.csv.");
    }

    [Fact]
    public void Leer_PrefijoSinCerosIniciales_FallaPorElDominio()
    {
        // Es lo que deja Excel al abrir prefijos.csv y guardarlo: "0071402" pasa a ser 71402.
        EscribirSemilla(prefijos: "prefijo;marca\n71402;Pelikan\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("prefijos.csv, línea 2: El prefijo '71402'*");
    }

    [Fact]
    public void Leer_ParDeUnTerminoQueNoEsAmbiguo_Falla()
    {
        EscribirSemilla(pares: Pares + "CUADERNO;ESPIRAL;Cuadernos\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>()
            .WithMessage("pares.csv, línea 3: el término 'CUADERNO' no figura como ambiguo*");
    }

    [Fact]
    public void Leer_ParRepetido_Falla()
    {
        EscribirSemilla(pares: Pares + "base;acrilica;Pinturas\n");

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("pares.csv, línea 3: *ya figura*");
    }

    [Fact]
    public void Leer_ArchivoGuardadoEnAnsi_Falla()
    {
        EscribirSemilla();
        File.WriteAllBytes(
            Path.Combine(_carpeta, LectorSemillaDiccionarios.ArchivoTaxonomia),
            Encoding.Latin1.GetBytes(Taxonomia));

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("taxonomia.csv: el archivo no está en UTF-8.");
    }

    [Fact]
    public void Leer_FaltaUnArchivo_Falla()
    {
        EscribirSemilla();
        File.Delete(Path.Combine(_carpeta, LectorSemillaDiccionarios.ArchivoPares));

        var leer = () => LectorSemillaDiccionarios.Leer(_carpeta);

        leer.Should().Throw<InvalidDataException>().WithMessage("Falta el archivo pares.csv*");
    }

    private void EscribirSemilla(
        string taxonomia = Taxonomia,
        string terminos = Terminos,
        string marcas = Marcas,
        string prefijos = Prefijos,
        string pares = Pares,
        bool conBom = true)
    {
        Escribir(LectorSemillaDiccionarios.ArchivoTaxonomia, taxonomia, conBom);
        Escribir(LectorSemillaDiccionarios.ArchivoTerminos, terminos, conBom);
        Escribir(LectorSemillaDiccionarios.ArchivoMarcas, marcas, conBom);
        Escribir(LectorSemillaDiccionarios.ArchivoPrefijos, prefijos, conBom);
        Escribir(LectorSemillaDiccionarios.ArchivoPares, pares, conBom);
    }

    private void Escribir(string archivo, string contenido, bool conBom)
    {
        File.WriteAllText(Path.Combine(_carpeta, archivo), contenido, new UTF8Encoding(conBom));
    }
}
