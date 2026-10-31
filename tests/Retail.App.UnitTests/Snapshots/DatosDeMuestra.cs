using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clientes;
using Retail.Domain.Enums;

namespace Retail.App.UnitTests.Snapshots;

/// <summary>
/// Datos de muestra realistas de una librería para poblar las vistas en las capturas (<see cref="VistaSnapshotTests"/>).
/// Cubren los casos visuales relevantes: stock normal, bajo el mínimo y en cero, servicios, presentaciones derivadas y
/// artículos con varias fuentes.
/// </summary>
internal static class DatosDeMuestra
{
    public static IReadOnlyList<CategoriaDto> Categorias()
    {
        return
        [
            new CategoriaDto { IdCategoria = 1, NombreCategoria = "Cuadernos", IdRubro = 1, NombreRubro = "Papelería" },
            new CategoriaDto { IdCategoria = 2, NombreCategoria = "Escritura", IdRubro = 1, NombreRubro = "Papelería" },
            new CategoriaDto { IdCategoria = 3, NombreCategoria = "Papel para impresión", IdRubro = 2, NombreRubro = "Oficina" },
            new CategoriaDto { IdCategoria = 4, NombreCategoria = "Servicios", IdRubro = 3, NombreRubro = "Servicios" }
        ];
    }

    public static IReadOnlyList<RubroDto> Rubros()
    {
        return
        [
            new RubroDto { IdRubro = 1, NombreRubro = "Papelería" },
            new RubroDto { IdRubro = 2, NombreRubro = "Oficina" },
            new RubroDto { IdRubro = 3, NombreRubro = "Servicios" }
        ];
    }

    public static IReadOnlyList<MarcaDto> Marcas()
    {
        return
        [
            new MarcaDto { IdMarca = 1, NombreMarca = "Rivadavia" },
            new MarcaDto { IdMarca = 2, NombreMarca = "Faber-Castell" },
            new MarcaDto { IdMarca = 3, NombreMarca = "Autor" },
            new MarcaDto { IdMarca = 4, NombreMarca = "Bic" }
        ];
    }

    public static IReadOnlyList<ArticuloDto> ArticulosPagina()
    {
        return
        [
            Articulo(1, "Cuaderno Rivadavia ABC 50 hojas rayado", "7790001000011", 1, "Cuadernos", 1, "Rivadavia", 1850m, 60m, 42, 10, "Distribuidora Sur", 1),
            Articulo(2, "Lápiz negro Faber-Castell HB", "7790002000028", 2, "Escritura", 2, "Faber-Castell", 320m, 80m, 8, 24, "Distribuidora Sur", 2),
            Articulo(3, "Resma A4 Autor 75g x 500 hojas", "7790003000035", 3, "Papel para impresión", 3, "Autor", 6900m, 35m, 0, 5, "Papelera del Plata", 1),
            Articulo(4, "Birome Bic Cristal azul", "7790004000042", 2, "Escritura", 4, "Bic", 280m, 90m, 150, 50, "Distribuidora Sur", 3),
            Articulo(5, "Cuaderno Rivadavia ABC 100 hojas cuadriculado", "7790001000059", 1, "Cuadernos", 1, "Rivadavia", 2900m, 60m, 3, 8, null, 0),
            Articulo(6, "Fotocopia simple faz (servicio)", null, 4, "Servicios", null, null, 40m, 150m, 0, 0, null, 0, esServicio: true),
            Articulo(7, "Sobre manila oficio (pack x100)", "7790005000066", 3, "Papel para impresión", null, null, 4200m, 40m, 12, 4, "Papelera del Plata", 1),
            Articulo(8, "Sobre manila oficio (unidad)", null, 3, "Papel para impresión", null, null, 42m, 40m, 35, 20, "Papelera del Plata", 1, idArticuloOrigen: 7, unidadesPorOrigen: 100),
            Articulo(9, "Corrector líquido Bic Wite-Out", "7790006000073", 2, "Escritura", 4, "Bic", 950m, 70m, 18, 6, "Distribuidora Sur", 2),
            Articulo(10, "Anillado hasta 100 hojas (servicio)", null, 4, "Servicios", null, null, 1500m, 120m, 0, 0, null, 0, esServicio: true)
        ];
    }

