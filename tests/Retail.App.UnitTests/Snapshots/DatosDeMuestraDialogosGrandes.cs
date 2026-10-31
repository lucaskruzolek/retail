using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Domain.Enums;

namespace Retail.App.UnitTests.Snapshots;

/// <summary>
/// Datos de ejemplo realistas (librería argentina) para las capturas de los diálogos grandes con grilla de
/// <see cref="DialogosGrandesSnapshotTests"/>. Cada método devuelve colecciones nuevas, con entre 6 y 10 filas, para
/// que se vean tablas, acciones de fila y secciones completas.
/// </summary>
internal static class DatosDeMuestraDialogosGrandes
{
    // ------------------------------------------------------------------ Clasificación

    public static List<RubroDto> Rubros()
    {
        return
        [
            new RubroDto { IdRubro = 1, NombreRubro = "Escolar", CantidadEnUso = 214 },
            new RubroDto { IdRubro = 2, NombreRubro = "Oficina", CantidadEnUso = 138 },
            new RubroDto { IdRubro = 3, NombreRubro = "Librería", CantidadEnUso = 96 },
            new RubroDto { IdRubro = 4, NombreRubro = "Arte y manualidades", CantidadEnUso = 71 },
            new RubroDto { IdRubro = 5, NombreRubro = "Papelería", CantidadEnUso = 64 },
            new RubroDto { IdRubro = 6, NombreRubro = "Juguetería", CantidadEnUso = 38 },
            new RubroDto { IdRubro = 7, NombreRubro = "Regalería", CantidadEnUso = 0 }
        ];
    }

    public static List<CategoriaDto> Categorias()
    {
        return
        [
            new CategoriaDto { IdCategoria = 1, NombreCategoria = "Cuadernos", IdRubro = 1, NombreRubro = "Escolar", CantidadEnUso = 58 },
            new CategoriaDto { IdCategoria = 2, NombreCategoria = "Carpetas", IdRubro = 1, NombreRubro = "Escolar", CantidadEnUso = 31 },
            new CategoriaDto { IdCategoria = 3, NombreCategoria = "Lápices", IdRubro = 1, NombreRubro = "Escolar", CantidadEnUso = 42 },
            new CategoriaDto { IdCategoria = 4, NombreCategoria = "Lapiceras y bolígrafos", IdRubro = 1, NombreRubro = "Escolar", CantidadEnUso = 47 },
            new CategoriaDto { IdCategoria = 5, NombreCategoria = "Marcadores", IdRubro = 1, NombreRubro = "Escolar", CantidadEnUso = 19 },
            new CategoriaDto { IdCategoria = 6, NombreCategoria = "Mochilas", IdRubro = 1, NombreRubro = "Escolar", CantidadEnUso = 12 },
            new CategoriaDto { IdCategoria = 7, NombreCategoria = "Resmas y hojas", IdRubro = 2, NombreRubro = "Oficina", CantidadEnUso = 24 },
            new CategoriaDto { IdCategoria = 8, NombreCategoria = "Adhesivos", IdRubro = 2, NombreRubro = "Oficina", CantidadEnUso = 17 },
            new CategoriaDto { IdCategoria = 9, NombreCategoria = "Tijeras", IdRubro = 2, NombreRubro = "Oficina", CantidadEnUso = 8 }
        ];
    }

    public static List<MarcaDto> Marcas()
    {
        return
        [
            new MarcaDto { IdMarca = 1, NombreMarca = "Rivadavia", CantidadEnUso = 44 },
            new MarcaDto { IdMarca = 2, NombreMarca = "Faber-Castell", CantidadEnUso = 36 },
            new MarcaDto { IdMarca = 3, NombreMarca = "Bic", CantidadEnUso = 29 },
            new MarcaDto { IdMarca = 4, NombreMarca = "Filgo", CantidadEnUso = 21 },
            new MarcaDto { IdMarca = 5, NombreMarca = "Maped", CantidadEnUso = 18 },
            new MarcaDto { IdMarca = 6, NombreMarca = "Staedtler", CantidadEnUso = 11 },
            new MarcaDto { IdMarca = 7, NombreMarca = "Pelikan", CantidadEnUso = 7 },
            new MarcaDto { IdMarca = 8, NombreMarca = "Ledesma", CantidadEnUso = 0 }
        ];
    }

