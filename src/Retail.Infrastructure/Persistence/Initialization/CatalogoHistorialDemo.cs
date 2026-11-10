using Retail.Domain.Enums;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Datos fijos del historial de demostración: artículos, presentaciones, clientes y cajeros, todos ficticios. Usa solo
/// categorías y marcas de <see cref="SemilleroDatosDemo"/>, que también están en la semilla de diccionarios.
/// </summary>
internal static class CatalogoHistorialDemo
{
    // CUIT ficticios con dígito verificador válido: pasan por las mismas reglas que un alta desde la aplicación.
    public const string CuitProveedorEscolar = "30-70999777-3";
    public const string CuitProveedorBazar = "30-70999666-1";

    // Librería y arte se compran a un mayorista; el resto, al otro.
    private static readonly HashSet<string> CategoriasDelMayoristaEscolar = new(StringComparer.Ordinal)
    {
        "Cuadernos y Blocks", "Escritura", "Oficina", "Pinturas", "Pinceles y Herramientas"
    };

    public static readonly IReadOnlyList<(string Usuario, string Nombre, string Apellido)> Cajeros =
    [
        ("demo.mgomez", "Micaela", "Gómez"),
        ("demo.jperalta", "Julián", "Peralta"),
        ("demo.lsosa", "Lucía", "Sosa")
    ];

