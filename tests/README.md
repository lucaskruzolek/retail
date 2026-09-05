# Suites de Pruebas Automatizadas (Tests)

El directorio `tests/` agrupa todos los proyectos de pruebas unitarias, de integración y de presentación del sistema **Retail**, estructurados para validar el estricto cumplimiento de las reglas de negocio, prevenir regresiones y asegurar la calidad de la arquitectura monolítica limpia.

---

## 📁 Estructura del Directorio

```text
tests/
├── Retail.Domain.UnitTests/                 # Pruebas de Entidades, Fórmulas e Invariantes
│   ├── ArticuloTests.cs                     # Descuento de stock, markup, código nulable y recálculo automático
│   ├── TurnoCajaTests.cs                    # Arqueo de efectivo, balance teórico y diferencias
│   ├── VentaTests.cs                        # Cobro multimedio, Factura A vs B
│   ├── PresupuestoTests.cs                  # Congelamiento de precios y caducidad a 15 días
│   ├── ClienteTests.cs                      # Límite de crédito y condición IVA
│   └── CobranzaClienteTests.cs              # Imputación de pagos multimedio a saldos
├── Retail.Application.UnitTests/            # Pruebas de Casos de Uso y Validadores
│   ├── VentaServiceTests.cs                 # Orquestación de venta, descuento atómico y cliente
│   ├── PresupuestoServiceTests.cs           # Conversión con validación de stock y precios
│   ├── ClienteServiceTests.cs               # RegistrarCobranzaAsync y actualización de cuenta
│   ├── CompraServiceTests.cs                # Actualización de stock y recálculo de precio por markup
│   ├── AuthServiceTests.cs                  # Flujo de login y verificación de roles
│   ├── CajaServiceTests.cs                  # Conciliación de efectivo físico
│   ├── FiscalServiceTests.cs                # Despacho asíncrono y contingencia
│   └── Validators/                          # Pruebas de reglas FluentValidation
│       ├── CrearVentaValidatorTests.cs
│       ├── CrearPresupuestoValidatorTests.cs
│       ├── CrearClienteValidatorTests.cs
│       └── RegistrarCobranzaValidatorTests.cs
├── Retail.Infrastructure.IntegrationTests/  # Pruebas de Persistencia con SQL Server Local
│   ├── RetailDbContextTests.cs              # Filtered Index en CodigoBarras (múltiples NULLs)
│   ├── UnitOfWorkTests.cs                   # Transacciones atómicas y rollback
│   └── ArcaClientTests.cs                   # Manejo de timeouts y captura de motivo_error
└── Retail.App.UnitTests/                    # Pruebas de ViewModels de Escritorio
    ├── PosViewModelTests.cs                 # Carga de presupuesto, alerta de precios y totales
    ├── ClientesViewModelTests.cs            # Búsqueda y alta de clientes
    ├── CobranzaModalViewModelTests.cs       # Validación de montos y medios de pago
    ├── LoginViewModelTests.cs               # Credenciales y navegación
    └── CobroModalViewModelTests.cs          # Pagos combinados, vuelto y cuenta corriente
```

---

## 🧩 Pruebas Representativas de Nuevas Reglas

### 1. Recálculo Automático por Compras (`Retail.Domain.UnitTests/ArticuloTests.cs`)
```csharp
[Fact]
public void ActualizarCostoYRecalcularPrecio_ConMarkupConfigurado_DebeRecalcularPrecioVenta()
{
    // Arrange: Costo original $1.000, markup 50%, precio original $1.500
    var articulo = new Articulo(null, "Cuaderno Artesanal", 1, 1, null, 1000m, 50m, 1500m, 10, 2, false);

    // Act: Ingresa compra con nuevo costo de $1.200
    articulo.ActualizarCostoYRecalcularPrecio(1200m);

    // Assert: Nuevo precio de venta = $1.200 * 1.50 = $1.800
    articulo.CostoReposicion.Should().Be(1200m);
    articulo.PrecioVenta.Should().Be(1800m);
}
```

### 2. Soporte de Múltiples Códigos de Barras Nulos (`Retail.Infrastructure.IntegrationTests/RetailDbContextTests.cs`)
```csharp
[Fact]
public async Task GuardarArticulos_ConMultiplesCodigosNulos_NoDebeLanzarExcepcionDeUnicidad()
{
    // Arrange: Dos artículos artesanales sin código de barras (null)
    var art1 = new Articulo(null, "Atril de Madera Artesanal", 1, 1, null, 5000m, 40m, 7000m, 5, 1, false);
    var art2 = new Articulo(null, "Señalador de Cuero Pintado", 1, 1, null, 800m, 50m, 1200m, 20, 5, false);

    _context.Articulos.AddRange(art1, art2);

    // Act & Assert: Gracias al índice filtrado en SQL Server, ambos se persisten sin error
    var action = async () => await _context.SaveChangesAsync();
    await action.Should().NotThrowAsync();
}
```