    public static List<AliasListadoDto> AliasDeRivadavia()
    {
        return
        [
            new AliasListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Alias = "RIV" },
            new AliasListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Alias = "RIVADAV." },
            new AliasListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Alias = "CUAD. RIVADAVIA" },
            new AliasListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Alias = "PAPELERA RIVADAVIA" }
        ];
    }

    public static List<PrefijoListadoDto> PrefijosDeRivadavia()
    {
        return
        [
            new PrefijoListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Prefijo = "7790001" },
            new PrefijoListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Prefijo = "7798145" },
            new PrefijoListadoDto { IdMarca = 1, NombreMarca = "Rivadavia", Prefijo = "7791234" }
        ];
    }

    public static List<TerminoDto> Terminos()
    {
        return
        [
            new TerminoDto { IdTermino = 1, Termino = "CUADERNO" },
            new TerminoDto { IdTermino = 2, Termino = "CARPETA" },
            new TerminoDto { IdTermino = 3, Termino = "LAPIZ", IdCategoria = 3, NombreCategoria = "Lápices", NombreRubro = "Escolar" },
            new TerminoDto { IdTermino = 4, Termino = "LAPICERA", IdCategoria = 4, NombreCategoria = "Lapiceras y bolígrafos", NombreRubro = "Escolar" },
            new TerminoDto { IdTermino = 5, Termino = "MARCADOR", IdCategoria = 5, NombreCategoria = "Marcadores", NombreRubro = "Escolar" },
            new TerminoDto { IdTermino = 6, Termino = "MOCHILA", IdCategoria = 6, NombreCategoria = "Mochilas", NombreRubro = "Escolar" },
            new TerminoDto { IdTermino = 7, Termino = "RESMA", IdCategoria = 7, NombreCategoria = "Resmas y hojas", NombreRubro = "Oficina" },
            new TerminoDto { IdTermino = 8, Termino = "TIJERA", IdCategoria = 9, NombreCategoria = "Tijeras", NombreRubro = "Oficina" }
        ];
    }

    /// <summary>El término ambiguo ("CUADERNO") cuyo detalle se muestra a la derecha en las capturas.</summary>
    public static TerminoDto TerminoAmbiguo()
    {
        return Terminos()[0];
    }

    public static List<ParDto> ParesDeCuaderno()
    {
        return
        [
            new ParDto { IdTermino = 1, Termino = "CUADERNO", SegundaPalabra = "ESPIRAL", IdCategoria = 1, NombreCategoria = "Cuadernos", NombreRubro = "Escolar" },
            new ParDto { IdTermino = 1, Termino = "CUADERNO", SegundaPalabra = "TAPA", IdCategoria = 1, NombreCategoria = "Cuadernos", NombreRubro = "Escolar" },
            new ParDto { IdTermino = 1, Termino = "CUADERNO", SegundaPalabra = "UNIVERSITARIO", IdCategoria = 1, NombreCategoria = "Cuadernos", NombreRubro = "Escolar" },
            new ParDto { IdTermino = 1, Termino = "CUADERNO", SegundaPalabra = "DIBUJO", IdCategoria = 2, NombreCategoria = "Carpetas", NombreRubro = "Escolar" },
            new ParDto { IdTermino = 1, Termino = "CUADERNO", SegundaPalabra = "ABC", IdCategoria = null },
            new ParDto { IdTermino = 1, Termino = "CUADERNO", SegundaPalabra = "COMUNICACIONES", IdCategoria = 1, NombreCategoria = "Cuadernos", NombreRubro = "Escolar" }
        ];
    }

    public static List<ParPropuestoDto> ParesPropuestosDeCuaderno()
    {
        return
        [
            new ParPropuestoDto
            {
                IdTermino = 1,
                Termino = "CUADERNO",
                SegundaPalabra = "TAPA",
                IdCategoria = 1,
                NombreCategoria = "Cuadernos",
                Coincidencias = 18,
                Total = 18,
                Ejemplos = ["CUADERNO TAPA DURA 48H RAYADO", "CUADERNO TAPA BLANDA 24H"]
            },
            new ParPropuestoDto
            {
                IdTermino = 1,
                Termino = "CUADERNO",
                SegundaPalabra = "ANOTADOR",
                IdCategoria = 2,
                NombreCategoria = "Carpetas",
                Coincidencias = 7,
                Total = 9,
                Ejemplos = ["CUADERNO ANOTADOR A5", "CUADERNO ANOTADOR EJECUTIVO"]
            },
            new ParPropuestoDto
            {
                IdTermino = 1,
                Termino = "CUADERNO",
                SegundaPalabra = "PENTAGRAMA",
                IdCategoria = 1,
                NombreCategoria = "Cuadernos",
                Coincidencias = 5,
                Total = 5,
                Ejemplos = ["CUADERNO PENTAGRAMA 24H"]
            },
            new ParPropuestoDto
            {
                IdTermino = 1,
                Termino = "CUADERNO",
                SegundaPalabra = "BITACORA",
                IdCategoria = 1,
                NombreCategoria = "Cuadernos",
                Coincidencias = 4,
                Total = 6,
                Ejemplos = ["CUADERNO BITACORA 100H", "CUADERNO BITACORA CUADRICULADO"]
            },
            new ParPropuestoDto
            {
                IdTermino = 1,
                Termino = "CUADERNO",
                SegundaPalabra = "RECETAS",
                IdCategoria = 2,
                NombreCategoria = "Carpetas",
                Coincidencias = 3,
                Total = 3,
                Ejemplos = ["CUADERNO RECETAS ESPIRAL"]
            },
            new ParPropuestoDto
            {
                IdTermino = 1,
                Termino = "CUADERNO",
                SegundaPalabra = "MUSICA",
                IdCategoria = 1,
                NombreCategoria = "Cuadernos",
                Coincidencias = 6,
                Total = 6,
                Ejemplos = ["CUADERNO MUSICA 40H"]
            }
        ];
    }

    // ------------------------------------------------------------------ Equivalencias del proveedor

    public static ProveedorDto Proveedor()
    {
        return new ProveedorDto
        {
            IdProveedor = 1,
            RazonSocial = "Distribuidora Norte S.R.L.",
            Cuit = "30-70123456-7",
            Telefono = "011 4555-0123",
            Email = "ventas@distribuidoranorte.com.ar"
        };
    }

    public static List<EquivalenciaDto> Equivalencias()
    {
        return
        [
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "ESCOLAR", Texto = "CUADERNOS", IdCategoria = 1, NombreCategoria = "Cuadernos" },
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "ESCOLAR", Texto = "CARPETAS Y ARCHIVO", IdCategoria = 2, NombreCategoria = "Carpetas" },
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "ESCOLAR", Texto = "ESCRITURA", IdCategoria = 4, NombreCategoria = "Lapiceras y bolígrafos" },
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "OFICINA", Texto = "PAPEL P/ IMPRESION", IdCategoria = 7, NombreCategoria = "Resmas y hojas" },
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "OFICINA", Texto = "VARIOS" },
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "FABER CASTELL ARG.", IdMarca = 2, NombreMarca = "Faber-Castell" },
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "BIC ARGENTINA", IdMarca = 3, NombreMarca = "Bic" },
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "FILGO S.A.", IdMarca = 4, NombreMarca = "Filgo" },
            new EquivalenciaDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "GENERICO" }
        ];
    }

    // ------------------------------------------------------------------ Incorporar artículos

    public static List<CatalogoProveedorDto> ItemsDeCatalogo()
    {
        return
        [
            ItemDeCatalogo(101, "CU-A4-48", "7790001000011", "CUADERNO TAPA DURA A4 48H RAYADO RIVADAVIA", 2150m),
            ItemDeCatalogo(102, "CU-UNI-84", "7790001000028", "CUADERNO UNIVERSITARIO ESPIRAL 84H CUADRICULADO", 2890m),
            ItemDeCatalogo(103, "LAP-HB-12", "7790002000035", "LAPIZ NEGRO HB CAJA X12 FABER CASTELL", 3400m),
            ItemDeCatalogo(104, "BOL-AZ-50", "7790003000042", "BOLIGRAFO CRISTAL AZUL CAJA X50 BIC", 5200m),
            ItemDeCatalogo(105, "MAR-PZ-8", "7790004000059", "MARCADOR PIZARRA SET X8 COLORES FILGO", 4750m),
            ItemDeCatalogo(106, "RES-A4-75", "7790005000066", "RESMA A4 75 G X500 HOJAS", 8900m),
            ItemDeCatalogo(107, "TIJ-17", null, "TIJERA ESCOLAR PUNTA REDONDA 17 CM MAPED", 1650m),
            ItemDeCatalogo(108, "PEG-VIN-250", "7790006000073", "ADHESIVO VINILICO 250 G", 1980m)
        ];
    }

    private static CatalogoProveedorDto ItemDeCatalogo(int id, string codigo, string? codigoBarras, string descripcion, decimal costo)
    {
        return new CatalogoProveedorDto
        {
            Id = id,
            IdProveedor = 1,
            ProveedorRazonSocial = "Distribuidora Norte S.R.L.",
            CodigoProveedor = codigo,
            CodigoBarras = codigoBarras,
            DescripcionProveedor = descripcion,
            CostoReposicion = costo,
            FechaActualizacion = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    /// <summary>Sugerencias del motor para los ítems de <see cref="ItemsDeCatalogo"/> (los dos últimos quedan sin sugerir).</summary>
    public static List<SugerenciaItemDto> Sugerencias()
    {
        return
        [
            Sugerencia(101, "CUADERNO", 1, "Cuadernos", MotivoSugerenciaEnum.ParDelDiccionario, 1, "Rivadavia", MotivoSugerenciaEnum.MarcaEnLaDescripcion),
            Sugerencia(102, "CUADERNO", 1, "Cuadernos", MotivoSugerenciaEnum.ParDelDiccionario, null, null, null),
            Sugerencia(103, "LAPIZ", 3, "Lápices", MotivoSugerenciaEnum.Termino, 2, "Faber-Castell", MotivoSugerenciaEnum.MarcaEnLaDescripcion),
            Sugerencia(104, "BOLIGRAFO", 4, "Lapiceras y bolígrafos", MotivoSugerenciaEnum.EquivalenciaDelProveedor, 3, "Bic", MotivoSugerenciaEnum.MarcaInformadaPorElProveedor),
            Sugerencia(105, "MARCADOR", 5, "Marcadores", MotivoSugerenciaEnum.Termino, 4, "Filgo", MotivoSugerenciaEnum.MarcaEnLaDescripcion),
            Sugerencia(106, "RESMA", 7, "Resmas y hojas", MotivoSugerenciaEnum.Termino, null, null, null)
        ];
    }

    private static SugerenciaItemDto Sugerencia(
        int idCatalogo,
        string termino,
        int idCategoria,
        string categoria,
        MotivoSugerenciaEnum motivoCategoria,
        int? idMarca,
        string? marca,
        MotivoSugerenciaEnum? motivoMarca)
    {
        return new SugerenciaItemDto
        {
            IdCatalogo = idCatalogo,
            Descripcion = string.Empty,
            Termino = termino,
            Categoria = new SugerenciaDto { Id = idCategoria, Nombre = categoria, Motivo = motivoCategoria },
            Marca = idMarca is { } id && marca is not null && motivoMarca is { } motivo
                ? new SugerenciaDto { Id = id, Nombre = marca, Motivo = motivo }
                : null
        };
    }

    public static List<ValorSinTraducirDto> ValoresSinTraducir()
    {
        return
        [
            new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "ESCOLAR", Texto = "CUADERNOS Y BLOCKS", CantidadDeItems = 14 },
            new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "ESCOLAR", Texto = "UTILES VARIOS", CantidadDeItems = 9 },
            new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Categoria, TextoRubro = "OFICINA", Texto = "PAPEL P/ IMPRESION", CantidadDeItems = 5 },
            new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "FABER CASTELL ARG.", CantidadDeItems = 11 },
            new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "BIC ARGENTINA", CantidadDeItems = 8 },
            new ValorSinTraducirDto { Tipo = TipoEquivalenciaEnum.Marca, Texto = "GENERICO", CantidadDeItems = 6 }
        ];
    }

    public static List<BloqueTerminoNuevoDto> BloquesDeTerminosNuevos()
    {
        return
        [
            new BloqueTerminoNuevoDto
            {
                Termino = "BITACORA",
                IdsCatalogo = [201, 202, 203, 204],
                Ejemplos = ["BITACORA 100H CUADRICULADO", "BITACORA A5 TAPA DURA"]
            },
            new BloqueTerminoNuevoDto
            {
                Termino = "ACUARELA",
                IdsCatalogo = [205, 206, 207],
                Ejemplos = ["ACUARELA CAJA X12 PASTILLAS", "ACUARELA PROFESIONAL X24"]
            },
            new BloqueTerminoNuevoDto
            {
                Termino = "CORRECTOR",
                IdsCatalogo = [208, 209],
                Ejemplos = ["CORRECTOR LIQUIDO 20 ML", "CORRECTOR EN CINTA 5 MM"]
            },
            new BloqueTerminoNuevoDto
            {
                Termino = "SACAPUNTAS",
                IdsCatalogo = [210],
                Ejemplos = ["SACAPUNTAS METALICO DOBLE"]
            }
        ];
    }

    // ------------------------------------------------------------------ Importar planilla

    public static ResultadoImportacionDto ResultadoDeImportacionConObservaciones()
    {
        return new ResultadoImportacionDto
        {
            TotalFilasProcesadas = 1284,
            PreciosActualizados = 962,
            NuevosRegistros = 318,
            FilasConError = 4,
            ErroresDetalle =
            [
                "Fila 37: el precio '12,5O' no es un número válido.",
                "Fila 112: falta el código del proveedor.",
                "Fila 540: la descripción supera los 200 caracteres.",
                "Fila 981: el precio no puede ser negativo."
            ],
            TiempoTranscurrido = TimeSpan.FromSeconds(3.4)
        };
    }

    // ------------------------------------------------------------------ Pedir artículos

    public static VistaPreviaPedidoDto VistaPreviaDePedido()
    {
        return new VistaPreviaPedidoDto
        {
            Destinos =
            [
                new DestinoPedidoDto { IdProveedor = 1, RazonSocialProveedor = "Distribuidora Norte S.R.L.", IdBorrador = 7, CantidadRenglonesBorrador = 2 },
                new DestinoPedidoDto { IdProveedor = 2, RazonSocialProveedor = "Papelera del Sur S.A." },
                new DestinoPedidoDto { IdProveedor = 3, RazonSocialProveedor = "Mayorista Escolar Cuyo" }
            ],
            Renglones =
            [
                Renglon(10, 1, "CU-A4-48", "Cuaderno tapa dura A4 48H", 2150m, 3, 12, 4),
                Renglon(11, 1, "LAP-HB-12", "Lápiz negro HB caja x12", 3400m, 1, 6, 0),
                Renglon(12, 1, "BOL-AZ-50", "Bolígrafo cristal azul caja x50", 5200m, 2, 5, 2),
                Renglon(20, 2, "RES-A4-75", "Resma A4 75 g x500", 8900m, 0, 10, 0),
                Renglon(21, 2, "ADH-VIN-250", "Adhesivo vinílico 250 g", 1980m, 4, 8, 0),
                Renglon(30, 3, "MAR-PZ-8", "Marcador pizarra set x8", 4750m, 2, 6, 0),
                Renglon(31, 3, "TIJ-17", "Tijera escolar punta redonda 17 cm", 1650m, 3, 10, 0)
            ],
            Omitidos =
            [
                new RenglonOmitidoDto
                {
                    IdCatalogoProveedor = 40,
                    CodigoProveedor = "ACU-12",
                    Descripcion = "Acuarela caja x12 pastillas",
                    Motivo = MotivoOmisionPedidoEnum.SinIncorporar
                }
            ]
        };
    }

    private static RenglonVistaPreviaDto Renglon(
        int idCatalogo,
        int idProveedor,
        string codigo,
        string descripcion,
        decimal costo,
        int stock,
        int minimo,
        int enBorrador)
    {
        return new RenglonVistaPreviaDto
        {
            IdCatalogoProveedor = idCatalogo,
            IdProveedor = idProveedor,
            CodigoProveedor = codigo,
            Descripcion = descripcion,
            CostoActual = costo,
            IdArticulo = idCatalogo,
            StockActual = stock,
            StockMinimo = minimo,
            CantidadEnBorrador = enBorrador
        };
    }

    // ------------------------------------------------------------------ Selector de catálogo y de cliente

    public static List<CatalogoProveedorDto> ItemsParaSelectorDeCatalogo()
    {
        return
        [
            ItemDeCatalogo(1, "N-001", "7790001000011", "Cuaderno A4 rayado 48H tapa dura", 2150m),
            ItemDeCatalogo(2, "N-002", "7790001000028", "Cuaderno universitario espiral 84H", 2890m),
            ItemDeCatalogo(3, "N-003", "7790001000035", "Cuaderno A4 cuadriculado 96H", 2480m),
            ItemDeCatalogo(4, "N-004", null, "Cuaderno de dibujo A4 40H", 1760m),
            ItemDeCatalogo(5, "N-005", "7790001000059", "Cuaderno tapa blanda 24H", 1190m) with
            {
                EstaVinculado = true,
                IdArticuloVinculado = 99,
                NombreArticuloTienda = "Cuaderno Rivadavia tapa blanda 24H"
            },
            ItemDeCatalogo(6, "N-006", "7790001000066", "Cuaderno anillado A5 80H", 2230m),
            ItemDeCatalogo(7, "N-007", "7790001000073", "Cuaderno pentagrama 24H", 1540m) with
            {
                EstaVinculado = true,
                IdArticuloVinculado = 7,
                NombreArticuloTienda = "Cuaderno A4 rayado"
            },
            ItemDeCatalogo(8, "N-008", "7790001000080", "Cuaderno bitácora cuadriculado 100H", 3120m)
        ];
    }

    public static List<ProveedorDto> Proveedores()
    {
        return
        [
            Proveedor(),
            new ProveedorDto { IdProveedor = 2, RazonSocial = "Papelera del Sur S.A.", Cuit = "30-65432109-8" },
            new ProveedorDto { IdProveedor = 3, RazonSocial = "Mayorista Escolar Cuyo", Cuit = "30-71234567-1" }
        ];
    }

    public static List<ClienteDto> Clientes()
    {
        return
        [
            Cliente(1, "Rodríguez, María Elena", TipoDocumentoEnum.Dni, "28456123", CondicionIvaEnum.ConsumidorFinal, false, 0m, 0m),
            Cliente(2, "Colegio San Martín", TipoDocumentoEnum.Cuit, "30-51234567-9", CondicionIvaEnum.Exento, true, 250000m, 84500m),
            Cliente(3, "Fernández, Julián", TipoDocumentoEnum.Dni, "33987654", CondicionIvaEnum.Monotributo, false, 0m, 0m),
            Cliente(4, "Estudio Contable Giménez y Asoc.", TipoDocumentoEnum.Cuit, "30-70998877-4", CondicionIvaEnum.ResponsableInscripto, true, 150000m, 0m),
            Cliente(5, "Sosa, Lucía Belén", TipoDocumentoEnum.Dni, "40123789", CondicionIvaEnum.ConsumidorFinal, false, 0m, 0m),
            Cliente(6, "Jardín de Infantes N° 12", TipoDocumentoEnum.Cuit, "30-68123456-2", CondicionIvaEnum.Exento, true, 80000m, 61200m),
            Cliente(7, "Pérez, Ricardo Daniel", TipoDocumentoEnum.Dni, "22345678", CondicionIvaEnum.Monotributo, false, 0m, 0m),
            Cliente(8, "Academia de Idiomas del Plata", TipoDocumentoEnum.Cuit, "30-71555444-0", CondicionIvaEnum.ResponsableInscripto, true, 120000m, 15900m)
        ];
    }

    private static ClienteDto Cliente(
        int id,
        string nombre,
        TipoDocumentoEnum tipoDocumento,
        string documento,
        CondicionIvaEnum condicionIva,
        bool cuentaCorriente,
        decimal limite,
        decimal saldo)
    {
        return new ClienteDto
        {
            IdCliente = id,
            RazonSocialONombre = nombre,
            TipoDocumento = tipoDocumento,
            NumeroDocumento = documento,
            CondicionIva = condicionIva,
            TieneCuentaCorriente = cuentaCorriente,
            LimiteCredito = limite,
            SaldoCuentaCorriente = saldo
        };
    }
}
