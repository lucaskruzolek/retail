using Microsoft.EntityFrameworkCore;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Semillero inicial de datos para inicialización de base de datos en primer arranque (Etapa 0.7).
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(RetailDbContext context, IPasswordHasher passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        // Si ya existen roles cargados, la base de datos ya fue inicializada previamente
        if (await context.Roles.AnyAsync())
        {
            return;
        }

        // 1. Semillero de Roles (RBAC)
        var rolCajero = new Rol
        {
            NombreRol = nameof(RolUsuarioEnum.Cajero),
            Descripcion = "Operador de punto de venta, mostrador y cobranzas"
        };

        var rolEncargado = new Rol
        {
            NombreRol = nameof(RolUsuarioEnum.Encargado),
            Descripcion = "Encargado de inventario, compras a distribuidores y control de caja"
        };

        var rolGerente = new Rol
        {
            NombreRol = nameof(RolUsuarioEnum.Gerente),
            Descripcion = "Administrador general con acceso total, consola fiscal y configuración"
        };

        await context.Roles.AddRangeAsync(rolCajero, rolEncargado, rolGerente);
        await context.SaveChangesAsync();

        // 2. Semillero de Usuario Administrador Inicial
        var usuarioAdmin = new Usuario
        {
            NombreUsuario = "admin",
            NombreCompleto = "Administrador General",
            PasswordHash = passwordHasher.HashPassword("Admin123!"),
            IdRol = rolGerente.Id
        };

        await context.Usuarios.AddAsync(usuarioAdmin);
        await context.SaveChangesAsync();

        // 3. Semillero de Categorías Base
        var catEscolar = new Categoria { NombreCategoria = "Escolar" };
        var catOficina = new Categoria { NombreCategoria = "Oficina y Comercial" };
        var catArtistica = new Categoria { NombreCategoria = "Artística y Dibujo" };
        var catLiteratura = new Categoria { NombreCategoria = "Textos y Literatura" };
        var catArtesanias = new Categoria { NombreCategoria = "Regalería y Artesanías" };

        await context.Categorias.AddRangeAsync(catEscolar, catOficina, catArtistica, catLiteratura, catArtesanias);

        // 4. Semillero de Marcas Base
        var marcaRivadavia = new Marca { NombreMarca = "Rivadavia" };
        var marcaBic = new Marca { NombreMarca = "Bic" };
        var marcaFaber = new Marca { NombreMarca = "Faber-Castell" };
        var marcaMaped = new Marca { NombreMarca = "Maped" };
        var marcaPelikan = new Marca { NombreMarca = "Pelikan" };
        var marcaArtesanal = new Marca { NombreMarca = "Creaciones Artesanales" };

        await context.Marcas.AddRangeAsync(marcaRivadavia, marcaBic, marcaFaber, marcaMaped, marcaPelikan, marcaArtesanal);
        await context.SaveChangesAsync();

        // 5. Semillero de 20 Artículos Simulados (incluye 2 artesanías con código NULL para RF-04)
        var articulos = new List<Articulo>
        {
            new()
            {
                CodigoBarras = "7791234567011",
                Descripcion = "Cuaderno Rivadavia Tapa Dura Rayado 48h",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaRivadavia.Id,
                CostoReposicion = 3200.00m,
                PorcentajeGanancia = 50.00m,
                PrecioVenta = 4800.00m,
                StockActual = 45,
                StockMinimo = 10,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567028",
                Descripcion = "Cuaderno Rivadavia Cuadriculado 48h",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaRivadavia.Id,
                CostoReposicion = 3200.00m,
                PorcentajeGanancia = 50.00m,
                PrecioVenta = 4800.00m,
                StockActual = 30,
                StockMinimo = 10,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567035",
                Descripcion = "Bolígrafo Bic Cristal Azul 1.0mm",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaBic.Id,
                CostoReposicion = 450.00m,
                PorcentajeGanancia = 60.00m,
                PrecioVenta = 720.00m,
                StockActual = 150,
                StockMinimo = 25,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567042",
                Descripcion = "Bolígrafo Bic Cristal Negro 1.0mm",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaBic.Id,
                CostoReposicion = 450.00m,
                PorcentajeGanancia = 60.00m,
                PrecioVenta = 720.00m,
                StockActual = 120,
                StockMinimo = 25,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567059",
                Descripcion = "Bolígrafo Bic Cristal Rojo 1.0mm",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaBic.Id,
                CostoReposicion = 450.00m,
                PorcentajeGanancia = 60.00m,
                PrecioVenta = 720.00m,
                StockActual = 80,
                StockMinimo = 20,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567066",
                Descripcion = "Lápiz Grafito Faber-Castell 2B",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaFaber.Id,
                CostoReposicion = 380.00m,
                PorcentajeGanancia = 55.00m,
                PrecioVenta = 589.00m,
                StockActual = 90,
                StockMinimo = 20,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567073",
                Descripcion = "Lápices de Colores Faber-Castell x12",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaFaber.Id,
                CostoReposicion = 4100.00m,
                PorcentajeGanancia = 45.00m,
                PrecioVenta = 5945.00m,
                StockActual = 25,
                StockMinimo = 5,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567080",
                Descripcion = "Lápices de Colores Faber-Castell x24",
                IdCategoria = catArtistica.Id,
                IdMarca = marcaFaber.Id,
                CostoReposicion = 7800.00m,
                PorcentajeGanancia = 45.00m,
                PrecioVenta = 11310.00m,
                StockActual = 15,
                StockMinimo = 4,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567097",
                Descripcion = "Goma de Borrar Maped Technic 600",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaMaped.Id,
                CostoReposicion = 320.00m,
                PorcentajeGanancia = 65.00m,
                PrecioVenta = 528.00m,
                StockActual = 70,
                StockMinimo = 15,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567103",
                Descripcion = "Tijera Escolar Maped Sensoft 13cm",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaMaped.Id,
                CostoReposicion = 1850.00m,
                PorcentajeGanancia = 50.00m,
                PrecioVenta = 2775.00m,
                StockActual = 20,
                StockMinimo = 5,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567110",
                Descripcion = "Sacapuntas Metálico Maped Doble",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaMaped.Id,
                CostoReposicion = 820.00m,
                PorcentajeGanancia = 50.00m,
                PrecioVenta = 1230.00m,
                StockActual = 40,
                StockMinimo = 10,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567127",
                Descripcion = "Resaltador Pelikan Fluo Amarillo",
                IdCategoria = catOficina.Id,
                IdMarca = marcaPelikan.Id,
                CostoReposicion = 950.00m,
                PorcentajeGanancia = 50.00m,
                PrecioVenta = 1425.00m,
                StockActual = 60,
                StockMinimo = 15,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567134",
                Descripcion = "Resaltador Pelikan Fluo Verde",
                IdCategoria = catOficina.Id,
                IdMarca = marcaPelikan.Id,
                CostoReposicion = 950.00m,
                PorcentajeGanancia = 50.00m,
                PrecioVenta = 1425.00m,
                StockActual = 45,
                StockMinimo = 10,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567141",
                Descripcion = "Marcadores Pelikan Colores Pastel x6",
                IdCategoria = catOficina.Id,
                IdMarca = marcaPelikan.Id,
                CostoReposicion = 4800.00m,
                PorcentajeGanancia = 45.00m,
                PrecioVenta = 6960.00m,
                StockActual = 18,
                StockMinimo = 5,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567158",
                Descripcion = "Resma Laprida A4 75g 500 hojas",
                IdCategoria = catOficina.Id,
                IdMarca = marcaRivadavia.Id,
                CostoReposicion = 5200.00m,
                PorcentajeGanancia = 40.00m,
                PrecioVenta = 7280.00m,
                StockActual = 50,
                StockMinimo = 10,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567165",
                Descripcion = "Carpeta Escolar 3 Anillos Rivadavia",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaRivadavia.Id,
                CostoReposicion = 2800.00m,
                PorcentajeGanancia = 50.00m,
                PrecioVenta = 4200.00m,
                StockActual = 22,
                StockMinimo = 6,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567172",
                Descripcion = "Regla Plástica Cristal 30cm Maped",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaMaped.Id,
                CostoReposicion = 650.00m,
                PorcentajeGanancia = 55.00m,
                PrecioVenta = 1007.50m,
                StockActual = 35,
                StockMinimo = 8,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = "7791234567189",
                Descripcion = "Compás Escolar Maped Stop System",
                IdCategoria = catEscolar.Id,
                IdMarca = marcaMaped.Id,
                CostoReposicion = 2600.00m,
                PorcentajeGanancia = 50.00m,
                PrecioVenta = 3900.00m,
                StockActual = 14,
                StockMinimo = 4,
                EsServicio = false
            },
            // Artículos artesanales sin código de barras para probar Filtered Index (RF-04)
            new()
            {
                CodigoBarras = null,
                Descripcion = "Cuaderno Artesanal de Cuero A5",
                IdCategoria = catArtesanias.Id,
                IdMarca = marcaArtesanal.Id,
                CostoReposicion = 6500.00m,
                PorcentajeGanancia = 60.00m,
                PrecioVenta = 10400.00m,
                StockActual = 8,
                StockMinimo = 2,
                EsServicio = false
            },
            new()
            {
                CodigoBarras = null,
                Descripcion = "Señalador de Madera Rústico Pirograbado",
                IdCategoria = catArtesanias.Id,
                IdMarca = marcaArtesanal.Id,
                CostoReposicion = 900.00m,
                PorcentajeGanancia = 70.00m,
                PrecioVenta = 1530.00m,
                StockActual = 25,
                StockMinimo = 5,
                EsServicio = false
            }
        };

        await context.Articulos.AddRangeAsync(articulos);
        await context.SaveChangesAsync();
    }
}
