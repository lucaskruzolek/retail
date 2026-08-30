# Retail.Infrastructure (Capa de Infraestructura y Persistencia)

`Retail.Infrastructure` implementa todas las **comunicaciones con el mundo exterior**, la persistencia de datos en **Microsoft SQL Server** mediante **Entity Framework Core 8**, los clientes HTTP hacia servicios locales (`arcasdk`), los servicios en segundo plano (**Background Workers**) para colas FIFO y procesamiento masivo de planillas, y la criptografía de seguridad.

---

## 🎯 Objetivos de la Capa

1. **Implementar la Persistencia Relacional con EF Core 8:**
   Mapear exhaustivamente las tablas, índices, restricciones y claves foráneas definidas en el **DER** mediante Fluent API, aislando la base de datos para que solo escuche en `127.0.0.1:1433` (`RNF-05`, `IC-02`).
2. **Garantizar el Procesamiento Secuencial FIFO Fiscal:**
   Proveer un worker en segundo plano (`FiscalInvoiceQueueWorker`) basado en `System.Threading.Channels` que despache comprobantes hacia el microservicio `arcasdk` respetando la correlatividad numérica estricta exigida por ARCA (`RF-17`, `IS-03`).
3. **Procesar Asíncronamente Grandes Volúmenes de Datos:**
   Implementar lectores de planillas Excel/CSV de distribuidores ($\ge 10.000$ filas) con bajo consumo de memoria RAM mediante `ClosedXML` o `MiniExcel` en segundo plano (`RF-07`, `RNF-02`).
4. **Implementar Criptografía y Seguridad de Alto Nivel:**
   Garantizar el hashing unidireccional con salting seguro de contraseñas (PBKDF2 / BCrypt) y la emisión y validación de tokens JWT (`RF-01`, `RNF-04`).

---

## 📁 Estructura del Directorio

```text
src/Backend/Retail.Infrastructure/
├── Persistence/                         # Acceso a Microsoft SQL Server con EF Core 8
│   ├── Context/
│   │   └── RetailDbContext.cs           # DbContext con Global Query Filters (Soft Delete)
│   ├── Configurations/                  # Mapeo Fluent API según el DER
│   │   ├── UsuarioConfiguration.cs
│   │   ├── TurnoCajaConfiguration.cs
│   │   ├── MovimientoCajaConfiguration.cs
│   │   ├── ArticuloConfiguration.cs
│   │   ├── CatalogoProveedorConfiguration.cs
│   │   ├── VentaConfiguration.cs
│   │   ├── DetalleVentaConfiguration.cs
│   │   ├── PagoVentaConfiguration.cs
│   │   ├── ComprobanteFiscalConfiguration.cs
│   │   └── CompraConfiguration.cs
│   └── Repositories/
│       ├── UnitOfWork.cs                # Transacciones ACID y SaveChangesAsync
│       └── Repository.cs                # Implementación base de repositorios
├── ExternalServices/                    # Clientes de integración externa
│   ├── ArcaSdk/                         # Integración con el microservicio fiscal local
│   │   ├── ArcaClient.cs                # Cliente HTTP tipado con IHttpClientFactory
│   │   └── ArcaOptions.cs               # URL de localhost, timeout y credenciales
│   └── Excel/                           # Motor de lectura de catálogos
│       └── ExcelCatalogParser.cs        # Lectura eficiente en streaming de .xlsx y .csv
├── BackgroundWorkers/                   # Tareas en segundo plano (BackgroundService)
│   ├── FiscalInvoiceQueueWorker.cs      # Consumidor de la cola FIFO ARCA
│   └── CatalogImportWorker.cs           # Procesador masivo de planillas de distribuidores
└── Security/                            # Criptografía e identidad
    ├── PasswordHasher.cs                # PBKDF2 con HMAC-SHA256 o BCrypt (factor >= 12)
    └── JwtTokenGenerator.cs             # Emisión y firma de JWT (HmacSha256)
```

---

## ⚙️ Funcionamiento de Componentes Técnicos

### 1. `RetailDbContext.cs` y Configuraciones Fluent API
* **Aislamiento en `127.0.0.1:1433`:** Configurado para conectarse exclusivamente a la instancia local de SQL Server, impidiendo accesos desde otras máquinas de la LAN.
* **Global Query Filters para Soft Delete:** Aplica automáticamente `builder.Entity<T>().HasQueryFilter(e => e.DeletedAt == null)` en todas las entidades derivadas de `BaseEntity`.
* **Mapeo de Tipos y Precisión Decimal:** Configura tipos `decimal(18,2)` para importes de venta, compras y cajas, y `datetime2` para marcas temporales.