    public static IReadOnlyList<ClienteDto> Clientes()
    {
        return
        [
            Cliente(1, "Librería Central S.R.L.", TipoDocumentoEnum.Cuit, "30712345678", CondicionIvaEnum.ResponsableInscripto, "Av. Corrientes 1234, CABA", "011 4321-5566", "compras@libreriacentral.com.ar", true, 500000m, 185000m),
            Cliente(2, "Colegio San Martín", TipoDocumentoEnum.Cuit, "30554433221", CondicionIvaEnum.Exento, "Belgrano 450, Rosario", "0341 422-7788", "administracion@sanmartin.edu.ar", true, 300000m, 0m),
            Cliente(3, "María Laura Gómez", TipoDocumentoEnum.Dni, "27456789", CondicionIvaEnum.ConsumidorFinal, null, "11 5555-1234", "mlgomez@mail.com", false, 0m, 0m),
            Cliente(4, "Estudio Contable Ferrari", TipoDocumentoEnum.Cuit, "20301234569", CondicionIvaEnum.Monotributo, "San Lorenzo 88, Córdoba", "0351 489-2200", "info@estudioferrari.com.ar", true, 120000m, 118500m),
            Cliente(5, "Juan Pablo Rossi", TipoDocumentoEnum.Dni, "33123456", CondicionIvaEnum.ConsumidorFinal, null, null, null, false, 0m, 0m),
            Cliente(6, "Papelera Los Andes S.A.", TipoDocumentoEnum.Cuit, "30698765432", CondicionIvaEnum.ResponsableInscripto, "Ruta 7 km 12, Mendoza", "0261 431-9090", "ventas@papeleralosandes.com.ar", true, 800000m, 410000m),
            Cliente(7, "Instituto Belgrano", TipoDocumentoEnum.Cuit, "30667788990", CondicionIvaEnum.Exento, "Mitre 920, La Plata", "0221 455-3030", null, true, 200000m, 45000m),
            Cliente(8, "Lucía Fernández", TipoDocumentoEnum.Dni, "38765432", CondicionIvaEnum.ConsumidorFinal, null, "11 6677-8899", "lucia.fernandez@mail.com", false, 0m, 0m)
        ];
    }

    private static ArticuloDto Articulo(
        int id,
        string descripcion,
        string? codigoBarras,
        int idCategoria,
        string categoria,
        int? idMarca,
        string? marca,
        decimal costo,
        decimal porcentajeGanancia,
        int stock,
        int stockMinimo,
        string? proveedor,
        int cantidadFuentes,
        bool esServicio = false,
        int? idArticuloOrigen = null,
        int? unidadesPorOrigen = null)
    {
        return new ArticuloDto
        {
            IdArticulo = id,
            CodigoBarras = codigoBarras,
            Descripcion = descripcion,
            IdCategoria = idCategoria,
            CategoriaNombre = categoria,
            IdMarca = idMarca,
            MarcaNombre = marca,
            IdCatalogoProveedor = proveedor is null ? null : 100 + id,
            ProveedorNombre = proveedor,
            CantidadFuentes = cantidadFuentes,
            IdArticuloOrigen = idArticuloOrigen,
            UnidadesPorOrigen = unidadesPorOrigen,
            CostoReposicion = costo,
            PorcentajeGanancia = porcentajeGanancia,
            PrecioVenta = Math.Round(costo * (1 + (porcentajeGanancia / 100m)), 0),
            StockActual = stock,
            StockMinimo = stockMinimo,
            EsServicio = esServicio
        };
    }

    private static ClienteDto Cliente(
        int id,
        string nombre,
        TipoDocumentoEnum tipoDocumento,
        string numeroDocumento,
        CondicionIvaEnum condicionIva,
        string? domicilio,
        string? telefono,
        string? email,
        bool tieneCuentaCorriente,
        decimal limiteCredito,
        decimal saldo)
    {
        return new ClienteDto
        {
            IdCliente = id,
            RazonSocialONombre = nombre,
            TipoDocumento = tipoDocumento,
            NumeroDocumento = numeroDocumento,
            CondicionIva = condicionIva,
            DomicilioFiscal = domicilio,
            Telefono = telefono,
            Email = email,
            TieneCuentaCorriente = tieneCuentaCorriente,
            LimiteCredito = limiteCredito,
            SaldoCuentaCorriente = saldo
        };
    }
}