    public static readonly IReadOnlyList<ProductoDemo> Productos =
    [
        .. Familia("Cuadernos y Blocks", "Rivadavia", "Cuaderno ABC tapa dura 48 hojas", 3200m, 65m, "rayado", "cuadriculado"),
        .. Familia("Cuadernos y Blocks", "Rivadavia", "Cuaderno ABC tapa dura 100 hojas", 5400m, 65m, "rayado", "cuadriculado"),
        .. Familia("Cuadernos y Blocks", "Rivadavia", "Cuaderno universitario 84 hojas", 4100m, 60m, "rayado", "cuadriculado"),
        .. Familia("Cuadernos y Blocks", "Rivadavia", "Repuesto de hojas A4 x96", 3000m, 55m, "rayado", "cuadriculado"),
        .. Familia("Cuadernos y Blocks", null, "Block de notas A5", 1500m, 70m, "rayado", "liso"),
        .. Familia("Escritura", "Bic", "Bolígrafo Bic Cristal", 420m, 90m, "azul", "negro", "rojo", "verde"),
        .. Familia("Escritura", "Bic", "Bolígrafo Bic Opaco", 380m, 90m, "azul", "negro"),
        .. Familia("Escritura", "Bic", "Corrector en cinta Bic", 1900m, 75m),
        .. Familia("Escritura", "Faber", "Microfibra Faber 0.4", 900m, 85m, "negro", "azul", "rojo", "verde"),
        .. Familia("Escritura", "Faber", "Resaltador Faber Textliner", 1100m, 80m, "amarillo", "verde", "rosa", "naranja"),
        .. Familia("Escritura", "Faber", "Lápiz negro Faber HB caja x12", 3300m, 60m),
        .. Familia("Escritura", "Pelikan", "Portaminas Pelikan 0.5", 2300m, 75m),
        .. Familia("Oficina", null, "Resma A4 75 g x500", 7800m, 35m),
        .. Familia("Oficina", null, "Folios A4 x100", 3500m, 50m),
        .. Familia("Oficina", "Maped", "Abrochadora Maped", 6200m, 55m),
        .. Familia("Oficina", "Maped", "Broches 24/6 x1000 Maped", 1100m, 65m),
        .. Familia("Oficina", "Maped", "Tijera de oficina Maped", 2600m, 60m),
        .. Familia("Oficina", "Maped", "Cinta adhesiva Maped", 900m, 70m, "18 mm", "48 mm"),
        .. Familia("Oficina", null, "Clips metálicos x100", 900m, 70m),
        .. Familia("Pinturas", "Pelikan", "Témpera Pelikan 250 ml", 1900m, 70m, "rojo", "azul", "amarillo", "verde", "blanco", "negro"),
        .. Familia("Pinturas", "Pelikan", "Acrílico Pelikan 200 ml", 3100m, 70m, "rojo", "azul", "blanco"),
        .. Familia("Pinturas", "Pelikan", "Acuarelas Pelikan 12 colores", 4500m, 65m),
        .. Familia("Pinceles y Herramientas", "Pelikan", "Pincel Pelikan", 800m, 85m, "N° 2", "N° 6", "N° 10"),
        .. Familia("Pinceles y Herramientas", null, "Espátula plástica x5", 1300m, 80m),
        .. Familia("Pinceles y Herramientas", null, "Set de pinceles x12", 5200m, 70m),
        .. Familia("Mochilas", null, "Mochila escolar de espalda", 28000m, 55m, "negra", "azul", "rosa"),
        .. Familia("Mochilas", null, "Mochila con carro", 52000m, 50m, "negra", "azul"),
        .. Familia("Mochilas", "Maped", "Mochila urbana Maped", 39000m, 55m),
        .. Familia("Cartucheras", "Maped", "Cartuchera canopla dos pisos Maped", 11500m, 60m, "azul", "violeta"),
        .. Familia("Cartucheras", null, "Cartuchera tubo", 4200m, 65m, "negra", "roja", "gris"),
        .. Familia("Viandas y Botellas", null, "Botella térmica 500 ml", 12500m, 60m, "negra", "blanca"),
        .. Familia("Viandas y Botellas", null, "Vianda hermética", 5800m, 65m, "chica", "grande"),
        .. Familia("Viandas y Botellas", null, "Botella plástica infantil", 3900m, 70m),
        .. Familia("Juegos de Mesa y Encastre", "Creaciones Artesanales", "Juego de memoria de madera", 7400m, 60m),
        .. Familia("Juegos de Mesa y Encastre", "Creaciones Artesanales", "Ajedrez de madera", 15800m, 55m),
        .. Familia("Juegos de Mesa y Encastre", null, "Rompecabezas 500 piezas", 9800m, 50m),
        .. Familia("Juegos de Mesa y Encastre", null, "Ludo y damas", 5100m, 55m),
        .. Familia("Juegos de Mesa y Encastre", null, "Dominó clásico", 4300m, 55m),
        .. Familia("Muñecas y Peluches", "Creaciones Artesanales", "Peluche oso", 8200m, 65m, "chico", "mediano", "grande"),
        .. Familia("Muñecas y Peluches", "Creaciones Artesanales", "Muñeca de trapo", 9500m, 70m),
        .. Familia("Globos", null, "Globos x25", 1700m, 110m, "surtidos", "blancos", "negros", "dorados"),
        .. Familia("Globos", null, "Inflador manual de globos", 2800m, 90m),
        .. Familia("Audio", null, "Auricular con cable", 6700m, 45m),
        .. Familia("Audio", null, "Auriculares inalámbricos", 21000m, 40m),
        .. Familia("Audio", null, "Parlante portátil", 32000m, 40m),
        new ProductoDemo("Fotocopia simple", "Oficina", null, 40m, 150m, EsServicio: true),
        new ProductoDemo("Anillado A4", "Oficina", null, 900m, 120m, EsServicio: true)
    ];

    public static readonly IReadOnlyList<PresentacionDemo> Presentaciones =
    [
        new("Lápiz negro Faber HB caja x12", "Lápiz negro Faber HB unidad", 12, 95m),
        new("Folios A4 x100", "Folio A4 unidad", 100, 110m),
        new("Globos x25 surtidos", "Globo surtido unidad", 25, 130m)
    ];