```csharp
public class RetailDbContext : DbContext, IRetailDbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<SesionActiva> SesionesActivas => Set<SesionActiva>();
    public DbSet<TurnoCaja> TurnosCaja => Set<TurnoCaja>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<PagoVenta> PagosVenta => Set<PagoVenta>();
    public DbSet<ComprobanteFiscal> ComprobantesFiscales => Set<ComprobanteFiscal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RetailDbContext).Assembly);

        // Filtro Global para Soft Delete
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(ConvertFilterExpression(entityType.ClrType));
            }
        }
    }
}
```

### 2. `FiscalInvoiceQueueWorker.cs` (Cola Secuencial FIFO)
* **Objetivo:** Cumplir con la estricta normativa de ARCA que exige **correlatividad numérica sin saltos ni colisiones** cuando múltiples puestos de venta facturan simultáneamente.
* **Mecanismo:** Utiliza `Channel<FacturacionRequest>` con `BoundedChannelOptions(SingleReader = true)`.
* **Flujo:**
  1. `FiscalService` encola la solicitud en memoria de forma no bloqueante.
  2. `FiscalInvoiceQueueWorker` extrae un ítem a la vez en orden de llegada (FIFO).
  3. Invoca a `ArcaClient` para comunicarse con `arcasdk`.
  4. Si obtiene CAE exitoso: Actualiza la venta como `EMITIDO` y registra `COMPROBANTES_FISCALES`.
  5. Si ocurre timeout o corte de Internet: Marca la venta como `ERROR_FISCAL_REINTENTABLE` para posterior procesamiento desde la Consola Gerencial (`RF-18`, `RF-19`).

```csharp
public class FiscalInvoiceQueueWorker : BackgroundService
{
    private readonly ChannelReader<int> _queueReader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FiscalInvoiceQueueWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _queueReader.WaitToReadAsync(stoppingToken))
        {
            while (_queueReader.TryRead(out var ventaId))
            {
                using var scope = _scopeFactory.CreateScope();
                var arcaClient = scope.ServiceProvider.GetRequiredService<IArcaClient>();
                var dbContext = scope.ServiceProvider.GetRequiredService<RetailDbContext>();

                try
                {
                    var response = await arcaClient.EmitirComprobanteAsync(ventaId, stoppingToken);
                    // Actualizar BD con CAE otorgado
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fallo fiscal para Venta ID {VentaId}. Marcando contingencia.", ventaId);
                    // Actualizar BD como ERROR_FISCAL_REINTENTABLE
                }
            }
        }
    }
}
```

### 3. `CatalogImportWorker.cs` y `ExcelCatalogParser.cs`
* **Lectura Masiva en Streaming (`RF-07`):** Procesa archivos `.xlsx` y `.csv` de distribuidores con $\ge 10.000$ filas fila por fila (*streaming reader*), evitando cargar todo el archivo en memoria RAM.
* **Actualización en Lote (*Bulk Update*):** Mapea código, descripción y costo en `CATALOGOS_PROVEEDORES`, actualizando en bloque los precios sugeridos de los artículos enlazados mediante el markup correspondiente.

### 4. `PasswordHasher.cs` y `JwtTokenGenerator.cs`
* **Hashing (`RNF-04`):** Implementa derivación criptográfica de claves mediante PBKDF2 (HMAC-SHA256 con 100.000 iteraciones y salt criptográfico de 128 bits) o BCrypt con factor de costo $\ge 12$.
* **Emisión de Tokens JWT (`RF-01`):** Construye tokens con claims de `id_usuario`, `nombre_usuario`, `rol` y firma simétrica `HmacSha256` utilizando la clave secreta provista en `appsettings.json`.

---

## 🚫 Reglas de Dependencia

- **Dependencias Permitidas:** `Retail.Infrastructure` depende de `Retail.Application`, `Retail.Domain` y `Retail.Shared`.
- **Inyección de Dependencias:** Expone un método de extensión `AddInfrastructure(this IServiceCollection services, IConfiguration configuration)` que registra `RetailDbContext`, `UnitOfWork`, clientes HTTP, background services y componentes criptográficos para ser consumidos por `Retail.Server.Api`.
- **Aislamiento de Interfaces:** Ninguna clase fuera de `Infrastructure` debe instanciar directamente `DbContext` ni bibliotecas de terceros; todo el acceso se realiza a través de las interfaces definidas en `Retail.Application`.
