# Retail.Infrastructure (Capa de Infraestructura y Persistencia)

`Retail.Infrastructure` implementa el acceso a datos y la integración con servicios locales para el sistema Retail. Encapsula la persistencia relacional en Microsoft SQL Server mediante **Entity Framework Core 8**, la comunicación HTTP con el microservicio fiscal local `arcasdk`, la lectura de planillas Excel/CSV y el hashing de contraseñas.

---

## 📁 Estructura del Directorio

```text
src/Retail.Infrastructure/
├── Persistence/
│   ├── Context/
│   │   └── RetailDbContext.cs           # DbContext de EF Core, DbSets, Global Query Filters
│   ├── Configurations/                  # Mapeo Fluent API de entidades según el DER
│   │   ├── UsuarioConfiguration.cs
│   │   ├── RolConfiguration.cs
│   │   ├── ClienteConfiguration.cs      # Índices únicos por número de documento (CUIT/DNI)
│   │   ├── CobranzaClienteConfiguration.cs # Relación con Cliente y TurnoCaja
│   │   ├── TurnoCajaConfiguration.cs    # Mapeo de saldos teóricos y declarados de efectivo
│   │   ├── MovimientoCajaConfiguration.cs
│   │   ├── CategoriaConfiguration.cs
│   │   ├── MarcaConfiguration.cs
│   │   ├── ArticuloConfiguration.cs        # Filtered Index: [codigo_barras] IS NOT NULL
│   │   ├── ProveedorConfiguration.cs
│   │   ├── CatalogoProveedorConfiguration.cs
│   │   ├── VentaConfiguration.cs        # Relación con Cliente y PresupuestoOrigen
│   │   ├── DetalleVentaConfiguration.cs
│   │   ├── PagoVentaConfiguration.cs
│   │   ├── PresupuestoConfiguration.cs  # Estados, fechas y totales
│   │   ├── DetallePresupuestoConfiguration.cs
│   │   ├── ComprobanteFiscalConfiguration.cs # Mapeo de CAE, PV y motivo_error
│   │   ├── CompraConfiguration.cs
│   │   └── DetalleCompraConfiguration.cs
│   ├── Repositories/
│   │   ├── Repository.cs                # Operaciones genéricas de persistencia
│   │   └── UnitOfWork.cs                # Coordinación de transacciones y SaveChangesAsync
│   └── Migrations/                      # Migraciones automáticas de EF Core
├── ExternalServices/
│   ├── ArcaSdk/
│   │   ├── ArcaClient.cs                # Implementación de IArcaClient mediante HttpClient
│   │   └── ArcaOptions.cs               # URL base (localhost:8080), timeout, punto de venta
│   └── Excel/
│       └── ExcelCatalogParser.cs        # Implementación de IExcelCatalogParser (MiniExcel)
└── Security/
    └── PasswordHasher.cs                # Implementación de IPasswordHasher con BCrypt.Net / PBKDF2
```

---

## ⚙️ Configuraciones Relacionales Destacadas

### 1. `ArticuloConfiguration.cs` (Índice Filtrado para Artesanías y Servicios)
Permite ilimitados productos con código de barras nulo garantizando unicidad en los que sí tienen código:
```csharp
builder.Property(a => a.CodigoBarras)
       .HasMaxLength(50)
       .IsRequired(false);

builder.HasIndex(a => a.CodigoBarras)
       .IsUnique()
       .HasFilter("[codigo_barras] IS NOT NULL");
```

### 2. `CobranzaClienteConfiguration.cs`
```csharp
builder.HasKey(cc => cc.IdCobranza);
builder.HasOne(cc => cc.Cliente).WithMany().HasForeignKey(cc => cc.IdCliente).OnDelete(DeleteBehavior.Restrict);
builder.HasOne(cc => cc.TurnoCaja).WithMany().HasForeignKey(cc => cc.IdTurno).OnDelete(DeleteBehavior.Restrict);
builder.Property(cc => cc.Monto).HasPrecision(18, 2);
builder.Property(cc => cc.MedioPago).HasMaxLength(30).IsRequired();
```

### 3. `ClienteConfiguration.cs`
```csharp
builder.HasKey(c => c.IdCliente);
builder.HasIndex(c => c.NumeroDocumento).IsUnique();
builder.Property(c => c.RazonSocialONombre).HasMaxLength(150).IsRequired();
builder.Property(c => c.CondicionIva).HasMaxLength(30).IsRequired();
builder.Property(c => c.LimiteCredito).HasPrecision(18, 2);
builder.Property(c => c.SaldoCuentaCorriente).HasPrecision(18, 2);
builder.HasQueryFilter(c => c.DeletedAt == null);
```