    public static readonly IReadOnlyList<ClienteDemo> Clientes =
    [
        new("Gómez María Laura", TipoDocumentoEnum.Dni, "25111222", CondicionIvaEnum.ConsumidorFinal, "(011) 4555-0101", "mlgomez@example.com", 250000m),
        new("Fernández Carlos Alberto", TipoDocumentoEnum.Dni, "27333444", CondicionIvaEnum.ConsumidorFinal, "(011) 4555-0102"),
        new("Rodríguez Ana Paula", TipoDocumentoEnum.Dni, "30222333", CondicionIvaEnum.ConsumidorFinal, null, "anapaula@example.com"),
        new("López Jorge Daniel", TipoDocumentoEnum.Dni, "22444555", CondicionIvaEnum.Monotributo, "(011) 4555-0104", null, 180000m),
        new("Martínez Sofía", TipoDocumentoEnum.Dni, "38111999", CondicionIvaEnum.ConsumidorFinal),
        new("García Lucas Emanuel", TipoDocumentoEnum.Dni, "35666777", CondicionIvaEnum.ConsumidorFinal, "(011) 4555-0106"),
        new("Pérez Valentina", TipoDocumentoEnum.Dni, "40123456", CondicionIvaEnum.ConsumidorFinal),
        new("Sánchez Roberto", TipoDocumentoEnum.Dni, "18222111", CondicionIvaEnum.Monotributo, "(011) 4555-0108", "rsanchez@example.com", 90000m, EsMoroso: true),
        new("Romero Florencia", TipoDocumentoEnum.Dni, "33999888", CondicionIvaEnum.ConsumidorFinal, null, "florromero@example.com"),
        new("Díaz Martín", TipoDocumentoEnum.Dni, "29555666", CondicionIvaEnum.ConsumidorFinal),
        new("Torres Camila", TipoDocumentoEnum.Dni, "41222333", CondicionIvaEnum.ConsumidorFinal, "(011) 4555-0111"),
        new("Ruiz Hernán", TipoDocumentoEnum.Dni, "26888777", CondicionIvaEnum.ConsumidorFinal, "(011) 4555-0112", null, 120000m),
        new("Álvarez Natalia", TipoDocumentoEnum.Dni, "31444555", CondicionIvaEnum.ConsumidorFinal),
        new("Benítez Gabriel", TipoDocumentoEnum.Dni, "24777888", CondicionIvaEnum.Monotributo, null, "gbenitez@example.com"),
        new("Medina Julieta", TipoDocumentoEnum.Dni, "37666555", CondicionIvaEnum.ConsumidorFinal),
        new("Herrera Pablo Andrés", TipoDocumentoEnum.Dni, "23111777", CondicionIvaEnum.ConsumidorFinal, "(011) 4555-0116"),
        new("Castro Milagros", TipoDocumentoEnum.Dni, "42555111", CondicionIvaEnum.ConsumidorFinal),
        new("Vega Tomás", TipoDocumentoEnum.Dni, "36222888", CondicionIvaEnum.ConsumidorFinal, "(011) 4555-0118"),
        new("Molina Agustina", TipoDocumentoEnum.Dni, "39444222", CondicionIvaEnum.ConsumidorFinal, null, "amolina@example.com"),
        new("Ortiz Diego", TipoDocumentoEnum.Dni, "28111666", CondicionIvaEnum.ConsumidorFinal, "(011) 4555-0120", null, 75000m, EsMoroso: true),
        new("Colegio Modelo del Sur S.R.L.", TipoDocumentoEnum.Cuit, "30-70888111-9", CondicionIvaEnum.Exento, "(011) 4555-0201", "compras@colegiomodelo.example.com", 600000m),
        new("Estudio Contable Arenas y Asociados", TipoDocumentoEnum.Cuit, "30-70888222-0", CondicionIvaEnum.ResponsableInscripto, "(011) 4555-0202", "administracion@estudioarenas.example.com", 350000m),
        new("Jardín de Infantes Los Girasoles S.A.", TipoDocumentoEnum.Cuit, "30-70888333-2", CondicionIvaEnum.ResponsableInscripto, "(011) 4555-0203", null, 300000m),
        new("Inmobiliaria Del Parque S.A.", TipoDocumentoEnum.Cuit, "33-70888444-3", CondicionIvaEnum.ResponsableInscripto, null, "info@delparque.example.com")
    ];

    public static bool EsDelMayoristaEscolar(string categoria)
    {
        return CategoriasDelMayoristaEscolar.Contains(categoria);
    }

    private static IEnumerable<ProductoDemo> Familia(
        string categoria,
        string? marca,
        string descripcion,
        decimal costo,
        decimal margen,
        params string[] variantes)
    {
        if (variantes.Length == 0)
        {
            return [new ProductoDemo(descripcion, categoria, marca, costo, margen)];
        }

        return variantes.Select(variante => new ProductoDemo($"{descripcion} {variante}", categoria, marca, costo, margen));
    }
}
